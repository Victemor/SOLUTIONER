using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Resuelve la dirección horizontal suavizada de referencia para la cámara.
    /// Reduce giros bruscos cuando el jugador cambia de dirección o retrocede.
    /// </summary>
    public sealed class CameraForwardReferenceSolver
    {
        private const float SharpTurnAngle = 120f;
        private const float SharpTurnSpeedMultiplier = 0.25f;

        private Vector3 smoothedForward = Vector3.forward;
        private float alignmentMomentum;
        private Vector3 previousDesiredForward = Vector3.forward;

        public void Initialize(Transform target)
        {
            smoothedForward = ResolveHorizontalForward(target);
            previousDesiredForward = smoothedForward;
            alignmentMomentum = 0f;
        }

        public void SnapToTargetForward(Transform target)
        {
            smoothedForward = ResolveHorizontalForward(target);
            previousDesiredForward = smoothedForward;
            alignmentMomentum = 0f;
        }

        /// <summary>
        /// Actualiza y devuelve la dirección horizontal de referencia.
        /// </summary>
        public Vector3 UpdateReferenceForward(
            Transform target,
            CameraFollowConfig config,
            float deltaTime,
            bool freezeForwardTracking = false)
        {
            if (freezeForwardTracking)
            {
                alignmentMomentum = 0f;
                previousDesiredForward = ResolveHorizontalForward(target);
                return smoothedForward;
            }

            Vector3 desiredForward = ResolveHorizontalForward(target);

            if (smoothedForward.sqrMagnitude < 0.0001f)
            {
                smoothedForward = desiredForward;
                previousDesiredForward = desiredForward;
                return smoothedForward;
            }

            float angleToDesired = Vector3.Angle(smoothedForward, desiredForward);

            if (angleToDesired < config.MinimumDirectionAngle)
            {
                previousDesiredForward = desiredForward;
                return smoothedForward;
            }

            float angleFromPrevious = Vector3.Angle(previousDesiredForward, desiredForward);
            bool isConsistent = angleFromPrevious < config.CameraAlignmentConsistencyAngle;

            alignmentMomentum = isConsistent
                ? Mathf.MoveTowards(alignmentMomentum, 1f, config.CameraAlignmentMomentumBuildRate * deltaTime)
                : Mathf.MoveTowards(alignmentMomentum, 0f, config.CameraAlignmentMomentumDecayRate * deltaTime);

            previousDesiredForward = desiredForward;

            if (alignmentMomentum < 0.001f)
            {
                return smoothedForward;
            }

            float turnSpeedMultiplier = angleToDesired >= SharpTurnAngle
                ? SharpTurnSpeedMultiplier
                : 1f;

            float effectiveSpeed = config.ForwardAlignmentSpeed * alignmentMomentum * turnSpeedMultiplier;
            float maxRadians = Mathf.Deg2Rad * effectiveSpeed * Mathf.Max(0f, deltaTime);

            smoothedForward = Vector3.RotateTowards(smoothedForward, desiredForward, maxRadians, 0f);
            smoothedForward.y = 0f;

            if (smoothedForward.sqrMagnitude < 0.0001f)
            {
                smoothedForward = desiredForward;
            }
            else
            {
                smoothedForward.Normalize();
            }

            return smoothedForward;
        }

        private static Vector3 ResolveHorizontalForward(Transform target)
        {
            if (target == null)
            {
                return Vector3.forward;
            }

            Vector3 forward = target.forward;
            forward.y = 0f;

            return forward.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : forward.normalized;
        }
    }
}