using SkyCircuit.Profiles;
using UnityEngine;

namespace SkyCircuit.Flight
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SkyCircuitFlightController : MonoBehaviour
    {
        [SerializeField] private CompetitorProfile profile;
        [SerializeField] private FlightSpeedModule speedModule = new FlightSpeedModule();
        [SerializeField] private FlightDashSkillModule dashSkillModule = new FlightDashSkillModule();

        [Header("Steering")]
        [SerializeField] private float mouseYawSensitivity = 0.18f;
        [SerializeField] private float mousePitchSensitivity = 0.18f;
        [SerializeField] private float keyboardYawRate = 115f;
        [SerializeField] private float maxPitch = 58f;
        [SerializeField] private float maxBank = 46f;
        [SerializeField] private float rotationSharpness = 26f;
        [Tooltip("Manual pitch offset for velocity only. It does not rotate the body or character visual.")]
        [Range(-45f, 45f)]
        [SerializeField] private float movementVerticalOffsetDegrees = 0f;

        [Header("External Forces")]
        [SerializeField] private float externalImpulseDecay = 4f;

        private Rigidbody body;
        private FlightInputState input;
        private Vector3 externalVelocity;
        private float latestLookBank;
        private float yaw;
        private float pitch;

        public float CurrentSpeed => speedModule != null ? speedModule.CurrentSpeed : 0f;
        public float NormalizedSpeed => speedModule != null ? speedModule.NormalizedSpeed : 0f;
        public bool IsBoosting => speedModule != null && speedModule.IsBoosting;
        public float DashCharge => dashSkillModule != null ? dashSkillModule.CurrentCharge : 0f;
        public float DashMaxCharge => dashSkillModule != null ? dashSkillModule.MaxCharge : 0f;
        public float NormalizedDashCharge => dashSkillModule != null ? dashSkillModule.NormalizedCharge : 0f;
        public float DashCooldownRemaining => dashSkillModule != null ? dashSkillModule.CooldownRemaining : 0f;
        public bool IsDashCoolingDown => dashSkillModule != null && dashSkillModule.IsCoolingDown;
        public bool RequiresDashRelease => dashSkillModule != null && dashSkillModule.RequiresDashRelease;
        public bool IsDashing => dashSkillModule != null && dashSkillModule.IsDashing;
        public CompetitorProfile Profile => profile;
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
            EnsureDashSkillModule();
            ApplyProfile(profile, true);
            if (profile == null)
            {
                speedModule.Reset(body.rotation);
                dashSkillModule.Reset();
            }
        }

        public void ApplyProfile(CompetitorProfile newProfile, bool resetSpeed)
        {
            if (newProfile == null)
            {
                return;
            }

            profile = newProfile;
            EnsureSpeedModule();
            EnsureDashSkillModule();
            speedModule.ApplySettings(profile.Speed, !resetSpeed);
            dashSkillModule.ApplySettings(profile.DashSkill, !resetSpeed);
            ApplySteeringSettings(profile.Steering);

            if (resetSpeed)
            {
                Quaternion rotation = body != null ? body.rotation : transform.rotation;
                speedModule.Reset(rotation);
                dashSkillModule.Reset();
            }
        }

        public void ResetDashSkill()
        {
            EnsureDashSkillModule();
            dashSkillModule.Reset();
        }

        public void SetInput(FlightInputState state)
        {
            input = state;
            UpdateControlRotation(Time.deltaTime, state.LookDelta);
        }

        public void ApplyExternalImpulse(Vector3 velocityChange)
        {
            EnsureBody();
            externalVelocity += velocityChange;
            body.linearVelocity += velocityChange;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float actualTurnRate = UpdateRotation(dt);
            UpdateVelocity(dt, actualTurnRate);
        }

        public void ResetFlight(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            externalVelocity = Vector3.zero;

            var angles = rotation.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
            latestLookBank = 0f;
            EnsureSpeedModule();
            EnsureDashSkillModule();
            ApplyProfile(profile, false);
            speedModule.Reset(rotation);
        }

        private void ApplySteeringSettings(FlightSteeringSettings settings)
        {
            settings = settings.Validated();
            mouseYawSensitivity = settings.mouseYawSensitivity;
            mousePitchSensitivity = settings.mousePitchSensitivity;
            keyboardYawRate = settings.keyboardYawRate;
            maxPitch = settings.maxPitch;
            maxBank = settings.maxBank;
            rotationSharpness = settings.rotationSharpness;
            externalImpulseDecay = settings.externalImpulseDecay;
        }

        private void UpdateControlRotation(float dt, Vector2 lookDelta)
        {
            yaw += lookDelta.x * mouseYawSensitivity;
            yaw += input.Turn * keyboardYawRate * dt;
            pitch -= lookDelta.y * mousePitchSensitivity;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            latestLookBank = Mathf.Clamp(lookDelta.x * 0.025f, -1f, 1f);
        }

        private float UpdateRotation(float dt)
        {
            float bankInput = Mathf.Clamp(input.Turn + latestLookBank, -1f, 1f);
            float bank = -bankInput * maxBank;

            Quaternion currentRotation = body.rotation;
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, bank);
            float blend = DampBlend(rotationSharpness, dt);
            Quaternion nextRotation = Quaternion.Slerp(currentRotation, targetRotation, blend);
            body.MoveRotation(nextRotation);

            return dt > 0f ? Quaternion.Angle(currentRotation, nextRotation) / dt : 0f;
        }

        private void UpdateVelocity(float dt, float actualTurnRate)
        {
            EnsureSpeedModule();
            EnsureDashSkillModule();

            var dashInput = new FlightDashSkillInput(input.Boost);
            var dashContext = new FlightDashSkillContext(dt, speedModule.CurrentSpeed, actualTurnRate);
            FlightDashSkillOutput dashOutput = dashSkillModule.Step(dashInput, dashContext);

            var speedInput = new FlightSpeedInput(
                input.Throttle,
                input.Vertical,
                dashOutput.IsDashing,
                dashOutput.DashAcceleration);
            var speedContext = new FlightSpeedContext(dt, body.rotation, GetMovementVelocityRotation(), body.linearVelocity);
            FlightSpeedOutput output = speedModule.Step(speedInput, speedContext);

            float blend = DampBlend(speedModule.VelocitySharpness, dt);
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, output.TargetVelocity + externalVelocity, blend);
            externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, DampBlend(externalImpulseDecay, dt));
        }

        private void EnsureSpeedModule()
        {
            speedModule ??= new FlightSpeedModule();
        }

        private Quaternion GetMovementVelocityRotation()
        {
            return body.rotation * Quaternion.Euler(movementVerticalOffsetDegrees, 0f, 0f);
        }

        private void EnsureDashSkillModule()
        {
            dashSkillModule ??= new FlightDashSkillModule();
        }

        private void EnsureBody()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
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
