using System;
using SkyCircuit.Profiles;
using UnityEngine;
using UnityEngine.Serialization;

namespace SkyCircuit.Flight
{
    [Serializable]
    public sealed class FlightSpeedModule
    {
        [Header("Speed")]
        [SerializeField] private float minSpeed = 0f;
        [SerializeField] private float cruiseSpeed = 24f;
        [FormerlySerializedAs("maxSpeed")]
        [SerializeField] private float poweredMaxSpeed = 42f;
        [FormerlySerializedAs("boostSpeed")]
        [SerializeField] private float absoluteMaxSpeed = 58f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float deceleration = 30f;
        [SerializeField] private float highSpeedAccelerationScale = 0.18f;
        [SerializeField] private float overspeedReturnRate = 10f;
        [SerializeField] private float velocitySharpness = 8f;

        [Header("Vertical Energy")]
        [SerializeField] private float verticalAssistSpeed = 17f;
        [SerializeField] private float gravityEnergy = 9.8f;
        [SerializeField] private float climbEfficiency = 0.9f;
        [SerializeField] private float diveEfficiency = 0.8f;

        [Header("Turn Drag")]
        [SerializeField] private float turnLossReferenceRate = 140f;
        [SerializeField] private float turnSpeedLossRate = 0.18f;
        [SerializeField] private float turnLossMinSpeed = 4f;

        private float currentSpeed;
        private Quaternion previousBodyRotation;

        public float CurrentSpeed => currentSpeed;
        public float NormalizedSpeed => Mathf.InverseLerp(minSpeed, absoluteMaxSpeed, currentSpeed);
        public float VelocitySharpness => velocitySharpness;
        public bool IsBoosting { get; private set; }

        public void ApplySettings(FlightSpeedSettings settings, bool preserveCurrentSpeed)
        {
            settings = settings.Validated();
            minSpeed = settings.minSpeed;
            cruiseSpeed = settings.cruiseSpeed;
            poweredMaxSpeed = settings.poweredMaxSpeed;
            absoluteMaxSpeed = settings.absoluteMaxSpeed;
            acceleration = settings.acceleration;
            deceleration = settings.deceleration;
            highSpeedAccelerationScale = settings.highSpeedAccelerationScale;
            overspeedReturnRate = settings.overspeedReturnRate;
            velocitySharpness = settings.velocitySharpness;
            verticalAssistSpeed = settings.verticalAssistSpeed;
            gravityEnergy = settings.gravityEnergy;
            climbEfficiency = settings.climbEfficiency;
            diveEfficiency = settings.diveEfficiency;
            turnLossReferenceRate = settings.turnLossReferenceRate;
            turnSpeedLossRate = settings.turnSpeedLossRate;
            turnLossMinSpeed = settings.turnLossMinSpeed;

            currentSpeed = preserveCurrentSpeed
                ? Mathf.Clamp(currentSpeed, minSpeed, absoluteMaxSpeed)
                : Mathf.Clamp(cruiseSpeed, minSpeed, absoluteMaxSpeed);
        }

        public void Reset(Quaternion bodyRotation)
        {
            currentSpeed = Mathf.Clamp(cruiseSpeed, minSpeed, absoluteMaxSpeed);
            previousBodyRotation = bodyRotation;
            IsBoosting = false;
        }

        public FlightSpeedOutput Step(FlightSpeedInput input, FlightSpeedContext context)
        {
            ApplyThrottle(input, context.DeltaTime);
            ApplyGravityEnergy(input, context);
            ApplyTurnLoss(context);
            ApplyOverspeedReturn(input, context);
            return BuildOutput(input, context);
        }

