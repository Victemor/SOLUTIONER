using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using MobControl.Config;
using MobControl.Core;
using MobControl.UI;

namespace MobControl.Gameplay
{
    /// <summary>
    /// Controla el cañón: movimiento, disparo básico, burst de especiales y Súper Soldado.
    ///
    /// FLUJO DE INPUT:
    /// - Estado WaitingForInput: el primer press transiciona a Playing y empieza a disparar.
    /// - Estado Playing: press = disparar, release = parar (las torretas siguen atacando).
    /// - Entre fases: SetInputEnabled(false) previene disparos durante la transición.
    ///   Al terminar la transición, el estado vuelve a WaitingForInput — no a Playing.
    ///   El jugador debe volver a presionar para reanudar.
    /// </summary>
    public class LauncherController : MonoBehaviour
    {
        [SerializeField] private LauncherConfigSO _config;
        [SerializeField] private LevelConfigSO    _levelConfig;
        [SerializeField] private UnitPool         _unitPool;
        [SerializeField] private ArmyManager      _armyManager;
        [SerializeField] private Transform        _firePoint;

        [Header("Súper Soldado")]
        [SerializeField, Tooltip("Prefab del Súper Soldado. Arrastrar desde Assets/Prefabs/Gameplay/.")]
        private GameObject _superSoldierPrefab;

        public LauncherConfigSO Config => _config;

        // ── Input ────────────────────────────────────────────────────────

        private InputAction _pointerPressAction;
        private InputAction _pointerPositionAction;

        // ── Estado ───────────────────────────────────────────────────────

        private bool      _isFiring;
        private bool      _firstReleaseReceived;   // protección contra click del botón Play
        private bool      _isInputEnabled = true;
        private bool      _isInSpecialBurst;
        private Vector2   _previousPointerScreenPos;
        private Coroutine _fireCoroutine;
        private Coroutine _specialBurstCoroutine;

        private SuperSoldierController _activeSuperSoldier;
        private ChargeBarController    _superSoldierBar;
        private ChargeBarController    _specialUnitBar;

        // ── Unity ────────────────────────────────────────────────────────

        private void Awake()
        {
            CreateInputActions();
            PrewarmSuperSoldier();
        }

        private void Start()
        {
            if (GameplayHUD.Instance != null)
            {
                _superSoldierBar = GameplayHUD.Instance.SuperSoldierBar;
                _specialUnitBar  = GameplayHUD.Instance.SpecialUnitBar;

                if (_specialUnitBar != null)
                    _specialUnitBar.OnChargeFull += HandleSpecialBarFull;
            }
        }

        private void OnEnable()
        {
            _pointerPressAction.Enable();
            _pointerPositionAction.Enable();
        }

        private void OnDisable()
        {
            _pointerPressAction.Disable();
            _pointerPositionAction.Disable();
        }

        private void OnDestroy()
        {
            _pointerPressAction.started  -= OnPointerPressed;
            _pointerPressAction.canceled -= OnPointerReleased;
            _pointerPressAction.Dispose();
            _pointerPositionAction.Dispose();

            if (_specialUnitBar != null)
                _specialUnitBar.OnChargeFull -= HandleSpecialBarFull;
        }

        private void Update()
        {
            if (!_isFiring || !_isInputEnabled) return;
            HandleMovement();
        }

        // ── API pública ──────────────────────────────────────────────────

        public void SetInputEnabled(bool enabled)
        {
            _isInputEnabled = enabled;

            if (!enabled && _isFiring)
            {
                _isFiring = false;
                StopFireCoroutine();
            }
        }

        /// <summary>
        /// Resetea el flag de input para que la fase/nivel siguiente
        /// requiera un nuevo press antes de disparar.
        /// Llamado por LevelManager tras cada transición de fase.
        /// </summary>
        public void ResetInputState()
        {
            _isFiring             = false;
            _firstReleaseReceived = false;
            StopFireCoroutine();
        }

        public void SetCannonPosition(Vector3 targetPosition)
        {
            transform.position = new Vector3(
                targetPosition.x,
                transform.position.y,
                transform.position.z
            );
        }

        // ── Callbacks de Input ───────────────────────────────────────────

        private void OnPointerPressed(InputAction.CallbackContext ctx)
        {
            // Ignorar el primer press (click del botón Play en editor)
            if (!_firstReleaseReceived) return;
            if (!_isInputEnabled) return;

            GameState state = GameManager.Instance.CurrentState;

            // Si estamos esperando input, este press arranca el juego
            if (state == GameState.WaitingForInput)
                GameManager.Instance.DeclareReady();

            // Solo disparar si estamos en Playing
            if (GameManager.Instance.CurrentState != GameState.Playing) return;

            _isFiring = true;
            _previousPointerScreenPos = _pointerPositionAction.ReadValue<Vector2>();
            _fireCoroutine = StartCoroutine(FireRoutine());
        }

        private void OnPointerReleased(InputAction.CallbackContext ctx)
        {
            // El primer release habilita el input real (evita click del botón Play)
            _firstReleaseReceived = true;

            if (!_isFiring) return;

            _isFiring = false;
            StopFireCoroutine();

            if (_superSoldierBar != null && _superSoldierBar.IsFull)
                LaunchSuperSoldier();
        }

        // ── Movimiento ───────────────────────────────────────────────────

