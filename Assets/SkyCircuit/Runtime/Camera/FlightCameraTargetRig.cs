using UnityEngine;

namespace SkyCircuit.CameraRigging
{
    [DefaultExecutionOrder(-50)]
    public sealed class FlightCameraTargetRig : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private float lookAheadDistance = 22f;
        [SerializeField] private float verticalLookOffset = 2.2f;
        [Range(0f, 1f)]
        [SerializeField] private float velocityDirectionWeight = 0f;
        [SerializeField] private float minVelocityForDirection = 2f;
        [SerializeField] private float directionSharpness = 8f;

        private Vector3 smoothedDirection;

        public Transform AimTarget => aimTarget;

        public void Configure(Transform newFollowTarget, Rigidbody newTargetBody, Transform newAimTarget)
        {
            followTarget = newFollowTarget;
            targetBody = newTargetBody;
            aimTarget = newAimTarget;
            SnapToTarget();
        }

        private void Awake()
        {
            if (followTarget != null && targetBody == null)
            {
                targetBody = followTarget.GetComponent<Rigidbody>();
            }
        }

        private void OnEnable()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            if (targetBody == null)
            {
                targetBody = followTarget.GetComponent<Rigidbody>();
            }

            Vector3 desiredDirection = GetDesiredDirection();
            float blend = DampBlend(directionSharpness, Time.deltaTime);
            smoothedDirection = smoothedDirection.sqrMagnitude < 0.0001f
                ? desiredDirection
                : Vector3.Slerp(smoothedDirection, desiredDirection, blend).normalized;

            transform.SetPositionAndRotation(followTarget.position, Quaternion.LookRotation(smoothedDirection, Vector3.up));
            UpdateAimTarget();
        }

        private void SnapToTarget()
        {
            if (followTarget == null)
            {
                return;
            }

            if (targetBody == null)
            {
                targetBody = followTarget.GetComponent<Rigidbody>();
            }

            smoothedDirection = GetDesiredDirection();
            transform.SetPositionAndRotation(followTarget.position, Quaternion.LookRotation(smoothedDirection, Vector3.up));
            UpdateAimTarget();
        }

        private Vector3 GetDesiredDirection()
        {
            Vector3 forward = followTarget != null ? followTarget.forward : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            if (velocityDirectionWeight <= 0f)
            {
                return forward;
            }

            Vector3 velocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            if (velocity.sqrMagnitude < minVelocityForDirection * minVelocityForDirection)
            {
                return forward;
            }

            Vector3 blendedDirection = Vector3.Slerp(forward, velocity.normalized, velocityDirectionWeight);
            return blendedDirection.sqrMagnitude < 0.0001f ? forward : blendedDirection.normalized;
        }

        private void UpdateAimTarget()
        {
            if (aimTarget == null)
            {
                return;
            }

            aimTarget.localPosition = new Vector3(0f, verticalLookOffset, lookAheadDistance);
            aimTarget.localRotation = Quaternion.identity;
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }
    }
}
