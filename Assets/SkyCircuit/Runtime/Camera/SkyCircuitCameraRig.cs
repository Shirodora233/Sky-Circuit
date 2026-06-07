using UnityEngine;

namespace SkyCircuit.CameraRigging
{
    public sealed class SkyCircuitCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform lookTarget;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 5.5f, -13f);
        [SerializeField] private float lookAhead = 10f;
        [SerializeField] private float verticalLookOffset = 1.8f;
        [SerializeField] private float positionSharpness = 7f;
        [SerializeField] private float rotationSharpness = 10f;

        public void SetTarget(Transform newTarget)
        {
            SetTargets(newTarget, null);
        }

        public void SetTargets(Transform newTarget, Transform newLookTarget)
        {
            target = newTarget;
            lookTarget = newLookTarget;
            if (target == null)
            {
                return;
            }

            transform.position = GetDesiredPosition();
            transform.rotation = GetDesiredRotation(transform.position);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            Vector3 desiredPosition = GetDesiredPosition();
            transform.position = Vector3.Lerp(transform.position, desiredPosition, DampBlend(positionSharpness, dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, GetDesiredRotation(transform.position), DampBlend(rotationSharpness, dt));
        }

        private Vector3 GetDesiredPosition()
        {
            Quaternion directionRotation = Quaternion.LookRotation(GetLookDirection(), Vector3.up);
            return target.position + directionRotation * localOffset;
        }

        private Quaternion GetDesiredRotation(Vector3 fromPosition)
        {
            Vector3 lookPosition = lookTarget != null
                ? lookTarget.position
                : target.position + target.forward * lookAhead + Vector3.up * verticalLookOffset;
            Vector3 toTarget = lookPosition - fromPosition;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return transform.rotation;
            }

            return Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }

        private Vector3 GetLookDirection()
        {
            if (lookTarget != null)
            {
                Vector3 toLookTarget = lookTarget.position - target.position;
                if (toLookTarget.sqrMagnitude > 0.0001f)
                {
                    return toLookTarget.normalized;
                }
            }

            return target.forward.sqrMagnitude > 0.0001f ? target.forward.normalized : Vector3.forward;
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }
    }
}
