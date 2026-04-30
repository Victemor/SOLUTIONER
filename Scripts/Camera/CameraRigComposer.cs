using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// Compone la pose objetivo final de la cámara.
    /// 
    /// Responsabilidades:
    /// - Elegir el offset correcto según el estado vertical.
    /// - Construir posición objetivo a partir del target y su forward horizontal.
    /// - Construir rotación objetivo mirando al target con el look offset adecuado.
    /// </summary>
    public sealed class CameraRigComposer
    {
        #region Public API

        /// <summary>
        /// Construye la pose objetivo de la cámara.
        /// </summary>
        public CameraRigPose ComposePose(
            Transform target,
            Vector3 referenceForward,
            CameraVerticalState verticalState,
            CameraFollowConfig config,
            Vector3 currentCameraPosition)
        {
            Vector3 offset = ResolveOffset(verticalState, config);
            Vector3 lookAtOffset = ResolveLookAtOffset(verticalState, config);

            Vector3 forward = referenceForward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }
            else
            {
                forward.Normalize();
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 desiredPosition = target.position
                                      - (forward * offset.z)
                                      + (Vector3.up * offset.y)
                                      + (right * offset.x);

            Vector3 lookTarget = target.position + lookAtOffset;
            Vector3 lookDirection = lookTarget - currentCameraPosition;

            Quaternion desiredRotation = lookDirection.sqrMagnitude < 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            return new CameraRigPose(desiredPosition, desiredRotation);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Devuelve el offset de posición según el estado vertical.
        /// </summary>
        private static Vector3 ResolveOffset(
            CameraVerticalState verticalState,
            CameraFollowConfig config)
        {
            return verticalState switch
            {
                CameraVerticalState.Ascending => config.AscendingOffset,
                CameraVerticalState.Descending => config.DescendingOffset,
                _ => config.NormalOffset
            };
        }

        /// <summary>
        /// Devuelve el offset de mirada según el estado vertical.
        /// </summary>
        private static Vector3 ResolveLookAtOffset(
            CameraVerticalState verticalState,
            CameraFollowConfig config)
        {
            return verticalState switch
            {
                CameraVerticalState.Ascending => config.AscendingLookAtOffset,
                CameraVerticalState.Descending => config.DescendingLookAtOffset,
                _ => config.NormalLookAtOffset
            };
        }

        #endregion
    }
}