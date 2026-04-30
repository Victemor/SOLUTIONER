using UnityEngine;

/// <summary>
/// Conecta el joystick virtual invisible con el motor físico de la pelota.
/// Convierte input 2D de pantalla a dirección 3D relativa a cámara.
/// </summary>
[RequireComponent(typeof(BallMovementMotor))]
[RequireComponent(typeof(BallStateController))]
public sealed class MobileBallInputRouter : MonoBehaviour
{
    #region Inspector

    [Header("References")]

    [SerializeField]
    [Tooltip("Joystick virtual invisible de la escena.")]
    private VirtualJoystickInput joystickInput;

    [SerializeField]
    [Tooltip("Motor físico de movimiento de la pelota.")]
    private BallMovementMotor movementMotor;

    [SerializeField]
    [Tooltip("Estado del jugador.")]
    private BallStateController stateController;

    [SerializeField]
    [Tooltip("Transform de la cámara usado como referencia direccional.")]
    private Transform cameraTransform;

    [Header("Filtering")]

    [SerializeField]
    [Tooltip("Magnitud mínima para enviar movimiento al motor.")]
    [Range(0f, 0.3f)]
    private float minimumInputMagnitude = 0.025f;

    [Header("Debug")]

    [SerializeField]
    [Tooltip("Muestra logs de input y dirección calculada.")]
    private bool debugInput;

    #endregion

    #region Runtime

    private Vector2 currentInput;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        movementMotor = GetComponent<BallMovementMotor>();
        stateController = GetComponent<BallStateController>();
        joystickInput = FindFirstObjectByType<VirtualJoystickInput>();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Awake()
    {
        if (movementMotor == null)
        {
            movementMotor = GetComponent<BallMovementMotor>();
        }

        if (stateController == null)
        {
            stateController = GetComponent<BallStateController>();
        }

        if (joystickInput == null)
        {
            joystickInput = FindFirstObjectByType<VirtualJoystickInput>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void OnEnable()
    {
        if (joystickInput != null)
        {
            joystickInput.OnJoystickChanged += HandleJoystickChanged;
        }
    }

    private void OnDisable()
    {
        if (joystickInput != null)
        {
            joystickInput.OnJoystickChanged -= HandleJoystickChanged;
        }
    }

    private void FixedUpdate()
    {
        ApplyInputToMotor();
    }

    #endregion

    #region Input

    /// <summary>
    /// Guarda el último input recibido desde el joystick.
    /// </summary>
    private void HandleJoystickChanged(Vector2 input)
    {
        currentInput = input;

        if (debugInput)
        {
            Debug.Log($"[MobileBallInputRouter] Input recibido: {currentInput}");
        }
    }

    /// <summary>
    /// Convierte el input actual y lo envía al motor.
    /// </summary>
    private void ApplyInputToMotor()
    {
        if (movementMotor == null)
        {
            return;
        }

        if (stateController != null && !stateController.CanControl)
        {
            movementMotor.SetMovementInput(Vector3.zero, 0f);
            return;
        }

        float inputMagnitude = Mathf.Clamp01(currentInput.magnitude);

        if (inputMagnitude <= minimumInputMagnitude)
        {
            movementMotor.SetMovementInput(Vector3.zero, 0f);
            return;
        }

        Vector3 worldDirection = ResolveCameraRelativeDirection(currentInput.normalized);

        if (debugInput)
        {
            Debug.Log($"[MobileBallInputRouter] Dirección mundo: {worldDirection}, magnitud: {inputMagnitude}");
        }

        movementMotor.SetMovementInput(worldDirection, inputMagnitude);
    }

    /// <summary>
    /// Convierte el joystick a movimiento horizontal relativo a cámara.
    /// </summary>
    private Vector3 ResolveCameraRelativeDirection(Vector2 inputDirection)
    {
        if (cameraTransform == null)
        {
            Vector3 fallbackDirection = new Vector3(inputDirection.x, 0f, inputDirection.y);
            return fallbackDirection.sqrMagnitude < 0.0001f ? Vector3.zero : fallbackDirection.normalized;
        }

        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0f;

        Vector3 cameraRight = cameraTransform.right;
        cameraRight.y = 0f;

        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward = Vector3.forward;
        }
        else
        {
            cameraForward.Normalize();
        }

        if (cameraRight.sqrMagnitude < 0.0001f)
        {
            cameraRight = Vector3.right;
        }
        else
        {
            cameraRight.Normalize();
        }

        Vector3 direction = cameraForward * inputDirection.y + cameraRight * inputDirection.x;
        direction.y = 0f;

        return direction.sqrMagnitude < 0.0001f ? Vector3.zero : direction.normalized;
    }

    #endregion
}