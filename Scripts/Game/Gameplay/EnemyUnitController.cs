using System;
using System.Collections.Generic;
using UnityEngine;
using MobControl.Core;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Unidad enemiga. Misma lógica de separación por posición que UnitController.
    /// </summary>
    public class EnemyUnitController : MonoBehaviour
    {
        private static readonly List<EnemyUnitController> _activeUnits =
            new List<EnemyUnitController>();

        public static IReadOnlyList<EnemyUnitController> ActiveUnits => _activeUnits;

        public event Action<EnemyUnitController> OnReturnToPool;

        private float     _moveSpeed;
        private Transform _cannonTransform;   // referencia viva — se lee cada frame
        private ArmyManager _armyManager;
        private bool        _isInitialized;
        private float       _spawnZ;
        private Vector3     _moveDirection;

        private const float SteerLerpSpeed    = 2.5f;
        private const float MinTravelDistance = 1.2f;
        private const float HitCannonDist     = 0.8f;
        private const float HitEnemyUnitDist  = 0.5f;
        private const float SeparationRadius  = 0.32f;

        private static readonly Vector3 ColliderSize = new Vector3(0.3f, 0.3f, 0.3f);

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake() { SetupPhysics(); }

        private void OnEnable()  { _activeUnits.Add(this); }

        private void OnDisable()
        {
            _activeUnits.Remove(this);
            _isInitialized = false;
        }

        private void Update()
        {
            if (!_isInitialized) return;
            // Pausar en derrota/victoria — el jefe deja de atacar y
            // las unidades enemigas dejan de avanzar
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            UpdateDirection();
            Move();
            ResolveOverlap();
            CheckHitCannon();
            CheckHitSuperSoldier();
        }

        // ── API pública ──────────────────────────────────────────────────

        public void Initialize(float moveSpeed, Transform cannonTransform, ArmyManager armyManager)
        {
            _moveSpeed       = moveSpeed;
            _cannonTransform = cannonTransform;
            _armyManager     = armyManager;
            _spawnZ          = transform.position.z;

            Vector3 cannonPos = cannonTransform != null
                ? cannonTransform.position
                : new Vector3(0f, 0f, 0f);

            _moveDirection = (cannonPos - transform.position).normalized;
            _isInitialized = true;
        }

        public void ConsumeByFriendly() { ReturnToPool(); }

        // ── Movimiento ───────────────────────────────────────────────────

        private void UpdateDirection()
        {
            // Leer la posición del cañón cada frame para seguir su movimiento lateral
            Vector3 cannonPos = _cannonTransform != null
                ? _cannonTransform.position
                : Vector3.zero;

            Vector3 toTarget = (cannonPos - transform.position).normalized;
            _moveDirection = Vector3.Lerp(_moveDirection, toTarget,
                                          SteerLerpSpeed * Time.deltaTime).normalized;
        }

        private void Move()
        {
            transform.position += _moveDirection * _moveSpeed * Time.deltaTime;
        }

        /// <summary>Corrección de posición para no solapar. No altera _moveDirection.</summary>
        private void ResolveOverlap()
        {
            Vector3 correction = Vector3.zero;
            int     count      = _activeUnits.Count;

            for (int i = 0; i < count; i++)
            {
                EnemyUnitController other = _activeUnits[i];
                if (other == this || !other._isInitialized) continue;

                Vector3 diff = transform.position - other.transform.position;
                float   dist = diff.magnitude;

                if (dist > 0.001f && dist < SeparationRadius)
                {
                    float overlap = SeparationRadius - dist;
                    correction += diff.normalized * (overlap * 0.5f);
                }
            }

            if (correction.sqrMagnitude < 0.0001f) return;

            Vector3 pos = transform.position;
            pos.x += correction.x;
            pos.z += correction.z;
            transform.position = pos;
        }

        // ── Detección de hits ────────────────────────────────────────────

        private void CheckHitCannon()
        {
            float traveled = _spawnZ - transform.position.z;
            if (traveled < MinTravelDistance) return;

            Vector3 cannonPos = _cannonTransform != null
                ? _cannonTransform.position
                : Vector3.zero;

            if (Vector3.Distance(transform.position, cannonPos) > HitCannonDist) return;

            GameManager.Instance.DeclareDefeat();
            ReturnToPool();
        }

        private void CheckHitSuperSoldier()
        {
            float traveled = _spawnZ - transform.position.z;
            if (traveled < MinTravelDistance) return;

            SuperSoldierController ss = SuperSoldierController.ActiveSuperSoldier;
            if (ss == null || !ss.gameObject.activeSelf) return;

            if (Vector3.Distance(transform.position, ss.transform.position) > HitEnemyUnitDist * 2f)
                return;

            ss.TakeHit(1);
            ReturnToPool();
        }

        private void ReturnToPool() { OnReturnToPool?.Invoke(this); }

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
            col.isTrigger = true;
        }
    }
}