using System;
using SkyCircuit.Profiles;
using UnityEngine;

namespace SkyCircuit.Flight
{
    [Serializable]
    public sealed class FlightDashSkillModule
    {
        [Header("Charge")]
        [SerializeField] private float maxCharge = 100f;
        [SerializeField] private float startingCharge = 40f;
        [SerializeField] private float baseTurnChargeRate = 18f;
        [SerializeField] private float typeChargeMultiplier = 1f;
        [SerializeField] private float chargeMinSpeed = 8f;
        [SerializeField] private float chargeReferenceSpeed = 42f;
        [SerializeField] private float chargeReferenceTurnRate = 150f;

        [Header("Dash")]
        [SerializeField] private float dashDrainRate = 26f;
        [SerializeField] private float dashAcceleration = 34f;
        [SerializeField] private float cooldownDuration = 2.5f;

        private float currentCharge;
        private float cooldownRemaining;
        private bool waitingForDashRelease;

        public float CurrentCharge => currentCharge;
        public float MaxCharge => maxCharge;
        public float NormalizedCharge => maxCharge > 0f ? Mathf.Clamp01(currentCharge / maxCharge) : 0f;
        public float CooldownRemaining => cooldownRemaining;
        public bool IsCoolingDown => cooldownRemaining > 0f;
        public bool RequiresDashRelease => waitingForDashRelease;
        public bool IsDashing { get; private set; }

        public void ApplySettings(DashSkillSettings settings, bool preserveCharge)
        {
            settings = settings.Validated();
            maxCharge = settings.maxCharge;
            startingCharge = settings.startingCharge;
            baseTurnChargeRate = settings.baseTurnChargeRate;
            typeChargeMultiplier = settings.typeChargeMultiplier;
            chargeMinSpeed = settings.chargeMinSpeed;
            chargeReferenceSpeed = settings.chargeReferenceSpeed;
            chargeReferenceTurnRate = settings.chargeReferenceTurnRate;
            dashDrainRate = settings.dashDrainRate;
            dashAcceleration = settings.dashAcceleration;
            cooldownDuration = settings.cooldownDuration;

            currentCharge = preserveCharge
                ? Mathf.Clamp(currentCharge, 0f, maxCharge)
                : Mathf.Clamp(startingCharge, 0f, maxCharge);
            cooldownRemaining = preserveCharge ? Mathf.Min(cooldownRemaining, cooldownDuration) : 0f;
            waitingForDashRelease = preserveCharge && waitingForDashRelease;
            IsDashing = false;
        }

        public void Reset()
        {
            currentCharge = Mathf.Clamp(startingCharge, 0f, maxCharge);
            cooldownRemaining = 0f;
            waitingForDashRelease = false;
            IsDashing = false;
        }

        public FlightDashSkillOutput Step(FlightDashSkillInput input, FlightDashSkillContext context)
        {
            float dt = context.DeltaTime;
            if (dt <= 0f || maxCharge <= 0f)
            {
                IsDashing = false;
                return BuildOutput(false, 0f);
            }

            if (!input.DashRequested)
            {
                waitingForDashRelease = false;
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
                IsDashing = false;
                return BuildOutput(false, 0f);
            }

            if (input.DashRequested && !waitingForDashRelease && currentCharge > 0f && dashDrainRate > 0f)
            {
                IsDashing = true;
                currentCharge = Mathf.Max(0f, currentCharge - dashDrainRate * dt);
                if (currentCharge <= 0f)
                {
                    BeginCooldown();
                }

                return BuildOutput(true, dashAcceleration);
            }

            IsDashing = false;
            if (!input.DashRequested)
            {
                RechargeFromTurn(context, dt);
            }

            return BuildOutput(false, 0f);
        }

        private void BeginCooldown()
        {
            cooldownRemaining = cooldownDuration;
            waitingForDashRelease = true;
        }

        private FlightDashSkillOutput BuildOutput(bool isDashing, float dashAccelerationValue)
        {
            return new FlightDashSkillOutput(
                isDashing,
                dashAccelerationValue,
                currentCharge,
                NormalizedCharge,
                cooldownRemaining,
                cooldownRemaining > 0f,
                waitingForDashRelease);
        }

        private void RechargeFromTurn(FlightDashSkillContext context, float dt)
        {
            if (baseTurnChargeRate <= 0f || typeChargeMultiplier <= 0f || chargeReferenceTurnRate <= 0f)
            {
                return;
            }

            float speed01 = Mathf.InverseLerp(chargeMinSpeed, chargeReferenceSpeed, context.CurrentSpeed);
            float turn01 = Mathf.Clamp01(context.ActualTurnRate / chargeReferenceTurnRate);
            float chargeRate = baseTurnChargeRate * typeChargeMultiplier * speed01 * turn01 * turn01;
            currentCharge = Mathf.Min(maxCharge, currentCharge + chargeRate * dt);
        }
    }

    public readonly struct FlightDashSkillInput
    {
        public readonly bool DashRequested;

        public FlightDashSkillInput(bool dashRequested)
        {
            DashRequested = dashRequested;
        }
    }

    public readonly struct FlightDashSkillContext
    {
        public readonly float DeltaTime;
        public readonly float CurrentSpeed;
        public readonly float ActualTurnRate;

        public FlightDashSkillContext(float deltaTime, float currentSpeed, float actualTurnRate)
        {
            DeltaTime = Mathf.Max(0f, deltaTime);
            CurrentSpeed = Mathf.Max(0f, currentSpeed);
            ActualTurnRate = Mathf.Max(0f, actualTurnRate);
        }
    }

    public readonly struct FlightDashSkillOutput
    {
        public readonly bool IsDashing;
        public readonly float DashAcceleration;
        public readonly float Charge;
        public readonly float NormalizedCharge;
        public readonly float CooldownRemaining;
        public readonly bool IsCoolingDown;
        public readonly bool RequiresDashRelease;

        public FlightDashSkillOutput(
            bool isDashing,
            float dashAcceleration,
            float charge,
            float normalizedCharge,
            float cooldownRemaining,
            bool isCoolingDown,
            bool requiresDashRelease)
        {
            IsDashing = isDashing;
            DashAcceleration = dashAcceleration;
            Charge = charge;
            NormalizedCharge = normalizedCharge;
            CooldownRemaining = cooldownRemaining;
            IsCoolingDown = isCoolingDown;
            RequiresDashRelease = requiresDashRelease;
        }
    }
}
