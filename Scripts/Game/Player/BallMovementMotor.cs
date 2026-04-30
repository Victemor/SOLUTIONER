using UnityEngine;

/// <summary>
/// Motor de locomoción de la pelota para joystick virtual analógico.
/// Mantiene movimiento libre relativo a cámara, incluyendo giro, frenado y retroceso.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class BallMovementMotor : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [SerializeField]
    [Tooltip("Rigidbody asociado al jugador.")]
    private Rigidbody rb;

    [SerializeField]
    [Tooltip("Sistema de respuesta a colisiones.")]
    private BallCollisionResponder collisionResponder;

    [SerializeField]
    [Tooltip("Sensor de suelo para adaptar movimiento a pendientes.")]
    private BallGroundSensor groundSensor;

    [Header("Velocidad")]

    [SerializeField]
    [Tooltip("Velocidad máxima en m/s con el joystick a tope.")]
    private float maxSpeed = 14f;

    [SerializeField]
    [Tooltip("Aceleración base en m/s² hacia la velocidad deseada.")]
    private float accelerationResponsiveness = 18f;

    [SerializeField]
    [Tooltip("Aceleración usada cuando el jugador cambia bruscamente de dirección. Valores bajos reducen reactividad lateral y giros violentos.")]
    private float directionChangeAcceleration = 9f;

    [SerializeField]
    [Tooltip("Desaceleración natural en m/s² al soltar el joystick.")]
    private float passiveDeceleration = 8f;

    [Header("Frenado y Reversa")]

    [SerializeField]
    [Tooltip("Fuerza de frenado en m/s² cuando el joystick apunta opuesto a la velocidad actual.")]
    private float activeBrakeForce = 24f;

    [SerializeField]
    [Tooltip("Umbral de dot product bajo el cual se considera que el joystick va en contra de la velocidad actual.")]
    [Range(-1f, 0f)]
    private float brakeDotThreshold = -0.4f;

    [SerializeField]
    [Tooltip("Velocidad mínima en m/s a partir de la cual se activa el frenado activo.")]
    private float brakingSpeedThreshold = 0.45f;

    [Header("Rotación automática")]

    [SerializeField]
    [Tooltip("Velocidad en grados/segundo a la que la pelota rota en Y para orientarse hacia su dirección de movimiento.")]
    private float autoRotateSpeed = 320f;

    [SerializeField]
    [Tooltip("Velocidad mínima de movimiento para que se active la rotación automática.")]
    private float minSpeedForAutoRotation = 0.35f;

    [Header("Movimiento físico")]

    [SerializeField]
    [Tooltip("Control horizontal en el aire. 0 = sin control, 1 = control completo.")]
    [Range(0f, 1f)]
    private float airControlFactor = 0.35f;

    [SerializeField]
    [Tooltip("Factor de deslizamiento lateral cuando hay bloqueo frontal.")]
    [Range(0f, 1f)]
    private float blockedSlideFactor = 0.9f;

    [Header("Slope Handling")]

    [SerializeField]
    [Tooltip("Multiplicador de velocidad mínima al subir pendiente.")]
    [Range(0.1f, 1f)]
    private float uphillSpeedFactor = 0.72f;

    [SerializeField]
    [Tooltip("Multiplicador de velocidad máxima al bajar pendiente.")]
    [Range(1f, 3f)]
    private float downhillSpeedFactor = 1.2f;

    [SerializeField]
    [Tooltip("Fuerza de adhesión al suelo en pendientes.")]
    private float groundStickForce = 28f;

    [SerializeField]
    [Tooltip("Velocidad vertical máxima positiva estando grounded.")]
    private float maxGroundedUpwardVelocity = 2.5f;

    [SerializeField]
    [Tooltip("Velocidad vertical mínima conservada al bajar.")]
    private float minimumGroundedDownwardVelocity = -12f;

    [Header("Impacto")]

    [SerializeField]
    [Tooltip("Duración del retroceso por impacto.")]
    private float postImpactRecoveryDuration = 0.1f;

    [SerializeField]
    [Tooltip("Velocidad de disipación del retroceso.")]
    private float impactVelocityDecay = 16f;

    #endregion

    #region Runtime

    private Vector3 desiredMoveDirection = Vector3.forward;
    private Vector3 lastValidMoveDirection = Vector3.forward;
    private float joystickMagnitude;
    private bool hasMovementInput;

    private float impactRecoveryTimer;
    private Vector3 impactVelocity;

    private bool isForceStopping;
    private float forcedStopDeceleration;

    #endregion

    #region Properties

    /// <summary>Magnitud de la velocidad planar actual.</summary>
    public float CurrentSpeed => CurrentPlanarVelocity;

    /// <summary>Magnitud de la velocidad planar del rigidbody.</summary>
    public float CurrentPlanarVelocity
    {
        get
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }
    }

    /// <summary>Velocidad completa del rigidbody.</summary>
    public Vector3 CurrentVelocity => rb.linearVelocity;

    /// <summary>Velocidad máxima configurada.</summary>
    public float MaxSpeed => maxSpeed;

    /// <summary>Indica si el motor está procesando un impacto.</summary>
    public bool IsRecoveringFromImpact => impactRecoveryTimer > 0f;

    /// <summary>Última dirección válida de intención de movimiento.</summary>
    public Vector3 LastValidMoveDirection => lastValidMoveDirection;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        collisionResponder = GetComponent<BallCollisionResponder>();
        groundSensor = GetComponent<BallGroundSensor>();
    }

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (collisionResponder == null)
        {
            collisionResponder = GetComponent<BallCollisionResponder>();
        }

        if (groundSensor == null)
        {
            groundSensor = GetComponent<BallGroundSensor>();
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void FixedUpdate()
    {
        RefreshGroundState();
        UpdateImpactRecovery();
        AutoRotateTowardVelocity();
        ApplyForcedStop();
        ApplyMovement();
        ApplyGroundAdhesion();
        ClampGroundedVerticalVelocity();
    }

    #endregion

    #region Input API

    /// <summary>
    /// Establece el input de movimiento del joystick.
    /// El input persiste hasta recibir magnitud cero o hasta que otro sistema bloquee el control.
    /// </summary>
    public void SetMovementInput(Vector3 worldFlatDirection, float magnitude)
    {
        if (isForceStopping)
        {
            ClearMovementInput();
            return;
        }

        if (magnitude < 0.001f)
        {
            ClearMovementInput();
            return;
        }

        Vector3 flatDirection = new Vector3(worldFlatDirection.x, 0f, worldFlatDirection.z);

        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            ClearMovementInput();
            return;
        }

        desiredMoveDirection = flatDirection.normalized;
        lastValidMoveDirection = desiredMoveDirection;
        joystickMagnitude = Mathf.Clamp01(magnitude);
        hasMovementInput = true;
    }

    /// <summary>
    /// Detiene completamente el movimiento.
    /// </summary>
    public void Stop()
    {
        ClearMovementInput();

        impactRecoveryTimer = 0f;
        impactVelocity = Vector3.zero;
        isForceStopping = false;
        forcedStopDeceleration = 0f;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Escala la velocidad planar actual por un factor.
    /// </summary>
    public void MultiplySpeed(float multiplier)
    {
        float clampedMultiplier = Mathf.Clamp01(multiplier);

        Vector3 velocity = rb.linearVelocity;
        velocity.x *= clampedMultiplier;
        velocity.z *= clampedMultiplier;

        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// Aplica retroceso temporal por colisión.
    /// </summary>
    public void ApplyImpactRecoil(Vector3 recoilVelocity)
    {
        recoilVelocity.y = 0f;

        if (recoilVelocity.magnitude > maxSpeed)
        {
            recoilVelocity = recoilVelocity.normalized * maxSpeed;
        }

        impactVelocity = recoilVelocity;
        impactRecoveryTimer = postImpactRecoveryDuration;
        ClearMovementInput();

        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(recoilVelocity.x, currentVelocity.y, recoilVelocity.z);
    }

    /// <summary>
    /// Suspende la tracción durante un tiempo sin retroceso.
    /// </summary>
    public void SuppressDrive(float duration)
    {
        impactRecoveryTimer = Mathf.Max(impactRecoveryTimer, duration);
        ClearMovementInput();
    }

    /// <summary>
    /// Activa freno forzado.
    /// </summary>
    public void BeginForcedStop(float deceleration)
    {
        isForceStopping = true;
        forcedStopDeceleration = Mathf.Max(0f, deceleration);
        impactRecoveryTimer = 0f;
        impactVelocity = Vector3.zero;
        ClearMovementInput();
    }

    /// <summary>
    /// Desactiva el freno forzado.
    /// </summary>
    public void EndForcedStop()
    {
        isForceStopping = false;
        forcedStopDeceleration = 0f;
    }

    /// <summary>
    /// Teletransporta la pelota y resetea todo el estado de movimiento.
    /// </summary>
    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        Stop();

        rb.position = position;
        rb.rotation = rotation;

        Vector3 newForward = rotation * Vector3.forward;
        newForward.y = 0f;

        if (newForward.sqrMagnitude > 0.0001f)
        {
            desiredMoveDirection = newForward.normalized;
            lastValidMoveDirection = desiredMoveDirection;
        }

        if (groundSensor != null)
        {
            groundSensor.RefreshGroundStateImmediate();
        }

        Physics.SyncTransforms();
    }

    #endregion

    #region Internal Physics

    private void RefreshGroundState()
    {
        groundSensor?.RefreshGroundState();
    }

    private void UpdateImpactRecovery()
    {
        if (impactRecoveryTimer <= 0f)
        {
            impactVelocity = Vector3.zero;
            return;
        }

        impactRecoveryTimer = Mathf.Max(0f, impactRecoveryTimer - Time.fixedDeltaTime);

        if (impactVelocity.sqrMagnitude <= 0.0001f)
        {
            impactVelocity = Vector3.zero;
            return;
        }

        float nextMagnitude = Mathf.MoveTowards(
            impactVelocity.magnitude,
            0f,
            impactVelocityDecay * Time.fixedDeltaTime);

        impactVelocity = impactVelocity.normalized * nextMagnitude;
    }

    /// <summary>
    /// Rota la pelota hacia su velocidad planar real.
    /// </summary>
    private void AutoRotateTowardVelocity()
    {
        if (isForceStopping || IsRecoveringFromImpact)
        {
            return;
        }

        Vector3 planarVelocity = GetPlanarVelocity(rb.linearVelocity);

        if (planarVelocity.magnitude < minSpeedForAutoRotation)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        Quaternion nextRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            autoRotateSpeed * Time.fixedDeltaTime);

        rb.MoveRotation(nextRotation);
    }

    private void ApplyForcedStop()
    {
        if (!isForceStopping)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = GetPlanarVelocity(velocity);

        Vector3 nextPlanarVelocity = Vector3.MoveTowards(
            planarVelocity,
            Vector3.zero,
            forcedStopDeceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(nextPlanarVelocity.x, velocity.y, nextPlanarVelocity.z);
    }

    private void ApplyMovement()
    {
        if (isForceStopping)
        {
            return;
        }

        Vector3 currentVelocity = rb.linearVelocity;

        if (IsRecoveringFromImpact)
        {
            rb.linearVelocity = new Vector3(impactVelocity.x, currentVelocity.y, impactVelocity.z);
            return;
        }

        if (groundSensor != null && groundSensor.IsGrounded)
        {
            ApplyGroundedMovement(currentVelocity);
            return;
        }

        ApplyAirborneMovement(currentVelocity);
    }

    /// <summary>
    /// Aplica locomoción sobre suelo manteniendo giro y retroceso, pero evitando cambios demasiado bruscos.
    /// </summary>
    private void ApplyGroundedMovement(Vector3 currentVelocity)
    {
        Vector3 planarVelocity = GetPlanarVelocity(currentVelocity);
        float currentPlanarSpeed = planarVelocity.magnitude;

        if (!hasMovementInput)
        {
            ApplyPassiveDeceleration(currentVelocity, planarVelocity, currentPlanarSpeed);
            return;
        }

        Vector3 moveDirection = GetSlopedDirection(desiredMoveDirection);
        float targetSpeed = ResolveSlopeAdjustedSpeed(moveDirection) * joystickMagnitude;
        Vector3 targetVelocity = ResolveBlockedVelocity(moveDirection * targetSpeed);

        bool isBraking = IsActiveBraking(planarVelocity, currentPlanarSpeed);

        if (isBraking)
        {
            Vector3 brakedVelocity = Vector3.MoveTowards(
                planarVelocity,
                Vector3.zero,
                activeBrakeForce * Time.fixedDeltaTime);

            brakedVelocity = ResolveBlockedVelocity(brakedVelocity);
            rb.linearVelocity = new Vector3(brakedVelocity.x, currentVelocity.y, brakedVelocity.z);
            return;
        }

        float resolvedAcceleration = ResolveAcceleration(planarVelocity, targetVelocity);

        Vector3 nextPlanarVelocity = Vector3.MoveTowards(
            planarVelocity,
            targetVelocity,
            resolvedAcceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(nextPlanarVelocity.x, currentVelocity.y, nextPlanarVelocity.z);
    }

    private void ApplyPassiveDeceleration(
        Vector3 currentVelocity,
        Vector3 planarVelocity,
        float currentPlanarSpeed)
    {
        float newSpeed = Mathf.MoveTowards(
            currentPlanarSpeed,
            0f,
            passiveDeceleration * Time.fixedDeltaTime);

        Vector3 newPlanarVelocity = currentPlanarSpeed > 0.001f
            ? planarVelocity.normalized * newSpeed
            : Vector3.zero;

        rb.linearVelocity = new Vector3(newPlanarVelocity.x, currentVelocity.y, newPlanarVelocity.z);
    }

    private void ApplyAirborneMovement(Vector3 currentVelocity)
    {
        if (!hasMovementInput)
        {
            return;
        }

        Vector3 desiredPlanarVelocity = desiredMoveDirection * (joystickMagnitude * maxSpeed * airControlFactor);
        Vector3 currentPlanarVelocity = GetPlanarVelocity(currentVelocity);

        Vector3 nextPlanarVelocity = Vector3.MoveTowards(
            currentPlanarVelocity,
            desiredPlanarVelocity,
            accelerationResponsiveness * airControlFactor * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(nextPlanarVelocity.x, currentVelocity.y, nextPlanarVelocity.z);
    }

    private void ApplyGroundAdhesion()
    {
        if (groundSensor == null || !groundSensor.IsGrounded)
        {
            return;
        }

        if (IsRecoveringFromImpact || isForceStopping)
        {
            return;
        }

        rb.AddForce(-groundSensor.GroundNormal * groundStickForce, ForceMode.Acceleration);
    }

    private void ClampGroundedVerticalVelocity()
    {
        if (groundSensor == null || !groundSensor.IsGrounded || IsRecoveringFromImpact)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Clamp(
            velocity.y,
            minimumGroundedDownwardVelocity,
            maxGroundedUpwardVelocity);

        rb.linearVelocity = velocity;
    }

    #endregion

    #region Helpers

    private void ClearMovementInput()
    {
        hasMovementInput = false;
        joystickMagnitude = 0f;
    }

    private bool IsActiveBraking(Vector3 planarVelocity, float currentPlanarSpeed)
    {
        if (currentPlanarSpeed <= brakingSpeedThreshold)
        {
            return false;
        }

        return Vector3.Dot(desiredMoveDirection, planarVelocity.normalized) < brakeDotThreshold;
    }

    private float ResolveAcceleration(Vector3 currentVelocity, Vector3 targetVelocity)
    {
        if (currentVelocity.sqrMagnitude < 0.0001f || targetVelocity.sqrMagnitude < 0.0001f)
        {
            return accelerationResponsiveness;
        }

        float directionDot = Vector3.Dot(currentVelocity.normalized, targetVelocity.normalized);
        float directionChangeAmount = Mathf.InverseLerp(1f, -1f, directionDot);

        return Mathf.Lerp(
            accelerationResponsiveness,
            directionChangeAcceleration,
            directionChangeAmount);
    }

    /// <summary>
    /// Proyecta la dirección deseada sobre la normal del suelo actual.
    /// </summary>
    private Vector3 GetSlopedDirection(Vector3 worldFlatDirection)
    {
        if (groundSensor == null)
        {
            return worldFlatDirection;
        }

        Vector3 projected = groundSensor.GetProjectedForward(worldFlatDirection);
        return projected.sqrMagnitude < 0.0001f ? worldFlatDirection : projected.normalized;
    }

    private float ResolveSlopeAdjustedSpeed(Vector3 movementDirection)
    {
        float resolvedSpeed = maxSpeed;
        float verticalComponent = movementDirection.y;

        if (verticalComponent > 0f)
        {
            resolvedSpeed *= Mathf.Lerp(1f, uphillSpeedFactor, Mathf.Clamp01(verticalComponent));
        }
        else if (verticalComponent < 0f)
        {
            resolvedSpeed *= Mathf.Lerp(1f, downhillSpeedFactor, Mathf.Clamp01(-verticalComponent));
        }

        return Mathf.Min(resolvedSpeed, maxSpeed * downhillSpeedFactor);
    }

    private Vector3 ResolveBlockedVelocity(Vector3 desiredVelocity)
    {
        if (collisionResponder == null || !collisionResponder.HasBlockingContact)
        {
            return desiredVelocity;
        }

        Vector3 normal = collisionResponder.BlockingNormal;
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.0001f)
        {
            return desiredVelocity;
        }

        normal.Normalize();

        Vector3 desiredPlanar = GetPlanarVelocity(desiredVelocity);
        float intoWall = Vector3.Dot(desiredPlanar, -normal);

        if (intoWall <= 0f)
        {
            return desiredVelocity;
        }

        Vector3 sliding = (desiredPlanar - (-normal * intoWall)) * blockedSlideFactor;

        if (sliding.sqrMagnitude < 0.0001f)
        {
            return new Vector3(0f, desiredVelocity.y, 0f);
        }

        return new Vector3(sliding.x, desiredVelocity.y, sliding.z);
    }

    private static Vector3 GetPlanarVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        return velocity;
    }

    #endregion
}