        private void ApplyThrottle(FlightSpeedInput input, float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            if (input.Boost)
            {
                float dashAcceleration = input.BoostAcceleration > 0f ? input.BoostAcceleration : acceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, absoluteMaxSpeed, dashAcceleration * dt);
            }
            else if (input.Throttle > 0.05f)
            {
                float targetSpeed = poweredMaxSpeed;
                if (currentSpeed < targetSpeed)
                {
                    float accelScale = CalculateAccelerationScale(targetSpeed);
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * accelScale * input.Throttle * dt);
                }
            }
            else if (input.Throttle < -0.05f)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, minSpeed, deceleration * -input.Throttle * dt);
            }
            else
            {
                currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, absoluteMaxSpeed);
            }
        }

        private void ApplyGravityEnergy(FlightSpeedInput input, FlightSpeedContext context)
        {
            float dt = context.DeltaTime;
            if (dt <= 0f || gravityEnergy <= 0f)
            {
                return;
            }

            Vector3 forward = context.BodyRotation * Vector3.forward;
            float verticalVelocity = forward.y * currentSpeed + input.Vertical * verticalAssistSpeed;
            float climbDelta = verticalVelocity * dt;
            if (Mathf.Abs(climbDelta) < 0.0001f)
            {
                return;
            }

            float speedSquared = currentSpeed * currentSpeed;
            if (climbDelta > 0f)
            {
                speedSquared -= 2f * gravityEnergy * climbDelta * climbEfficiency;
            }
            else
            {
                speedSquared += 2f * gravityEnergy * -climbDelta * diveEfficiency;
            }

            currentSpeed = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, speedSquared)), minSpeed, absoluteMaxSpeed);
        }

        private void ApplyTurnLoss(FlightSpeedContext context)
        {
            float dt = context.DeltaTime;
            if (dt <= 0f || turnLossReferenceRate <= 0f || turnSpeedLossRate <= 0f)
            {
                previousBodyRotation = context.BodyRotation;
                return;
            }

            if (currentSpeed <= turnLossMinSpeed)
            {
                previousBodyRotation = context.BodyRotation;
                return;
            }

            float turnRate = Quaternion.Angle(previousBodyRotation, context.BodyRotation) / dt;
            float turn01 = Mathf.Clamp01(turnRate / turnLossReferenceRate);
            float speedLoss = currentSpeed * turnSpeedLossRate * turn01 * turn01 * dt;
            currentSpeed = Mathf.Max(Mathf.Max(minSpeed, turnLossMinSpeed), currentSpeed - speedLoss);
            previousBodyRotation = context.BodyRotation;
        }

        private void ApplyOverspeedReturn(FlightSpeedInput input, FlightSpeedContext context)
        {
            float dt = context.DeltaTime;
            if (dt <= 0f || overspeedReturnRate <= 0f || currentSpeed <= poweredMaxSpeed)
            {
                return;
            }

            if (input.Boost)
            {
                return;
            }

            if (EstimateVerticalVelocity(input, context) < -0.25f)
            {
                return;
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, poweredMaxSpeed, overspeedReturnRate * dt);
        }

        private FlightSpeedOutput BuildOutput(FlightSpeedInput input, FlightSpeedContext context)
        {
            currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, absoluteMaxSpeed);
            IsBoosting = input.Boost || currentSpeed > poweredMaxSpeed + 1f;

            Vector3 forwardVelocity = context.BodyRotation * Vector3.forward * currentSpeed;
            Vector3 verticalAssist = Vector3.up * (input.Vertical * verticalAssistSpeed);
            Vector3 targetVelocity = forwardVelocity + verticalAssist;
            return new FlightSpeedOutput(currentSpeed, targetVelocity, IsBoosting);
        }

        private float CalculateAccelerationScale(float targetSpeed)
        {
            float speed01 = Mathf.InverseLerp(cruiseSpeed, targetSpeed, currentSpeed);
            float eased = speed01 * speed01;
            return Mathf.Lerp(1f, Mathf.Clamp01(highSpeedAccelerationScale), eased);
        }

        private float EstimateVerticalVelocity(FlightSpeedInput input, FlightSpeedContext context)
        {
            Vector3 forward = context.BodyRotation * Vector3.forward;
            return forward.y * currentSpeed + input.Vertical * verticalAssistSpeed;
        }
    }

    public readonly struct FlightSpeedInput
    {
        public readonly float Throttle;
        public readonly float Vertical;
        public readonly bool Boost;
        public readonly float BoostAcceleration;

        public FlightSpeedInput(float throttle, float vertical, bool boost, float boostAcceleration = 0f)
        {
            Throttle = Mathf.Clamp(throttle, -1f, 1f);
            Vertical = Mathf.Clamp(vertical, -1f, 1f);
            Boost = boost;
            BoostAcceleration = Mathf.Max(0f, boostAcceleration);
        }
    }

    public readonly struct FlightSpeedContext
    {
        public readonly float DeltaTime;
        public readonly Quaternion BodyRotation;
        public readonly Vector3 CurrentVelocity;

        public FlightSpeedContext(float deltaTime, Quaternion bodyRotation, Vector3 currentVelocity)
        {
            DeltaTime = Mathf.Max(0f, deltaTime);
            BodyRotation = bodyRotation;
            CurrentVelocity = currentVelocity;
        }
    }

    public readonly struct FlightSpeedOutput
    {
        public readonly float Speed;
        public readonly Vector3 TargetVelocity;
        public readonly bool IsBoosting;

        public FlightSpeedOutput(float speed, Vector3 targetVelocity, bool isBoosting)
        {
            Speed = speed;
            TargetVelocity = targetVelocity;
            IsBoosting = isBoosting;
        }
    }
}
