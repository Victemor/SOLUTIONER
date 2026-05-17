using UnityEngine;
using MobControl.Core;
using TMPro;
using MobControl.Config;

namespace MobControl.Gameplay
{
    /// <summary>
    /// El Súper Soldado: unidad única, grande, con HP propio.
    /// Expone ActiveSuperSoldier para que EnemyUnitController lo detecte.
    /// </summary>
    public class SuperSoldierController : MonoBehaviour
    {
        // ── Referencia estática ──────────────────────────────────────────
        public static SuperSoldierController ActiveSuperSoldier { get; private set; }

        [SerializeField] private TextMeshPro _hpLabel;
        [SerializeField] private Renderer    _bodyRenderer;
        [SerializeField] private Color       _hitFlashColor   = Color.white;
        [SerializeField] private float       _hitFlashDuration = 0.1f;

        public int  MaxHP     { get; private set; }
        public int  CurrentHP { get; private set; }
        public bool IsAlive   => CurrentHP > 0;

        private EnemyTurret      _target;
        private LauncherConfigSO _config;
        private bool             _isInitialized;
        private float            _spawnZ;
        private Vector3          _moveDirection;
        private Color            _baseColor;
        private float            _hitFlashTimer;

        private const float SteerLerpSpeed    = 2f;
        private const float MinTravelDistance = 1f;
        private const float HitThreshold     = 1f;
        private static readonly Vector3 ColliderSize = new Vector3(0.8f, 0.8f, 0.8f);

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()  { SetupPhysics(); }

        private void OnEnable()  { ActiveSuperSoldier = this; }
        private void OnDisable()
        {
            if (ActiveSuperSoldier == this) ActiveSuperSoldier = null;
            _isInitialized = false;
            _target        = null;
        }

        private void Update()
        {
            if (!_isInitialized) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            TickHitFlash();
            RefreshTarget();
            UpdateDirection();
            Move();
            CheckHit();
        }

        // ── API pública ──────────────────────────────────────────────────

        public void Initialize(int maxHP, LauncherConfigSO config, Color bodyColor)
        {
            MaxHP          = maxHP;
            CurrentHP      = maxHP;
            _config        = config;
            _spawnZ        = transform.position.z;
            _baseColor     = bodyColor;
            _isInitialized = true;

            if (_bodyRenderer != null)
                _bodyRenderer.material.color = bodyColor;

            transform.localScale = Vector3.one * 1.8f;

            _target        = GameManager.Instance.GetNearestActiveTurret(transform.position);
            _moveDirection = _target != null
                ? (_target.transform.position - transform.position).normalized
                : Vector3.forward;

            UpdateHPLabel();
        }

        public void TakeHit(int damage)
        {
            if (!IsAlive) return;
            CurrentHP = Mathf.Max(0, CurrentHP - damage);
            UpdateHPLabel();
            TriggerHitFlash();
            if (CurrentHP == 0) HandleDefeated();
        }

        // ── Movimiento curvo ─────────────────────────────────────────────

        private void RefreshTarget()
        {
            if (_target != null && _target.IsAlive) return;
            _target = GameManager.Instance.GetNearestActiveTurret(transform.position);
        }

        private void UpdateDirection()
        {
            if (_target == null) return;
            Vector3 toTarget = (_target.transform.position - transform.position).normalized;
            _moveDirection   = Vector3.Lerp(_moveDirection, toTarget,
                                            SteerLerpSpeed * Time.deltaTime).normalized;
        }

        private void Move()
        {
            transform.position += _moveDirection * _config.SuperSoldierMoveSpeed * Time.deltaTime;
        }

        private void CheckHit()
        {
            if (_target == null) return;
            float traveled = transform.position.z - _spawnZ;
            if (traveled < MinTravelDistance) return;
            if (Vector3.Distance(transform.position, _target.transform.position) > HitThreshold) return;
            _target.TakeDamage(CurrentHP);
            HandleDefeated();
        }

        // ── Internos ─────────────────────────────────────────────────────

        private void HandleDefeated()
        {
            Debug.Log("[SuperSoldier] Impactó o fue derrotado.");
            gameObject.SetActive(false);
        }

        private void UpdateHPLabel()
        {
            if (_hpLabel != null) _hpLabel.text = $"{CurrentHP}/{MaxHP}";
        }

        private void TriggerHitFlash()
        {
            if (_bodyRenderer == null) return;
            _hitFlashTimer = _hitFlashDuration;
            _bodyRenderer.material.color = _hitFlashColor;
        }

        private void TickHitFlash()
        {
            if (_hitFlashTimer <= 0f || _bodyRenderer == null) return;
            _hitFlashTimer -= Time.deltaTime;
            if (_hitFlashTimer <= 0f)
                _bodyRenderer.material.color = _baseColor;
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