using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maneja la respuesta de la pelota ante colisiones con obstáculos del escenario.
/// Ignora superficies de suelo para evitar que la pista o los rieles se comporten como obstáculos.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class BallCollisionResponder : MonoBehaviour
{
    #region Inspector

    [Header("Referencias")]

    [Tooltip("Motor de movimiento de la pelota.")]
    [SerializeField] private BallMovementMotor movementMotor;

    [Tooltip("Rigidbody de la pelota.")]
    [SerializeField] private Rigidbody rb;

    [Header("Layers")]

    [Tooltip("Capas que deben considerarse suelo. No aplican retroceso, bloqueo ni supresión de movimiento.")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("Capas que deben comportarse como obstáculos inamovibles.")]
    [SerializeField] private LayerMask immovableLayers;

    [Tooltip("Capas que deben comportarse como obstáculos empujables.")]
    [SerializeField] private LayerMask pushableLayers;

    [Header("Fallback")]

    [Tooltip("Si está activo, las colisiones que no pertenezcan a ninguna capa configurada se tratarán como obstáculos.")]
    [SerializeField] private bool handleUnclassifiedCollisionsAsObstacles = false;

    [Tooltip("Multiplicador de velocidad para colisiones sin perfil específico.")]
    [SerializeField] [Range(0f, 1f)] private float defaultSpeedMultiplier = 0.9f;

    [Tooltip("Velocidad mínima de retroceso para colisiones sin perfil específico.")]
    [SerializeField] private float defaultRecoilSpeed = 1.25f;

    [Tooltip("Duración mínima de supresión de tracción para colisiones sin perfil específico.")]
    [SerializeField] private float defaultDriveSuppression = 0.08f;

    [Header("Bloqueo")]

    [Tooltip("Dot mínimo para considerar que existe bloqueo frontal real.")]
    [SerializeField] [Range(0f, 1f)] private float forwardBlockThreshold = 0.35f;

    [Tooltip("Velocidad mínima para usar la velocidad real como dirección de impacto.")]
    [SerializeField] private float minimumVelocityDirectionSpeed = 0.15f;

    #endregion

    #region Runtime

    private readonly HashSet<Collider> activeObstacleContacts = new HashSet<Collider>();
    private Vector3 blockingNormal;
    private bool hasBlockingContact;

    #endregion

    #region Properties

    /// <summary>
    /// Indica si existe una superficie bloqueando el avance frontal de la pelota.
    /// </summary>
    public bool HasBlockingContact => hasBlockingContact;

    /// <summary>
    /// Normal promedio del bloqueo frontal actual.
    /// </summary>
    public Vector3 BlockingNormal => blockingNormal;

    #endregion

    #region Unity Lifecycle

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        movementMotor = GetComponent<BallMovementMotor>();
    }

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (movementMotor == null)
        {
            movementMotor = GetComponent<BallMovementMotor>();
        }
    }

    private void FixedUpdate()
    {
        RebuildBlockingState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider otherCollider = collision.collider;

        if (otherCollider == null)
        {
            return;
        }

        int otherLayer = otherCollider.gameObject.layer;

        if (IsGroundLayer(otherLayer))
        {
            return;
        }

        if (activeObstacleContacts.Contains(otherCollider))
        {
            return;
        }

        activeObstacleContacts.Add(otherCollider);

        if (IsInLayerMask(otherLayer, immovableLayers))
        {
            HandleImmovableCollision(collision, otherCollider);
            return;
        }

        if (IsInLayerMask(otherLayer, pushableLayers))
        {
            HandlePushableCollision(collision, otherCollider);
            return;
        }

        if (handleUnclassifiedCollisionsAsObstacles)
        {
            HandleDefaultCollision(collision, otherCollider);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider otherCollider = collision.collider;

        if (otherCollider == null)
        {
            return;
        }

        int otherLayer = otherCollider.gameObject.layer;

        if (IsGroundLayer(otherLayer))
        {
            return;
        }

        RegisterBlockingFromCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider otherCollider = collision.collider;

        if (otherCollider == null)
        {
            return;
        }

        activeObstacleContacts.Remove(otherCollider);
    }

    #endregion

    #region Collision Handling

    /// <summary>
    /// Resuelve colisión contra obstáculo inmóvil aplicando retroceso temporal.
    /// </summary>
    private void HandleImmovableCollision(Collision collision, Collider otherCollider)
    {
        ImmovableObstacle obstacle = otherCollider.GetComponent<ImmovableObstacle>();

        float speedMultiplier = obstacle != null ? obstacle.SpeedMultiplier : defaultSpeedMultiplier;
        float recoilSpeed = obstacle != null ? obstacle.RecoilSpeed : defaultRecoilSpeed;
        float frontalThreshold = obstacle != null ? obstacle.FrontalImpactThreshold : 0.45f;

        movementMotor.MultiplySpeed(speedMultiplier);

        Vector3 recoilDirection = CalculateRecoilDirection(collision, frontalThreshold);
        float resolvedRecoilSpeed = CalculateResolvedRecoilSpeed(recoilSpeed);

        movementMotor.ApplyImpactRecoil(recoilDirection * resolvedRecoilSpeed);
    }

    /// <summary>
    /// Resuelve colisión contra obstáculo empujable sin rebote fuerte.
    /// </summary>
    private void HandlePushableCollision(Collision collision, Collider otherCollider)
    {
        PushableObstacle obstacle = otherCollider.GetComponent<PushableObstacle>();

        float speedMultiplier = obstacle != null ? obstacle.SpeedMultiplier : 0.9f;
        float driveSuppressionDuration = obstacle != null ? obstacle.DriveSuppressionDuration : 0.04f;
        float pushImpulse = obstacle != null ? obstacle.PushImpulse : 2f;
        float lateralPushFactor = obstacle != null ? obstacle.LateralPushFactor : 0.35f;
        float upwardImpulse = obstacle != null ? obstacle.UpwardImpulse : 0.15f;
        float torqueImpulse = obstacle != null ? obstacle.TorqueImpulse : 1.25f;

        movementMotor.MultiplySpeed(speedMultiplier);

        if (driveSuppressionDuration > 0f)
        {
            movementMotor.SuppressDrive(driveSuppressionDuration);
        }

        Rigidbody otherRigidbody = collision.rigidbody;

        if (otherRigidbody == null || otherRigidbody.isKinematic)
        {
            return;
        }

        ApplyPushableForces(
            collision,
            otherRigidbody,
            pushImpulse,
            lateralPushFactor,
            upwardImpulse,
            torqueImpulse);
    }

    /// <summary>
    /// Resuelve colisión sin perfil específico como obstáculo inmóvil.
    /// </summary>
    private void HandleDefaultCollision(Collision collision, Collider otherCollider)
    {
        movementMotor.MultiplySpeed(defaultSpeedMultiplier);

        Vector3 recoilDirection = CalculateRecoilDirection(collision, 0.45f);
        float recoilSpeed = CalculateResolvedRecoilSpeed(defaultRecoilSpeed);

        movementMotor.ApplyImpactRecoil(recoilDirection * recoilSpeed);
        movementMotor.SuppressDrive(defaultDriveSuppression);
    }

    #endregion

    #region Blocking

    /// <summary>
    /// Reinicia el estado de bloqueo para reconstruirlo con los contactos del paso actual.
    /// </summary>
    private void RebuildBlockingState()
    {
        hasBlockingContact = false;
        blockingNormal = Vector3.zero;
    }

    /// <summary>
    /// Registra contactos que bloquean la dirección real de desplazamiento del jugador.
    /// </summary>
    private void RegisterBlockingFromCollision(Collision collision)
    {
        int contactCount = collision.contactCount;

        if (contactCount <= 0)
        {
            return;
        }

        Vector3 planarMotionDirection = GetPlanarMotionDirection();
        Vector3 accumulatedNormal = Vector3.zero;
        int validCount = 0;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            normal.y = 0f;

            if (normal.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            normal.Normalize();

            float frontalDot = Vector3.Dot(planarMotionDirection, -normal);

            if (frontalDot >= forwardBlockThreshold)
            {
                accumulatedNormal += normal;
                validCount++;
            }
        }

        if (validCount <= 0)
        {
            return;
        }

        accumulatedNormal /= validCount;

        if (accumulatedNormal.sqrMagnitude < 0.0001f)
        {
            return;
        }

        accumulatedNormal.Normalize();

        if (!hasBlockingContact)
        {
            hasBlockingContact = true;
            blockingNormal = accumulatedNormal;
            return;
        }

        blockingNormal = (blockingNormal + accumulatedNormal).normalized;
    }

    #endregion

    #region Recoil

    /// <summary>
    /// Calcula la dirección de retroceso usando primero la velocidad real de entrada.
    /// </summary>
    private Vector3 CalculateRecoilDirection(Collision collision, float frontalThreshold)
    {
        ContactPoint contact = collision.GetContact(0);

        Vector3 normal = contact.normal;
        normal.y = 0f;

        Vector3 motionDirection = GetPlanarMotionDirection();

        if (normal.sqrMagnitude < 0.0001f)
        {
            return -motionDirection;
        }

        normal.Normalize();

        float frontalDot = Vector3.Dot(motionDirection, -normal);

        if (frontalDot >= frontalThreshold)
        {
            return -motionDirection;
        }

        Vector3 lateralAway = Vector3.ProjectOnPlane(normal, Vector3.up);

        if (lateralAway.sqrMagnitude < 0.0001f)
        {
            return -motionDirection;
        }

        Vector3 recoilDirection = (-motionDirection + lateralAway.normalized).normalized;

        return recoilDirection.sqrMagnitude < 0.0001f
            ? -motionDirection
            : recoilDirection;
    }

    /// <summary>
    /// Calcula la velocidad final de retroceso en función de la velocidad de entrada.
    /// </summary>
    private float CalculateResolvedRecoilSpeed(float minimumRecoilSpeed)
    {
        Vector3 currentPlanarVelocity = rb.linearVelocity;
        currentPlanarVelocity.y = 0f;

        float incomingSpeed = currentPlanarVelocity.magnitude;
        float recoilSpeed = Mathf.Max(incomingSpeed * 0.35f, minimumRecoilSpeed);

        if (movementMotor != null)
        {
            recoilSpeed = Mathf.Min(recoilSpeed, movementMotor.MaxSpeed);
        }

        return recoilSpeed;
    }

    #endregion

    #region Pushables

    /// <summary>
    /// Aplica fuerzas e impulso angular a un objeto empujable.
    /// </summary>
    private void ApplyPushableForces(
        Collision collision,
        Rigidbody targetRigidbody,
        float pushImpulse,
        float lateralPushFactor,
        float upwardImpulse,
        float torqueImpulse)
    {
        Vector3 mainDirection = GetPlanarMotionDirection();
        Vector3 right = Vector3.Cross(Vector3.up, mainDirection).normalized;

        ContactPoint contact = collision.GetContact(0);
        Vector3 toContact = contact.point - transform.position;
        toContact.y = 0f;

        float sideSign = 0f;

        if (toContact.sqrMagnitude > 0.0001f)
        {
            sideSign = Mathf.Sign(Vector3.Dot(toContact.normalized, right));
        }

        Vector3 lateralDirection = right * sideSign;
        Vector3 finalDirection = mainDirection + lateralDirection * lateralPushFactor;
        finalDirection.y = 0f;

        finalDirection = finalDirection.sqrMagnitude < 0.0001f
            ? mainDirection
            : finalDirection.normalized;

        Vector3 impulse = finalDirection * pushImpulse;
        impulse.y += upwardImpulse;

        targetRigidbody.AddForceAtPosition(impulse, contact.point, ForceMode.Impulse);

        if (torqueImpulse <= 0f)
        {
            return;
        }

        Vector3 torqueAxis = Vector3.Cross(Vector3.up, finalDirection).normalized;
        Vector3 torque = torqueAxis * torqueImpulse * (sideSign == 0f ? 1f : sideSign);

        targetRigidbody.AddTorque(torque, ForceMode.Impulse);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Indica si una capa pertenece a las capas de suelo.
    /// </summary>
    private bool IsGroundLayer(int layer)
    {
        return IsInLayerMask(layer, groundLayers);
    }

    /// <summary>
    /// Indica si una capa pertenece a un LayerMask dado.
    /// </summary>
    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// Obtiene la mejor dirección planar disponible: velocidad real, último input o forward.
    /// </summary>
    private Vector3 GetPlanarMotionDirection()
    {
        Vector3 planarVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
        planarVelocity.y = 0f;

        if (planarVelocity.magnitude >= minimumVelocityDirectionSpeed)
        {
            return planarVelocity.normalized;
        }

        if (movementMotor != null && movementMotor.LastValidMoveDirection.sqrMagnitude > 0.0001f)
        {
            return movementMotor.LastValidMoveDirection.normalized;
        }

        Vector3 planarForward = transform.forward;
        planarForward.y = 0f;

        return planarForward.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : planarForward.normalized;
    }

    #endregion
}