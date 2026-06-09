using UnityEngine;

namespace SkyCircuit.Flight
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SkyCircuitFlightController : MonoBehaviour
    {
        [SerializeField] private FlightSpeedModule speedModule = new FlightSpeedModule();

        [Header("Steering")]
        [SerializeField] private float mouseYawSensitivity = 0.18f;
        [SerializeField] private float mousePitchSensitivity = 0.18f;
        [SerializeField] private float keyboardYawRate = 115f;
        [SerializeField] private float maxPitch = 58f;
        [SerializeField] private float maxBank = 46f;
        [SerializeField] private float rotationSharpness = 26f;

        private Rigidbody body;
        private FlightInputState input;
        private float latestLookBank;
        private float yaw;
        private float pitch;

        public float CurrentSpeed => speedModule != null ? speedModule.CurrentSpeed : 0f;
        public float NormalizedSpeed => speedModule != null ? speedModule.NormalizedSpeed : 0f;
        public bool IsBoosting => speedModule != null && speedModule.IsBoosting;
        public Quaternion ControlRotation => Quaternion.Euler(pitch, yaw, 0f);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
            EnsureSpeedModule();
            speedModule.Reset(body.rotation);
        }

        public void SetInput(FlightInputState state)
        {
            input = state;
            UpdateControlRotation(Time.deltaTime, state.LookDelta);
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            UpdateRotation(dt);
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
            latestLookBank = 0f;
            EnsureSpeedModule();
            speedModule.Reset(rotation);
        }

        private void UpdateControlRotation(float dt, Vector2 lookDelta)
        {
            yaw += lookDelta.x * mouseYawSensitivity;
            yaw += input.Turn * keyboardYawRate * dt;
            pitch -= lookDelta.y * mousePitchSensitivity;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            latestLookBank = Mathf.Clamp(lookDelta.x * 0.025f, -1f, 1f);
        }

        private void UpdateRotation(float dt)
        {
            float bankInput = Mathf.Clamp(input.Turn + latestLookBank, -1f, 1f);
            float bank = -bankInput * maxBank;

            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, bank);
            float blend = DampBlend(rotationSharpness, dt);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, blend));
        }

        private void UpdateVelocity(float dt)
        {
            EnsureSpeedModule();
            var speedInput = new FlightSpeedInput(input.Throttle, input.Vertical, input.Boost);
            var speedContext = new FlightSpeedContext(dt, body.rotation, body.linearVelocity);
            FlightSpeedOutput output = speedModule.Step(speedInput, speedContext);

            float blend = DampBlend(speedModule.VelocitySharpness, dt);
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, output.TargetVelocity, blend);
        }

        private void EnsureSpeedModule()
        {
            speedModule ??= new FlightSpeedModule();
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
