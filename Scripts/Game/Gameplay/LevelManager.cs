using System;
using System.Collections;
using UnityEngine;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Orquesta el nivel completo.
    ///
    /// PROGRESIÓN INFINITA:
    /// Lee el nivel de PlayerPrefs. Al completar, guarda el siguiente y recarga.
    ///
    /// DEBUG: _debugStartLevel > 0 salta directamente a ese nivel ignorando PlayerPrefs.
    /// La semilla se deriva automáticamente del nivel (determinista).
    ///
    /// SECUENCIA DE VICTORIA:
    /// 1. Calcular bonus (antes de limpiar)
    /// 2. Deshabilitar SuperSoldado
    /// 3. ReturnAll() en ambos pools → todos los cubitos desaparecen
    /// 4. Pequeña pausa visual
    /// 5. Disparar OnLevelComplete + DeclareVictory → aparece la pantalla
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Serializable]
        public class PhaseSetup
        {
            public GameObject[] TurretPrefabs;
            public GameObject[] PanelPrefabs;
            public GameObject[] BlockPrefabs;
        }

        [Header("Generación")]
        [SerializeField] private bool           _useGenerator = true;
        [SerializeField] private LevelGenerator _levelGenerator;

        [Header("Fases Manuales (fallback/debug)")]
        [SerializeField] private PhaseSetup[] _phaseSetups;

        [Header("Referencias")]
        [SerializeField] private PhaseGenerator     _generator;
        [SerializeField] private LauncherController  _launcher;
        [SerializeField] private ArmyManager         _armyManager;
        [SerializeField] private UnitPool            _unitPool;
        [SerializeField] private EnemyUnitPool       _enemyUnitPool;
        [SerializeField] private Config.LevelConfigSO _levelConfig;

        [Header("Transición")]
        [SerializeField] private float _transitionDelay = 1.5f;

        [Header("Bonus final")]
        [SerializeField]
        private AnimationCurve _bonusMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1000f, 3f);

        [Header("Debug — Nivel específico")]
        [SerializeField, Tooltip("Si > 0, arranca desde este nivel ignorando el progreso guardado. " +
                                 "La semilla se calcula automáticamente desde el índice del nivel.")]
        private int _debugStartLevel = 0;

        // ── Progresión ───────────────────────────────────────────────────

        private const string LevelPrefKey = "MobControl_Level";

        public int CurrentLevelIndex { get; private set; }

        /// <summary>Bonus del nivel completo. GameResultUI se suscribe a este evento.</summary>
        public event Action<BonusData> OnLevelComplete;

        // ── Estado ───────────────────────────────────────────────────────

        private int                _currentPhaseIndex = -1;
        private bool               _isTransitioning;
        private GeneratedLevelData _generatedData;

        // ── Unity ────────────────────────────────────────────────────────

        private void Start()
        {
            if (!ValidateSetup()) return;

            // Determinar el nivel inicial
            if (_debugStartLevel > 0)
            {
                CurrentLevelIndex = _debugStartLevel;
                Debug.Log($"[LevelManager] DEBUG: arrancando en nivel {CurrentLevelIndex}");
            }
            else
            {
                CurrentLevelIndex = PlayerPrefs.GetInt(LevelPrefKey, 1);
                Debug.Log($"[LevelManager] Nivel guardado: {CurrentLevelIndex}");
            }

            GameManager.Instance.OnAllTurretsDefeated += HandleAllTurretsDefeated;

            if (_useGenerator && _levelGenerator != null)
            {
                // La semilla se deriva del nivel → determinista y sin necesidad de configurarla
                int seed = CurrentLevelIndex * 7919;
                _generatedData = _levelGenerator.Generate(CurrentLevelIndex, seed);

                if (_generatedData == null) return;
                Debug.Log($"[LevelManager] Nivel {CurrentLevelIndex} " +
                          $"(seed={_generatedData.Seed}, boss={_generatedData.IsBossLevel})");
            }

            ActivatePhase(0);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnAllTurretsDefeated -= HandleAllTurretsDefeated;
        }

        // ── Manejadores ──────────────────────────────────────────────────

        private void HandleAllTurretsDefeated()
        {
            if (_isTransitioning) return;

            if (_currentPhaseIndex >= GetTotalPhases() - 1)
                StartCoroutine(HandleLevelCompleteRoutine());
            else
                StartCoroutine(TransitionToNextPhaseRoutine());
        }

        // ── Transición entre fases ───────────────────────────────────────

        private IEnumerator TransitionToNextPhaseRoutine()
        {
            _isTransitioning = true;

            _launcher.SetInputEnabled(false);
            _launcher.DisableSuperSoldier();   // SS desaparece al cambiar de fase
            _unitPool?.ReturnAll();
            _enemyUnitPool?.ReturnAll();
            _armyManager.ClearAll();
            _generator.Clear();

            Debug.Log($"[LevelManager] Fase {_currentPhaseIndex + 1} completada.");
            yield return new WaitForSeconds(_transitionDelay);

            ActivatePhase(_currentPhaseIndex + 1);
            _launcher.SetInputEnabled(true);
            _launcher.ResetInputState();
            GameManager.Instance.DeclareWaitingForInput();

            _isTransitioning = false;
        }

        /// <summary>
        /// Secuencia de victoria:
        /// 1. Calcular bonus ANTES de limpiar (para contar los sobrevivientes).
        /// 2. Deshabilitar SS + limpiar todas las unidades del campo.
        /// 3. Pausa para que el jugador vea el campo vacío.
        /// 4. Mostrar pantalla de victoria con el bonus calculado.
        /// </summary>
        private IEnumerator HandleLevelCompleteRoutine()
        {
            _isTransitioning = true;
            _launcher.SetInputEnabled(false);

            // Calcular bonus con las unidades que sobrevivieron
            BonusData bonus = CalculateBonus();

            // Limpiar todo el campo — unidades desaparecen antes de mostrar resultados
            _launcher.DisableSuperSoldier();
            _unitPool?.ReturnAll();
            _enemyUnitPool?.ReturnAll();

            // Pausa para que la transición sea visible
            yield return new WaitForSeconds(0.6f);

            Debug.Log($"[LevelManager] Nivel {CurrentLevelIndex} completo! " +
                      $"Sobrevivientes: {bonus.Survivors} | x{bonus.Multiplier:F2}");

            OnLevelComplete?.Invoke(bonus);
            GameManager.Instance.DeclareVictory();

            _isTransitioning = false;
        }

        // ── Activación de fase ───────────────────────────────────────────

        private void ActivatePhase(int index)
        {
            _currentPhaseIndex = index;

            if (_levelConfig != null)
                _launcher.SetCannonPosition(_levelConfig.CannonStartPosition);
            else
                _launcher.SetCannonPosition(Vector3.zero);

            if (_useGenerator && _generatedData != null)
                _generator.GenerateFromData(_generatedData.Phases[index]);
            else if (_phaseSetups != null && index < _phaseSetups.Length)
            {
                PhaseSetup setup = _phaseSetups[index];
                _generator.Generate(setup.TurretPrefabs, setup.PanelPrefabs, setup.BlockPrefabs);
            }
            else
                Debug.LogError($"[LevelManager] Sin datos para fase {index}.", this);
        }

        // ── Bonus ────────────────────────────────────────────────────────

        private BonusData CalculateBonus()
        {
            int   survivors  = _armyManager.UnitCount;
            float multiplier = _bonusMultiplierCurve.Evaluate(survivors);
            return new BonusData
            {
                LevelIndex  = CurrentLevelIndex,
                Survivors   = survivors,
                Multiplier  = multiplier
            };
        }

        // ── API pública para GameResultUI ────────────────────────────────

        public static void SaveNextLevel(int currentLevel)
        {
            PlayerPrefs.SetInt(LevelPrefKey, currentLevel + 1);
            PlayerPrefs.Save();
        }

        [ContextMenu("Reset Level Progress")]
        public void ResetLevelProgress()
        {
            PlayerPrefs.DeleteKey(LevelPrefKey);
            Debug.Log("[LevelManager] Progreso reseteado → volverá al nivel 1.");
        }

        // ── Utilidades ───────────────────────────────────────────────────

        private int GetTotalPhases() =>
            (_useGenerator && _generatedData != null)
                ? _generatedData.Phases.Count
                : (_phaseSetups?.Length ?? 0);

        private bool ValidateSetup()
        {
            bool ok = true;
            if (_generator   == null) { Debug.LogError("[LevelManager] PhaseGenerator nulo.",  this); ok = false; }
            if (_launcher    == null) { Debug.LogError("[LevelManager] Launcher nulo.",        this); ok = false; }
            if (_armyManager == null) { Debug.LogError("[LevelManager] ArmyManager nulo.",     this); ok = false; }
            if (_unitPool    == null) Debug.LogWarning("[LevelManager] UnitPool no asignado.", this);
            if (_enemyUnitPool == null) Debug.LogWarning("[LevelManager] EnemyUnitPool no asignado.", this);
            return ok;
        }
    }
}