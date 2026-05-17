using System;
using System.Collections.Generic;
using UnityEngine;
using MobControl.Core;
using MobControl.Config;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Unidad aliada del ejército del jugador.
    ///
    /// SEPARACIÓN POR POSICIÓN:
    /// Después de moverse, se aplica una corrección de posición si la unidad
    /// solapa con otra. Solo mueve la posición — no altera _moveDirection,
    /// así la unidad no sale disparada sino que simplemente no puede atravesar a otra.
    ///
    /// COLOR RESET:
    /// Initialize() restaura el color original del prefab antes de aplicar cualquier
    /// override. Evita que unidades recicladas del pool mantengan el color especial.
    /// </summary>
    public class UnitController : MonoBehaviour
    {
        [SerializeField, Tooltip("Config del cañón (velocidades).")]
        private LauncherConfigSO _config;

        // ── Lista estática ───────────────────────────────────────────────

        private static readonly List<UnitController> _activeUnits = new List<UnitController>();
        public static IReadOnlyList<UnitController> ActiveUnits => _activeUnits;

        // ── Eventos y propiedades ────────────────────────────────────────

        public event Action<UnitController> OnReturnToPool;

        public int         DamageValue      { get; private set; } = 1;
        public bool        IsSpawnedByPanel { get; private set; }
        public EnemyTurret CurrentTarget    => _target;
        public Color       CurrentColor     { get; private set; }

        // ── Estado interno ───────────────────────────────────────────────

        private EnemyTurret      _target;
        private LauncherConfigSO _runtimeConfig;
        private bool             _isInitialized;
        private float            _spawnZ;
        private Vector3          _moveDirection;
        private Renderer         _renderer;
        private ArmyManager      _armyManager;

        /// <summary>Color original del prefab — usado para resetear al reciclar la unidad.</summary>
        private Color _defaultColor;

        // ── Constantes ───────────────────────────────────────────────────

        private const float SteerLerpSpeed    = 3.5f;
        private const float MinTravelDistance = 0.8f;
        private const float HitTurretDist     = 0.8f;
        private const float HitEnemyUnitDist  = 0.5f;

        /// <summary>Radio de separación. Si dos unidades están más cerca que esto, se corrige la posición.</summary>
        private const float SeparationRadius = 0.32f;

        private static readonly Vector3 ColliderSize = new Vector3(0.3f, 0.3f, 0.3f);

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            _renderer    = GetComponentInChildren<Renderer>();
            _armyManager = FindFirstObjectByType<ArmyManager>();

            // Capturar el color original del prefab UNA sola vez
            _defaultColor = _renderer != null
                ? _renderer.material.color
                : Color.blue;

            CurrentColor = _defaultColor;
            SetupPhysics();
        }

        private void OnEnable()  { _activeUnits.Add(this); }

        private void OnDisable()
        {
            _activeUnits.Remove(this);
            _isInitialized   = false;
            _target          = null;
            IsSpawnedByPanel = false;
        }

        private void Update()
        {
            if (!_isInitialized) return;
            // Pausar en derrota/victoria — evita que unidades sigan moviéndose
            // tras el fin de la partida mientras los pools aún no las han recogido
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            RefreshTarget();
            UpdateDirection();
            Move();
            ResolveOverlap();
            CheckHitTurret();
            CheckHitEnemyUnits();
        }

        // ── API pública ──────────────────────────────────────────────────

        public void Initialize(EnemyTurret target,
                               LauncherConfigSO config,
                               bool isSpawnedByPanel = false)
        {
            _target          = target;
            _runtimeConfig   = config;
            DamageValue      = 1;
            _spawnZ          = transform.position.z;
            IsSpawnedByPanel = isSpawnedByPanel;

            // Siempre resetear al color del prefab — evita el "tercer color"
            // que aparecía cuando una unidad especial era reciclada como básica
            SetColor(_defaultColor);

            _moveDirection = _target != null
                ? (_target.transform.position - transform.position).normalized
                : Vector3.forward;

            _isInitialized = true;
        }

        /// <summary>
        /// Cambia el color de la unidad. SetColor se llama DESPUÉS de Initialize()
        /// para unidades especiales, anulando el reset al defaultColor.
        /// </summary>
        public void SetColor(Color color)
        {
            CurrentColor = color;
            if (_renderer != null)
                _renderer.material.color = color;
        }

        public void ConsumeByOpponent() { ReturnToPool(); }

        // ── Movimiento ───────────────────────────────────────────────────

        private void RefreshTarget()
        {
            if (_target != null && _target.IsAlive) return;
            _target = GameManager.Instance.GetNearestActiveTurret(transform.position);
        }

        private void UpdateDirection()
        {
            if (_target == null) return;
            Vector3 toTarget = (_target.transform.position - transform.position).normalized;
            _moveDirection = Vector3.Lerp(_moveDirection, toTarget,
                                          SteerLerpSpeed * Time.deltaTime).normalized;
        }

        private void Move()
        {
            transform.position += _moveDirection * _runtimeConfig.UnitMoveSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Corrige la posición para evitar solapamiento con otras unidades.
        /// Opera sobre la posición final (después de moverse) sin tocar _moveDirection.
        /// Efecto: las unidades se "detienen" al contacto, no se empujan.
        /// </summary>
        private void ResolveOverlap()
        {
            Vector3 correction = Vector3.zero;
            int     count      = _activeUnits.Count;

            for (int i = 0; i < count; i++)
            {
                UnitController other = _activeUnits[i];
                if (other == this || !other._isInitialized) continue;

                Vector3 diff = transform.position - other.transform.position;
                float   dist = diff.magnitude;

                if (dist > 0.001f && dist < SeparationRadius)
                {
                    // Empujar solo la mitad de la distancia solapada
                    float overlap = SeparationRadius - dist;
                    correction += diff.normalized * (overlap * 0.5f);
                }
            }

            if (correction.sqrMagnitude < 0.0001f) return;

            // Aplicar solo en XZ — mantener Y fija
            Vector3 pos = transform.position;
            pos.x += correction.x;
            pos.z += correction.z;
            transform.position = pos;
        }

        // ── Detección de hits ────────────────────────────────────────────

        private void CheckHitTurret()
        {
            if (_target == null) return;

            float traveled = transform.position.z - _spawnZ;
            if (traveled < MinTravelDistance) return;

            if (Vector3.Distance(transform.position, _target.transform.position) > HitTurretDist)
                return;

            _target.TakeDamage(DamageValue);
            ReturnToPool();
        }

        private void CheckHitEnemyUnits()
        {
            float traveled = transform.position.z - _spawnZ;
            if (traveled < MinTravelDistance) return;

            IReadOnlyList<EnemyUnitController> enemies = EnemyUnitController.ActiveUnits;
            int count = enemies.Count;

            for (int i = 0; i < count; i++)
            {
                EnemyUnitController enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeSelf) continue;

                if (Vector3.Distance(transform.position, enemy.transform.position)
                    > HitEnemyUnitDist) continue;

                enemy.ConsumeByFriendly();
                _armyManager?.RemoveUnits(1);
                ReturnToPool();
                return;
            }
        }

        private void ReturnToPool()
        {
            _target = null;
            OnReturnToPool?.Invoke(this);
        }

        private void SetupPhysics()
        {
            if (!TryGetComponent<Rigidbody>(out Rigidbody rb))
                rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic   = true;
            rb.useGravity    = false;
            rb.interpolation = RigidbodyInterpolation.None;

            if (!TryGetComponent<BoxCollider>(out BoxCollider col))
                col = gameObject.AddComponent<BoxCollider>();
            col.size      = ColliderSize;
            col.isTrigger = false;
        }
    }
}