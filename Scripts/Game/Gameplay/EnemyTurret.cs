using System;
using System.Collections;
using UnityEngine;
using TMPro;
using MobControl.Config;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Torreta enemiga con spawn continuo de unidades e INCOMING.
    ///
    /// SPAWN Y ESTADO:
    /// La coroutine de spawn comprueba GameState antes de cada unidad.
    /// Si el estado es WaitingForInput, pausa el spawn hasta que vuelva a Playing.
    /// Esto garantiza que las torretas no atacan entre fases ni al inicio.
    /// </summary>
    public class EnemyTurret : MonoBehaviour
    {
        [SerializeField] private TurretConfigSO _config;
        [SerializeField] private TextMeshPro    _hpLabel;
        [SerializeField] private Renderer       _bodyRenderer;

        [SerializeField] private Color _baseColor         = Color.red;
        [SerializeField] private Color _damageColor       = Color.white;
        [SerializeField] private float _damageFlashDuration = 0.12f;

        // ── Eventos ──────────────────────────────────────────────────────

        public event Action<EnemyTurret> OnDefeated;
        public event Action              OnIncomingStarted;
        public event Action              OnIncomingEnded;

        // ── Estado ───────────────────────────────────────────────────────

        public int  CurrentHP { get; private set; }
        public bool IsAlive   => CurrentHP > 0;

        protected TurretConfigSO Config => _config;

        private bool  _isIncoming;
        private bool  _isInitialized;
        private float _damageFlashTimer;

        private EnemyUnitPool _enemyUnitPool;
        private Transform     _cannonTransform;
        private ArmyManager   _armyManager;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_bodyRenderer != null)
            {
                _baseColor.a = 1f;
                _bodyRenderer.material.color = _baseColor;
            }
        }

        protected void Start()
        {
            if (!_isInitialized && _config != null)
                Initialize(_config, 0);

            GameManager.Instance.RegisterTurret(this);

            if (_config != null && _config.BaseSpawnRate > 0f)
            {
                StartCoroutine(SpawnRoutine());
                StartCoroutine(IncomingCooldownRoutine());
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.UnregisterTurret(this);
        }

        private void Update()
        {
            TickDamageFlash();
        }

        // ── API pública ──────────────────────────────────────────────────

        public void Initialize(TurretConfigSO config, int hpOverride = 0)
        {
            _config        = config;
            CurrentHP      = hpOverride > 0 ? hpOverride : config.BaseHP;
            _isInitialized = true;
            UpdateHPLabel();
        }

        public void InjectDependencies(EnemyUnitPool enemyUnitPool,
                                       Transform cannonTransform,
                                       ArmyManager armyManager)
        {
            _enemyUnitPool   = enemyUnitPool;
            _cannonTransform = cannonTransform;
            _armyManager     = armyManager;
        }

        public void TakeDamage(int amount)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Max(0, CurrentHP - amount);
            UpdateHPLabel();
            TriggerDamageFlash();
            if (CurrentHP == 0) HandleDefeated();
        }

        // ── Spawn de unidades enemigas ───────────────────────────────────

        /// <summary>
        /// La coroutine pausa mientras el estado no sea Playing.
        /// Esto evita que las torretas ataquen entre fases o al inicio.
        /// La torreta enemiga reanuda automáticamente al volver a Playing.
        /// </summary>
        private IEnumerator SpawnRoutine()
        {
            while (IsAlive)
            {
                // Esperar a que el estado sea Playing antes de spawnear
                while (GameManager.Instance.CurrentState != GameState.Playing)
                    yield return null;

                float rate     = _isIncoming ? _config.IncomingSpawnRate : _config.BaseSpawnRate;
                float interval = 1f / Mathf.Max(0.1f, rate);
                yield return new WaitForSeconds(interval);

                // Verificar de nuevo por si el estado cambió durante la espera
                if (!IsAlive) break;
                if (GameManager.Instance.CurrentState != GameState.Playing) continue;

                SpawnUnit();
            }
        }

        private void SpawnUnit()
        {
            if (_enemyUnitPool == null) return;

            Vector3 spawnPos = transform.position + Vector3.back
                               + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, 0f);

            EnemyUnitController unit = _enemyUnitPool.GetUnit(spawnPos);
            unit?.Initialize(_config.EnemyUnitSpeed, _cannonTransform, _armyManager);
        }

        // ── INCOMING ─────────────────────────────────────────────────────

        private IEnumerator IncomingCooldownRoutine()
        {
            while (IsAlive)
            {
                yield return new WaitForSeconds(_config.IncomingCooldown);
                if (!IsAlive) break;

                // Solo lanzar INCOMING si estamos jugando
                if (GameManager.Instance.CurrentState == GameState.Playing)
                    yield return IncomingBurstRoutine();
            }
        }

        private IEnumerator IncomingBurstRoutine()
        {
            _isIncoming = true;
            SetBodyColor(_config.IncomingColor);
            OnIncomingStarted?.Invoke();
            Debug.Log($"[EnemyTurret] {name} INCOMING!");

            yield return new WaitForSeconds(_config.IncomingDuration);

            _isIncoming = false;
            SetBodyColor(_baseColor);
            OnIncomingEnded?.Invoke();
        }

        // ── Internos ─────────────────────────────────────────────────────

        private void HandleDefeated()
        {
            Debug.Log($"[EnemyTurret] {name} destruida.");
            OnDefeated?.Invoke(this);
            gameObject.SetActive(false);
        }

        private void UpdateHPLabel()
        {
            if (_hpLabel != null) _hpLabel.text = CurrentHP.ToString();
        }

        private void TriggerDamageFlash()
        {
            if (_bodyRenderer == null) return;
            _damageFlashTimer = _damageFlashDuration;
            SetBodyColor(_damageColor);
        }

        private void TickDamageFlash()
        {
            if (_damageFlashTimer <= 0f || _bodyRenderer == null) return;
            _damageFlashTimer -= Time.deltaTime;
            if (_damageFlashTimer <= 0f)
                SetBodyColor(_isIncoming ? _config.IncomingColor : _baseColor);
        }

        protected void SetBodyColor(Color color)
        {
            if (_bodyRenderer != null)
                _bodyRenderer.material.color = color;
        }
    }
}