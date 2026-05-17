using System.Collections;
using UnityEngine;
using MobControl.Config;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Torreta jefe — Fase 2 del nivel jefe.
    /// Ataque de fila periódico con advertencia visual.
    ///
    /// ROBUSTEZ DEL WARNING:
    /// _activeWarning se destruye en tres casos:
    ///   1. Al ejecutar el ataque (flujo normal).
    ///   2. En OnDisable/OnDestroy — cubre el caso en que el jefe muere
    ///      durante la ventana de advertencia y la coroutine se detiene sin limpiar.
    ///   3. Si se inicia un nuevo ataque mientras hay uno activo (no debería ocurrir,
    ///      pero se cubre por seguridad).
    /// </summary>
    public class BossTurretController : EnemyTurret
    {
        [Header("Ataque de Fila")]
        [SerializeField, Tooltip("Segundos de advertencia antes del impacto.")]
        private float _attackWarningDuration = 2f;

        [SerializeField, Tooltip("Segundos entre ataques.")]
        private float _attackCooldown = 8f;

        [SerializeField, Tooltip("Color del indicador de advertencia.")]
        private Color _warningColor = new Color(1f, 0.2f, 0f, 0.7f);

        [SerializeField, Tooltip("Prefab del indicador visual. Si es null se crea un cubo genérico.")]
        private GameObject _warningIndicatorPrefab;

        // Referencias inyectadas por PhaseGenerator
        private LevelConfigSO      _levelConfig;
        private LauncherController _launcher;

        /// <summary>Indicador de advertencia activo. Se limpia en múltiples puntos para robustez.</summary>
        private GameObject _activeWarning;
        private Coroutine  _attackLoopCoroutine;

        // ── Unity ────────────────────────────────────────────────────────

        private new void Start()
        {
            base.Start();
            _attackLoopCoroutine = StartCoroutine(RowAttackLoop());
        }

        private void OnDisable()
        {
            // Destruir el warning SIEMPRE al desactivarse — cubre muerte durante advertencia
            DestroyActiveWarning();
        }

        private void OnDestroy()
        {
            DestroyActiveWarning();
        }

        // ── Inyección de dependencias ────────────────────────────────────

        public void InjectBossDependencies(LevelConfigSO levelConfig,
                                            LauncherController launcher)
        {
            _levelConfig = levelConfig;
            _launcher    = launcher;
        }

        // ── Ataque de fila ────────────────────────────────────────────────

        private IEnumerator RowAttackLoop()
        {
            yield return new WaitForSeconds(_attackCooldown * 0.5f);

            while (IsAlive)
            {
                yield return new WaitForSeconds(_attackCooldown);
                if (!IsAlive) break;
                yield return RowAttackRoutine();
            }
        }

        private IEnumerator RowAttackRoutine()
        {
            if (_levelConfig == null) yield break;

            int attackLane = UnityEngine.Random.Range(0, _levelConfig.LaneCount);

            // Limpiar cualquier warning previo antes de crear el nuevo
            DestroyActiveWarning();
            _activeWarning = SpawnWarningIndicator(attackLane);

            Debug.Log($"[BossTurret] Ataque en carril {attackLane} en {_attackWarningDuration}s");

            yield return new WaitForSeconds(_attackWarningDuration);

            // Limpiar el warning y ejecutar — si el jefe murió durante la espera,
            // OnDisable ya habrá destruido el warning y IsAlive será false
            DestroyActiveWarning();

            if (!IsAlive) yield break;

            ExecuteRowAttack(attackLane);
        }

        private void ExecuteRowAttack(int lane)
        {
            if (_levelConfig == null) return;

            float laneCenter = _levelConfig.GetLaneCenterX(lane);
            float halfLane   = _levelConfig.LaneWidth * 0.5f;
            float laneMin    = laneCenter - halfLane;
            float laneMax    = laneCenter + halfLane;

            // Verificar si el cañón está en la fila
            if (_launcher != null)
            {
                float cannonX = _launcher.transform.position.x;
                if (cannonX >= laneMin && cannonX <= laneMax)
                {
                    Debug.Log("[BossTurret] ¡Cañón golpeado! → Derrota.");
                    GameManager.Instance.DeclareDefeat();
                    return;
                }
            }

            // Destruir TODAS las unidades aliadas activas en ese carril
            // Primero recolectar (no modificar la lista mientras se itera)
            var toDestroy = new System.Collections.Generic.List<UnitController>();
            var activeUnits = UnitController.ActiveUnits;

            for (int i = 0; i < activeUnits.Count; i++)
            {
                UnitController unit = activeUnits[i];
                if (unit == null) continue;

                float unitX = unit.transform.position.x;
                if (unitX >= laneMin && unitX <= laneMax)
                    toDestroy.Add(unit);
            }

            // Destruir y descontar del ArmyManager
            ArmyManager army = FindFirstObjectByType<ArmyManager>();
            int destroyed = toDestroy.Count;

            foreach (UnitController unit in toDestroy)
                unit.ConsumeByOpponent();

            if (army != null && destroyed > 0)
            {
                army.RemoveUnits(destroyed);
                Debug.Log($"[BossTurret] Fila {lane} ejecutada — {destroyed} unidades destruidas.");
            }
            else
            {
                Debug.Log($"[BossTurret] Fila {lane} ejecutada — sin unidades en el carril.");
            }
        }

        // ── Warning visual ────────────────────────────────────────────────

        private GameObject SpawnWarningIndicator(int lane)
        {
            if (_levelConfig == null) return null;

            float x = _levelConfig.GetLaneCenterX(lane);

            GameObject indicator;
            if (_warningIndicatorPrefab != null)
            {
                indicator = Instantiate(_warningIndicatorPrefab);
            }
            else
            {
                indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(indicator.GetComponent<Collider>());
                if (indicator.TryGetComponent<Renderer>(out Renderer rend))
                    rend.material.color = _warningColor;
            }

            indicator.transform.position  = new Vector3(x, 0.05f, _levelConfig.TrackLength * 0.5f);
            indicator.transform.localScale = new Vector3(
                _levelConfig.LaneWidth * 0.9f,
                0.05f,
                _levelConfig.TrackLength
            );

            return indicator;
        }

        /// <summary>
        /// Destruye el warning activo de forma segura.
        /// Puede llamarse múltiples veces sin problema.
        /// </summary>
        private void DestroyActiveWarning()
        {
            if (_activeWarning == null) return;
            Destroy(_activeWarning);
            _activeWarning = null;
        }
    }
}