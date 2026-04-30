using UnityEngine;
using Game.CameraSystem;

/// <summary>
/// Orquestador del sistema de cámara para móvil.
/// </summary>
public sealed class CameraFollowController : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [Tooltip("Transform del objetivo a seguir.")]
    [SerializeField] private Transform target;

    [Tooltip("Rigidbody del objetivo para lectura de velocidad vertical.")]
    [SerializeField] private Rigidbody targetRigidbody;

    [Tooltip("Motor de movimiento del jugador.")]
    [SerializeField] private BallMovementMotor movementMotor;

    [Tooltip("Configuración de la cámara.")]
    [SerializeField] private CameraFollowConfig config;

    #endregion

    #region Runtime

    private CameraVerticalStateResolver verticalStateResolver;
    private CameraForwardReferenceSolver forwardReferenceSolver;
    private CameraRigComposer rigComposer;
    private Camera cameraComponent;

    private float forwardPositionVelocity;
    private float lateralPositionVelocity;
    private float verticalPositionVelocity;

    private float extraDistanceVelocity;
    private float currentExtraDistance;

    private Vector3 currentLookAheadTarget;
    private Vector3 lookAheadVelocity;

    private float currentFov;
    private float fovVelocity;

    private Vector3 cachedReferenceForward = Vector3.forward;

    private float verticalStateIgnoreTimer;
    private bool isRespawnTransitionActive;
    private float respawnTransitionTimer;

    #endregion

    #region Properties

    public bool IsRespawnTransitionActive => isRespawnTransitionActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        verticalStateResolver = new CameraVerticalStateResolver();
        forwardReferenceSolver = new CameraForwardReferenceSolver();
        rigComposer = new CameraRigComposer();

        cameraComponent = GetComponent<Camera>();

        if (cameraComponent == null)
        {
            cameraComponent = GetComponentInChildren<Camera>();
        }

        if (target != null)
        {
            forwardReferenceSolver.Initialize(target);
            currentLookAheadTarget = target.position;
        }

        if (cameraComponent != null)
        {
            currentFov = cameraComponent.fieldOfView;

            if (config != null && config.DynamicFovEnabled)
            {
                currentFov = config.BaseFov;
                cameraComponent.fieldOfView = currentFov;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null || config == null)
        {
            return;
        }

        UpdateTimers();

        CameraVerticalState verticalState = verticalStateResolver.Resolve(
            isRespawnTransitionActive,
            verticalStateIgnoreTimer > 0f,
            targetRigidbody,
            movementMotor,
            config);

        cachedReferenceForward = forwardReferenceSolver.UpdateReferenceForward(
            target,
            config,
            Time.deltaTime,
            freezeForwardTracking: false);

        CameraRigPose desiredPose = rigComposer.ComposePose(
            target,
            cachedReferenceForward,
            verticalState,
            config,
            transform.position);

        desiredPose = ApplyDynamicDistance(desiredPose);
        desiredPose = ApplyLookAhead(desiredPose);

        ApplyPosition(desiredPose.Position, isRespawnTransitionActive);
        ApplyRotation(desiredPose.Rotation, isRespawnTransitionActive);
        UpdateDynamicFov();
        UpdateRespawnTransitionState(desiredPose);
    }

    #endregion

    #region Public API

    public void BeginRespawnTransition()
    {
        if (target == null || config == null)
        {
            return;
        }

        forwardPositionVelocity = 0f;
        lateralPositionVelocity = 0f;
        verticalPositionVelocity = 0f;
        lookAheadVelocity = Vector3.zero;

        verticalStateIgnoreTimer = config.VerticalStateIgnoreDurationAfterRespawn;
        isRespawnTransitionActive = true;
        respawnTransitionTimer = 0f;

        forwardReferenceSolver.SnapToTargetForward(target);
        verticalStateResolver.Reset();
        currentLookAheadTarget = target.position;
    }

    #endregion

    #region Dynamic Distance

    /// <summary>
    /// Aleja la cámara horizontalmente según la velocidad actual.
    /// </summary>
    private CameraRigPose ApplyDynamicDistance(CameraRigPose pose)
    {
        if (!config.DynamicDistanceEnabled || movementMotor == null)
        {
            return pose;
        }

        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        float targetExtra = config.ExtraDistanceAtMaxSpeed * speedNorm;

        currentExtraDistance = Mathf.SmoothDamp(
            currentExtraDistance,
            targetExtra,
            ref extraDistanceVelocity,
            config.DistanceSmoothTime);

        if (currentExtraDistance < 0.01f)
        {
            return pose;
        }

        Vector3 flatForward = cachedReferenceForward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude <= 0.0001f)
        {
            return pose;
        }

        flatForward.Normalize();

        Vector3 adjustedPosition = pose.Position - flatForward * currentExtraDistance;
        return new CameraRigPose(adjustedPosition, pose.Rotation);
    }

    #endregion

    #region Look-Ahead

    /// <summary>
    /// Hace que la cámara mire un punto delante de la pelota.
    /// </summary>
    private CameraRigPose ApplyLookAhead(CameraRigPose pose)
    {
        if (config.LookAheadDistance <= 0.001f || movementMotor == null)
        {
            return pose;
        }

        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        float activeLookAhead = config.LookAheadDistance * speedNorm;

        Vector3 rawLookAheadTarget =
            target.position +
            config.NormalLookAtOffset +
            cachedReferenceForward * activeLookAhead;

        currentLookAheadTarget = Vector3.SmoothDamp(
            currentLookAheadTarget,
            rawLookAheadTarget,
            ref lookAheadVelocity,
            config.LookAheadSmoothTime);

        Vector3 lookDirection = currentLookAheadTarget - transform.position;

        if (lookDirection.sqrMagnitude < 0.001f)
        {
            return pose;
        }

        Quaternion lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return new CameraRigPose(pose.Position, lookRotation);
    }

    #endregion

    #region Position

    /// <summary>
    /// Aplica posición con SmoothDamp independiente por eje.
    /// </summary>
    private void ApplyPosition(Vector3 desiredPosition, bool useRespawnSmoothing)
    {
        float forwardSmooth = useRespawnSmoothing ? config.RespawnHorizontalSmoothTime : config.HorizontalSmoothTime;
        float lateralSmooth = useRespawnSmoothing ? config.RespawnHorizontalSmoothTime : config.LateralSmoothTime;
        float verticalSmooth = useRespawnSmoothing ? config.RespawnVerticalSmoothTime : config.VerticalSmoothTime;

        Vector3 currentPosition = transform.position;

        Vector3 forwardAxis = cachedReferenceForward;
        forwardAxis.y = 0f;

        if (forwardAxis.sqrMagnitude < 0.0001f)
        {
            forwardAxis = Vector3.forward;
        }
        else
        {
            forwardAxis.Normalize();
        }

        Vector3 lateralAxis = new Vector3(-forwardAxis.z, 0f, forwardAxis.x);

        Vector3 currentPlanar = new Vector3(currentPosition.x, 0f, currentPosition.z);
        Vector3 desiredPlanar = new Vector3(desiredPosition.x, 0f, desiredPosition.z);

        float currentForward = Vector3.Dot(currentPlanar, forwardAxis);
        float currentLateral = Vector3.Dot(currentPlanar, lateralAxis);
        float desiredForward = Vector3.Dot(desiredPlanar, forwardAxis);
        float desiredLateral = Vector3.Dot(desiredPlanar, lateralAxis);

        float nextForward = Mathf.SmoothDamp(
            currentForward,
            desiredForward,
            ref forwardPositionVelocity,
            forwardSmooth);

        float nextLateral = Mathf.SmoothDamp(
            currentLateral,
            desiredLateral,
            ref lateralPositionVelocity,
            lateralSmooth);

        float nextY = Mathf.SmoothDamp(
            currentPosition.y,
            desiredPosition.y,
            ref verticalPositionVelocity,
            verticalSmooth);

        transform.position = new Vector3(
            forwardAxis.x * nextForward + lateralAxis.x * nextLateral,
            nextY,
            forwardAxis.z * nextForward + lateralAxis.z * nextLateral);
    }

    #endregion

    #region Rotation

    private void ApplyRotation(Quaternion desiredRotation, bool useRespawnSmoothing)
    {
        float speed = useRespawnSmoothing
            ? config.RespawnRotationLerpSpeed
            : config.RotationLerpSpeed;

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            speed * Time.deltaTime);
    }

    #endregion

    #region Dynamic FOV

    /// <summary>
    /// Ajusta el FOV según la velocidad de la pelota.
    /// </summary>
    private void UpdateDynamicFov()
    {
        if (cameraComponent == null || !config.DynamicFovEnabled || movementMotor == null)
        {
            return;
        }

        float speedNorm = movementMotor.MaxSpeed > 0f
            ? Mathf.Clamp01(movementMotor.CurrentPlanarVelocity / movementMotor.MaxSpeed)
            : 0f;

        float targetFov = Mathf.Lerp(config.BaseFov, config.MaxFov, speedNorm);

        currentFov = Mathf.SmoothDamp(
            currentFov,
            targetFov,
            ref fovVelocity,
            config.FovSmoothTime);

        cameraComponent.fieldOfView = currentFov;
    }

    #endregion

    #region Timers & Respawn

    private void UpdateTimers()
    {
        if (verticalStateIgnoreTimer > 0f)
        {
            verticalStateIgnoreTimer = Mathf.Max(0f, verticalStateIgnoreTimer - Time.deltaTime);
        }

        if (isRespawnTransitionActive)
        {
            respawnTransitionTimer += Time.deltaTime;
        }
    }

    private void UpdateRespawnTransitionState(CameraRigPose desiredPose)
    {
        if (!isRespawnTransitionActive || config == null)
        {
            return;
        }

        if (respawnTransitionTimer < config.MinimumRespawnTransitionDuration)
        {
            return;
        }

        float positionDistance = Vector3.Distance(transform.position, desiredPose.Position);
        float rotationAngle = Quaternion.Angle(transform.rotation, desiredPose.Rotation);

        if (positionDistance <= config.RespawnPositionTolerance &&
            rotationAngle <= config.RespawnRotationTolerance)
        {
            isRespawnTransitionActive = false;
            forwardPositionVelocity = 0f;
            lateralPositionVelocity = 0f;
            verticalPositionVelocity = 0f;
        }
    }

    #endregion
}