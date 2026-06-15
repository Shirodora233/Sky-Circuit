using System;
using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.CameraRigging
{
    [DefaultExecutionOrder(-50)]
    public sealed class FlightCameraTargetRig : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private SkyCircuitFlightController flightController;
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private float lookAheadDistance = 22f;
        [SerializeField] private float verticalLookOffset = 2.2f;
        [Header("View Alignment")]
        [SerializeField] private bool alignViewWithForward = false;
        [SerializeField] private bool driveCameraFollowOffset = false;
        [SerializeField] private Component cameraFollowComponent;
        [SerializeField] private Vector3 cameraFollowOffset = new Vector3(0f, 5.2f, -14f);
        [Tooltip("Additional pitch applied after the regular look-ahead target is calculated. Positive values look farther down.")]
        [Range(-45f, 75f)]
        [SerializeField] private float viewPitchDownDegrees = 7f;
        [Range(0f, 1f)]
        [SerializeField] private float velocityDirectionWeight = 0f;
        [SerializeField] private float minVelocityForDirection = 2f;
        [SerializeField] private float directionSharpness = 8f;

        private Vector3 smoothedDirection;

        public Transform AimTarget => aimTarget;

        public void SnapAimTargetToSettings()
        {
            UpdateAimTarget();
        }

        public void ConfigureCameraFollowOffset(Component followComponent, Vector3 followOffset)
        {
            cameraFollowComponent = followComponent;
            cameraFollowOffset = followOffset;
            driveCameraFollowOffset = followComponent != null;
            ApplyCameraFollowOffset();
            UpdateAimTarget();
        }

        public void Configure(
            Transform newFollowTarget,
            SkyCircuitFlightController newFlightController,
            Rigidbody newTargetBody,
            Transform newAimTarget)
        {
            followTarget = newFollowTarget;
            flightController = newFlightController;
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

            if (followTarget != null && flightController == null)
            {
                flightController = followTarget.GetComponent<SkyCircuitFlightController>();
            }
        }

        private void OnEnable()
        {
            SnapToTarget();
        }

        private void OnValidate()
        {
            ApplyCameraFollowOffset();
            UpdateAimTarget();
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

            transform.SetPositionAndRotation(followTarget.position, GetDesiredRotation());
            ApplyCameraFollowOffset();
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

            if (flightController == null)
            {
                flightController = followTarget.GetComponent<SkyCircuitFlightController>();
            }

            smoothedDirection = GetDesiredDirection();
            transform.SetPositionAndRotation(followTarget.position, GetDesiredRotation());
            UpdateAimTarget();
        }

        private Quaternion GetDesiredRotation()
        {
            if (flightController != null)
            {
                return flightController.ControlRotation;
            }

            Vector3 desiredDirection = GetDesiredDirection();
            float blend = DampBlend(directionSharpness, Time.deltaTime);
            smoothedDirection = smoothedDirection.sqrMagnitude < 0.0001f
                ? desiredDirection
                : Vector3.Slerp(smoothedDirection, desiredDirection, blend).normalized;
            return Quaternion.LookRotation(smoothedDirection, Vector3.up);
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

            aimTarget.localPosition = alignViewWithForward
                ? GetForwardAlignedAimPosition()
                : new Vector3(0f, verticalLookOffset, lookAheadDistance);
            aimTarget.localRotation = Quaternion.identity;
        }

        private Vector3 GetForwardAlignedAimPosition()
        {
            Vector3 baseAimPosition = new Vector3(0f, verticalLookOffset, lookAheadDistance);
            Vector3 cameraToAim = baseAimPosition - cameraFollowOffset;
            if (cameraToAim.sqrMagnitude < 0.0001f)
            {
                return baseAimPosition;
            }

            Vector3 pitchedCameraToAim = Quaternion.AngleAxis(viewPitchDownDegrees, Vector3.right) * cameraToAim;
            return cameraFollowOffset + pitchedCameraToAim;
        }

        private void ApplyCameraFollowOffset()
        {
            if (!driveCameraFollowOffset || cameraFollowComponent == null)
            {
                return;
            }

            Type componentType = cameraFollowComponent.GetType();
            var property = componentType.GetProperty("FollowOffset");
            if (property != null && property.CanWrite && property.PropertyType == typeof(Vector3))
            {
                property.SetValue(cameraFollowComponent, cameraFollowOffset);
                return;
            }

            var field = componentType.GetField("FollowOffset");
            if (field != null && field.FieldType == typeof(Vector3))
            {
                field.SetValue(cameraFollowComponent, cameraFollowOffset);
            }
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }
    }
}
