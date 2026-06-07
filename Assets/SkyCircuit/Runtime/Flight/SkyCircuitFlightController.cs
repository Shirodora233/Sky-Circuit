using UnityEngine;

namespace SkyCircuit.Flight
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SkyCircuitFlightController : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float minSpeed = 0f;
        [SerializeField] private float cruiseSpeed = 24f;
        [SerializeField] private float maxSpeed = 42f;
        [SerializeField] private float boostSpeed = 58f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float deceleration = 30f;
        [SerializeField] private float velocitySharpness = 8f;

        [Header("Steering")]
        [SerializeField] private float mouseYawSensitivity = 0.22f;
        [SerializeField] private float mousePitchSensitivity = 0.18f;
        [SerializeField] private float keyboardYawRate = 115f;
        [SerializeField] private float maxPitch = 58f;
        [SerializeField] private float maxBank = 46f;
        [SerializeField] private float rotationSharpness = 11f;

        [Header("Vertical")]
        [SerializeField] private float verticalSpeed = 17f;

        private Rigidbody body;
        private FlightInputState input;
        private Vector2 queuedLookDelta;
        private float yaw;
        private float pitch;
        private float currentSpeed;

        public float CurrentSpeed => currentSpeed;
        public float NormalizedSpeed => Mathf.InverseLerp(minSpeed, boostSpeed, currentSpeed);
        public bool IsBoosting => input.Boost && currentSpeed > maxSpeed + 1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
            currentSpeed = Mathf.Clamp(cruiseSpeed, minSpeed, boostSpeed);
        }

        public void SetInput(FlightInputState state)
        {
            input = state;
            queuedLookDelta += state.LookDelta;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 lookDelta = queuedLookDelta;
            queuedLookDelta = Vector2.zero;

            UpdateRotation(dt, lookDelta);
            UpdateVelocity(dt);
        }

        public void ResetFlight(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            var angles = rotation.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
            currentSpeed = Mathf.Clamp(cruiseSpeed, minSpeed, boostSpeed);
        }

        private void UpdateRotation(float dt, Vector2 lookDelta)
        {
            yaw += lookDelta.x * mouseYawSensitivity;
            yaw += input.Turn * keyboardYawRate * dt;
            pitch -= lookDelta.y * mousePitchSensitivity;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            float lookBank = Mathf.Clamp(lookDelta.x * 0.025f, -1f, 1f);
            float bankInput = Mathf.Clamp(input.Turn + lookBank, -1f, 1f);
            float bank = -bankInput * maxBank;

            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, bank);
            float blend = DampBlend(rotationSharpness, dt);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, blend));
        }

        private void UpdateVelocity(float dt)
        {
            if (input.Throttle > 0.05f)
            {
                float targetSpeed = input.Boost ? boostSpeed : maxSpeed;
                currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * input.Throttle * dt);
            }
            else if (input.Throttle < -0.05f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, minSpeed, deceleration * -input.Throttle * dt);
            }
            else
            {
                currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, boostSpeed);
            }

            Vector3 forwardVelocity = body.rotation * Vector3.forward * currentSpeed;
            Vector3 verticalVelocity = Vector3.up * (input.Vertical * verticalSpeed);
            Vector3 targetVelocity = forwardVelocity + verticalVelocity;

            float blend = DampBlend(velocitySharpness, dt);
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, targetVelocity, blend);
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-sharpness * dt);
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f)
            {
                angle -= 360f;
            }

            while (angle < -180f)
            {
                angle += 360f;
            }

            return angle;
        }
    }
}