        private void HandleMovement()
        {
            Vector2 currentPos  = _pointerPositionAction.ReadValue<Vector2>();
            float   screenDelta = currentPos.x - _previousPointerScreenPos.x;
            _previousPointerScreenPos = currentPos;

            float worldDelta = (screenDelta / Screen.width)
                               * _levelConfig.TrackWidth
                               * _config.MoveSensitivity;

            float clampedX = Mathf.Clamp(
                transform.position.x + worldDelta,
                _levelConfig.LeftBound  + _config.HorizontalMargin,
                _levelConfig.RightBound - _config.HorizontalMargin
            );

            transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
        }

        // ── Disparo ──────────────────────────────────────────────────────

        private IEnumerator FireRoutine()
        {
            var wait = new WaitForSeconds(1f / _config.FireRate);
            while (_isFiring)
            {
                // Durante el burst de especiales no se disparan unidades normales.
                // Las barras tampoco cargan — conceptualmente el cañón está en modo especial.
                if (!_isInSpecialBurst)
                {
                    SpawnBasicUnit();
                    ChargeBarTick();
                }
                yield return wait;
            }
        }

        private void SpawnBasicUnit()
        {
            EnemyTurret target = GameManager.Instance.GetNearestActiveTurret(transform.position);
            if (target == null) return;

            UnitController unit = _unitPool.GetUnit(_firePoint.position);
            if (unit == null) return;

            unit.Initialize(target, _config);
            _armyManager.AddUnits(1);
        }

        private void ChargeBarTick()
        {
            _superSoldierBar?.AddCharge(_config.SuperSoldierChargeRate);
            _specialUnitBar?.AddCharge(_config.SpecialUnitChargeRate);
        }

        // ── Burst Especiales ─────────────────────────────────────────────

        private void HandleSpecialBarFull()
        {
            if (_specialBurstCoroutine != null) return;
            _specialBurstCoroutine = StartCoroutine(SpecialBurstRoutine());
        }

        private IEnumerator SpecialBurstRoutine()
        {
            _specialUnitBar.ResetCharge();
            _isInSpecialBurst = true;

            float elapsed  = 0f;
            float interval = 1f / _config.SpecialUnitFireRate;
            var   wait     = new WaitForSeconds(interval);

            while (elapsed < _config.SpecialUnitDuration)
            {
                // El timer SIEMPRE avanza — no se pausa al soltar.
                // Las unidades solo se spawnean mientras el jugador presiona (_isFiring).
                // Al soltar: timer sigue, spawn para. Al volver a presionar: spawn reanuda.
                bool canSpawn = _isFiring
                             && GameManager.Instance.CurrentState == GameState.Playing;

                if (canSpawn)
                {
                    EnemyTurret target = GameManager.Instance.GetNearestActiveTurret(transform.position);
                    if (target != null)
                    {
                        UnitController unit = _unitPool.GetUnit(_firePoint.position);
                        if (unit != null)
                        {
                            unit.Initialize(target, _config);
                            unit.SetColor(_config.SpecialUnitColor);
                            _armyManager.AddUnits(1);
                        }
                    }
                }

                elapsed += interval;
                yield return wait;
            }

            _isInSpecialBurst      = false;
            _specialBurstCoroutine = null;
        }

        // ── Súper Soldado ────────────────────────────────────────────────

        private void PrewarmSuperSoldier()
        {
            if (_superSoldierPrefab == null) return;

            GameObject go = Instantiate(_superSoldierPrefab, transform.parent);
            _activeSuperSoldier = go.GetComponent<SuperSoldierController>();

            if (_activeSuperSoldier == null)
            {
                Debug.LogError("[LauncherController] SuperSoldier prefab sin SuperSoldierController.", this);
                Destroy(go);
                return;
            }
            go.SetActive(false);
        }

        private void LaunchSuperSoldier()
        {
            if (_activeSuperSoldier == null || _activeSuperSoldier.gameObject.activeSelf) return;

            _superSoldierBar.ResetCharge();
            _activeSuperSoldier.transform.position = _firePoint.position;
            _activeSuperSoldier.gameObject.SetActive(true);
            _activeSuperSoldier.Initialize(_config.SuperSoldierMaxHP, _config, _config.SuperSoldierColor);
        }

        // ── Utilidades ───────────────────────────────────────────────────

        private void CreateInputActions()
        {
            _pointerPressAction = new InputAction("PointerPress", InputActionType.Button);
            _pointerPressAction.AddBinding("<Mouse>/leftButton");
            _pointerPressAction.AddBinding("<Touchscreen>/primaryTouch/press");

            _pointerPositionAction = new InputAction(
                "PointerPosition", InputActionType.Value,
                expectedControlType: "Vector2");
            _pointerPositionAction.AddBinding("<Mouse>/position");
            _pointerPositionAction.AddBinding("<Touchscreen>/primaryTouch/position");

            _pointerPressAction.started  += OnPointerPressed;
            _pointerPressAction.canceled += OnPointerReleased;
        }

        private void StopFireCoroutine()
        {
            if (_fireCoroutine == null) return;
            StopCoroutine(_fireCoroutine);
            _fireCoroutine = null;
        }

        /// <summary>
        /// Desactiva el Súper Soldado si está activo en campo.
        /// LevelManager lo llama al cambiar de fase y al terminar el nivel.
        /// </summary>
        public void DisableSuperSoldier()
        {
            if (_activeSuperSoldier != null && _activeSuperSoldier.gameObject.activeSelf)
                _activeSuperSoldier.gameObject.SetActive(false);
        }
    }
}