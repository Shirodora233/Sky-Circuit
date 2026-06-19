using System;
using UnityEngine;

namespace SkyCircuit.Profiles
{
    public enum CompetitorArchetype
    {
        Speeder,
        Fighter,
        AllRounder
    }

    [CreateAssetMenu(fileName = "SC_CompetitorProfile", menuName = "Sky Circuit/Competitor Profile")]
    public sealed class CompetitorProfile : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "All-Rounder";
        [SerializeField] private CompetitorArchetype archetype = CompetitorArchetype.AllRounder;

        [Header("Flight Speed")]
        [SerializeField] private FlightSpeedSettings speed = FlightSpeedSettings.AllRounder();

        [Header("Flight Steering")]
        [SerializeField] private FlightSteeringSettings steering = FlightSteeringSettings.AllRounder();

        [Header("Dash Skill")]
        [SerializeField] private DashSkillSettings dashSkill = DashSkillSettings.AllRounder();

        [Header("AI Pilot")]
        [SerializeField] private RouteAIPilotSettings aiPilot = RouteAIPilotSettings.AllRounder();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public CompetitorArchetype Archetype => archetype;
        public FlightSpeedSettings Speed => speed;
        public FlightSteeringSettings Steering => steering;
        public DashSkillSettings DashSkill => dashSkill;
        public RouteAIPilotSettings AiPilot => aiPilot;

        public void Configure(
            string profileName,
            CompetitorArchetype profileArchetype,
            FlightSpeedSettings speedSettings,
            FlightSteeringSettings steeringSettings,
            DashSkillSettings dashSkillSettings,
            RouteAIPilotSettings aiPilotSettings)
        {
            displayName = profileName;
            archetype = profileArchetype;
            speed = speedSettings.Validated();
            steering = steeringSettings.Validated();
            dashSkill = dashSkillSettings.Validated();
            aiPilot = aiPilotSettings.Validated();
        }

        private void OnValidate()
        {
            speed = speed.Validated();
            steering = steering.Validated();
            dashSkill = dashSkill.Validated();
            aiPilot = aiPilot.Validated();
        }
    }

    [Serializable]
    public struct FlightSpeedSettings
    {
        [Min(0f)] public float minSpeed;
        [Min(0f)] public float cruiseSpeed;
        [Min(0f)] public float poweredMaxSpeed;
        [Min(0f)] public float absoluteMaxSpeed;
        [Min(0f)] public float acceleration;
        [Min(0f)] public float deceleration;
        [Range(0f, 1f)] public float highSpeedAccelerationScale;
        [Min(0f)] public float overspeedReturnRate;
        [Min(0f)] public float velocitySharpness;

        [Min(0f)] public float verticalAssistSpeed;
        [Min(0f)] public float gravityEnergy;
        [Min(0f)] public float climbEfficiency;
        [Min(0f)] public float diveEfficiency;

        [Min(0f)] public float turnLossReferenceRate;
        [Min(0f)] public float turnSpeedLossRate;
        [Min(0f)] public float turnLossMinSpeed;

        public static FlightSpeedSettings Speeder()
        {
            return new FlightSpeedSettings
            {
                minSpeed = 0f,
                cruiseSpeed = 26f,
                poweredMaxSpeed = 48f,
                absoluteMaxSpeed = 66f,
                acceleration = 16f,
                deceleration = 24f,
                highSpeedAccelerationScale = 0.12f,
                overspeedReturnRate = 7f,
                velocitySharpness = 6.5f,
                verticalAssistSpeed = 14f,
                gravityEnergy = 9.8f,
                climbEfficiency = 1.05f,
                diveEfficiency = 0.9f,
                turnLossReferenceRate = 115f,
                turnSpeedLossRate = 0.26f,
                turnLossMinSpeed = 4f,
            };
        }

        public static FlightSpeedSettings Fighter()
        {
            return new FlightSpeedSettings
            {
                minSpeed = 0f,
                cruiseSpeed = 22f,
                poweredMaxSpeed = 36f,
                absoluteMaxSpeed = 48f,
                acceleration = 30f,
                deceleration = 38f,
                highSpeedAccelerationScale = 0.3f,
                overspeedReturnRate = 16f,
                velocitySharpness = 10f,
                verticalAssistSpeed = 21f,
                gravityEnergy = 9.8f,
                climbEfficiency = 0.72f,
                diveEfficiency = 0.65f,
                turnLossReferenceRate = 175f,
                turnSpeedLossRate = 0.11f,
                turnLossMinSpeed = 4f,
            };
        }

        public static FlightSpeedSettings AllRounder()
        {
            return new FlightSpeedSettings
            {
                minSpeed = 0f,
                cruiseSpeed = 24f,
                poweredMaxSpeed = 42f,
                absoluteMaxSpeed = 58f,
                acceleration = 22f,
                deceleration = 30f,
                highSpeedAccelerationScale = 0.18f,
                overspeedReturnRate = 10f,
                velocitySharpness = 8f,
                verticalAssistSpeed = 17f,
                gravityEnergy = 9.8f,
                climbEfficiency = 0.9f,
                diveEfficiency = 0.8f,
                turnLossReferenceRate = 140f,
                turnSpeedLossRate = 0.18f,
                turnLossMinSpeed = 4f,
            };
        }

        public FlightSpeedSettings Validated()
        {
            minSpeed = Mathf.Max(0f, minSpeed);
            cruiseSpeed = Mathf.Max(minSpeed, cruiseSpeed);
            poweredMaxSpeed = Mathf.Max(cruiseSpeed, poweredMaxSpeed);
            absoluteMaxSpeed = Mathf.Max(poweredMaxSpeed, absoluteMaxSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            highSpeedAccelerationScale = Mathf.Clamp01(highSpeedAccelerationScale);
            overspeedReturnRate = Mathf.Max(0f, overspeedReturnRate);
            velocitySharpness = Mathf.Max(0f, velocitySharpness);
            verticalAssistSpeed = Mathf.Max(0f, verticalAssistSpeed);
            gravityEnergy = Mathf.Max(0f, gravityEnergy);
            climbEfficiency = Mathf.Max(0f, climbEfficiency);
            diveEfficiency = Mathf.Max(0f, diveEfficiency);
            turnLossReferenceRate = Mathf.Max(0f, turnLossReferenceRate);
            turnSpeedLossRate = Mathf.Max(0f, turnSpeedLossRate);
            turnLossMinSpeed = Mathf.Max(minSpeed, turnLossMinSpeed);
            return this;
        }
    }

    [Serializable]
    public struct FlightSteeringSettings
    {
        [Min(0f)] public float mouseYawSensitivity;
        [Min(0f)] public float mousePitchSensitivity;
        [Min(0f)] public float keyboardYawRate;
        [Range(1f, 89f)] public float maxPitch;
        [Min(0f)] public float rotationSharpness;
        [Min(0f)] public float externalImpulseDecay;

        public static FlightSteeringSettings Speeder()
        {
            return new FlightSteeringSettings
            {
                mouseYawSensitivity = 0.2f,
                mousePitchSensitivity = 0.16f,
                keyboardYawRate = 88f,
                maxPitch = 50f,
                rotationSharpness = 18f,
                externalImpulseDecay = 3.6f,
            };
        }

        public static FlightSteeringSettings Fighter()
        {
            return new FlightSteeringSettings
            {
                mouseYawSensitivity = 0.24f,
                mousePitchSensitivity = 0.21f,
                keyboardYawRate = 150f,
                maxPitch = 66f,
                rotationSharpness = 38f,
                externalImpulseDecay = 5.2f,
            };
        }

        public static FlightSteeringSettings AllRounder()
        {
            return new FlightSteeringSettings
            {
                mouseYawSensitivity = 0.22f,
                mousePitchSensitivity = 0.18f,
                keyboardYawRate = 115f,
                maxPitch = 58f,
                rotationSharpness = 26f,
                externalImpulseDecay = 4f,
            };
        }

        public FlightSteeringSettings Validated()
        {
            mouseYawSensitivity = Mathf.Max(0f, mouseYawSensitivity);
            mousePitchSensitivity = Mathf.Max(0f, mousePitchSensitivity);
            keyboardYawRate = Mathf.Max(0f, keyboardYawRate);
            maxPitch = Mathf.Clamp(maxPitch, 1f, 89f);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            externalImpulseDecay = Mathf.Max(0f, externalImpulseDecay);
            return this;
        }
    }

    [Serializable]
    public struct DashSkillSettings
    {
        [Min(0f)] public float maxCharge;
        [Min(0f)] public float startingCharge;
        [Min(0f)] public float baseTurnChargeRate;
        [Min(0f)] public float typeChargeMultiplier;
        [Min(0f)] public float chargeMinSpeed;
        [Min(0f)] public float chargeReferenceSpeed;
        [Min(0f)] public float chargeReferenceTurnRate;
        [Min(0f)] public float dashDrainRate;
        [Min(0f)] public float dashAcceleration;
        [Min(0f)] public float cooldownDuration;

        public static DashSkillSettings Speeder()
        {
            return new DashSkillSettings
            {
                maxCharge = 100f,
                startingCharge = 30f,
                baseTurnChargeRate = 18f,
                typeChargeMultiplier = 0.55f,
                chargeMinSpeed = 10f,
                chargeReferenceSpeed = 48f,
                chargeReferenceTurnRate = 130f,
                dashDrainRate = 20f,
                dashAcceleration = 18f,
                cooldownDuration = 3f,
            };
        }

        public static DashSkillSettings Fighter()
        {
            return new DashSkillSettings
            {
                maxCharge = 100f,
                startingCharge = 45f,
                baseTurnChargeRate = 18f,
                typeChargeMultiplier = 1.45f,
                chargeMinSpeed = 6f,
                chargeReferenceSpeed = 36f,
                chargeReferenceTurnRate = 170f,
                dashDrainRate = 34f,
                dashAcceleration = 52f,
                cooldownDuration = 2f,
            };
        }

        public static DashSkillSettings AllRounder()
        {
            return new DashSkillSettings
            {
                maxCharge = 100f,
                startingCharge = 40f,
                baseTurnChargeRate = 18f,
                typeChargeMultiplier = 1f,
                chargeMinSpeed = 8f,
                chargeReferenceSpeed = 42f,
                chargeReferenceTurnRate = 150f,
                dashDrainRate = 26f,
                dashAcceleration = 34f,
                cooldownDuration = 2.5f,
            };
        }

        public DashSkillSettings Validated()
        {
            maxCharge = Mathf.Max(0f, maxCharge);
            startingCharge = Mathf.Clamp(startingCharge, 0f, maxCharge);
            baseTurnChargeRate = Mathf.Max(0f, baseTurnChargeRate);
            typeChargeMultiplier = Mathf.Max(0f, typeChargeMultiplier);
            chargeMinSpeed = Mathf.Max(0f, chargeMinSpeed);
            chargeReferenceSpeed = Mathf.Max(chargeMinSpeed + 0.01f, chargeReferenceSpeed);
            chargeReferenceTurnRate = Mathf.Max(0.01f, chargeReferenceTurnRate);
            dashDrainRate = Mathf.Max(0f, dashDrainRate);
            dashAcceleration = Mathf.Max(0f, dashAcceleration);
            cooldownDuration = Mathf.Max(0f, cooldownDuration);
            return this;
        }
    }

    [Serializable]
    public struct RouteAIPilotSettings
    {
        [Min(0f)] public float turnGain;
        [Min(0.01f)] public float verticalRange;
        [Min(0f)] public float routeSpeed;
        [Min(0f)] public float closeTargetSpeed;
        [Min(0f)] public float overshootSpeed;
        [Min(0f)] public float approachSlowRadius;
        [Min(0f)] public float overshootRecoveryRadius;
        [Min(0f)] public float speedTolerance;
        [Range(-1f, 0f)] public float brakeInput;
        [Min(0f)] public float dogfightEngageDistance;
        [Min(0f)] public float dogfightBehindOffset;
        public float dogfightVerticalOffset;
        public bool boostOnStraight;
        [Min(0f)] public float recoveryForwardDistance;
        [Min(0f)] public float recoverySideDistance;
        [Min(0f)] public float spinRecoveryThreshold;
        [Min(0f)] public float stuckRecoveryDuration;
        [Min(0f)] public float routePredictionStartRadius;
        [Min(0f)] public float routeExitLeadDistance;
        [Min(0f)] public float lookYawGain;
        [Min(0f)] public float lookPitchGain;
        [Min(0f)] public float maxLookDelta;
        [Min(0f)] public float dogfightMaxRouteDistance;
        [Min(0f)] public float dogfightMaxChaseTime;
        [Min(0f)] public float dogfightCooldownDuration;
        [Min(0f)] public float dogfightPredictionTime;
        [Range(-1f, 1f)] public float dogfightEntryBehindDot;
        [Range(-1f, 1f)] public float dogfightEntryFacingDot;

        public static RouteAIPilotSettings Speeder()
        {
            return new RouteAIPilotSettings
            {
                turnGain = 1.9f,
                verticalRange = 24f,
                routeSpeed = 42f,
                closeTargetSpeed = 16f,
                overshootSpeed = 8f,
                approachSlowRadius = 52f,
                overshootRecoveryRadius = 34f,
                speedTolerance = 3f,
                brakeInput = -0.75f,
                dogfightEngageDistance = 28f,
                dogfightBehindOffset = 4.2f,
                dogfightVerticalOffset = 0.25f,
                boostOnStraight = true,
                recoveryForwardDistance = 36f,
                recoverySideDistance = 16f,
                spinRecoveryThreshold = 1.6f,
                stuckRecoveryDuration = 1.8f,
                routePredictionStartRadius = 66f,
                routeExitLeadDistance = 24f,
                lookYawGain = 0.75f,
                lookPitchGain = 1f,
                maxLookDelta = 5f,
                dogfightMaxRouteDistance = 48f,
                dogfightMaxChaseTime = 3.2f,
                dogfightCooldownDuration = 5.5f,
                dogfightPredictionTime = 0.25f,
                dogfightEntryBehindDot = -0.45f,
                dogfightEntryFacingDot = 0.2f,
            };
        }

        public static RouteAIPilotSettings Fighter()
        {
            return new RouteAIPilotSettings
            {
                turnGain = 3.1f,
                verticalRange = 14f,
                routeSpeed = 28f,
                closeTargetSpeed = 10f,
                overshootSpeed = 4.5f,
                approachSlowRadius = 32f,
                overshootRecoveryRadius = 22f,
                speedTolerance = 1.6f,
                brakeInput = -0.95f,
                dogfightEngageDistance = 58f,
                dogfightBehindOffset = 2.6f,
                dogfightVerticalOffset = 0.7f,
                boostOnStraight = false,
                recoveryForwardDistance = 24f,
                recoverySideDistance = 22f,
                spinRecoveryThreshold = 1.5f,
                stuckRecoveryDuration = 2f,
                routePredictionStartRadius = 42f,
                routeExitLeadDistance = 12f,
                lookYawGain = 1.1f,
                lookPitchGain = 1.35f,
                maxLookDelta = 7f,
                dogfightMaxRouteDistance = 78f,
                dogfightMaxChaseTime = 7f,
                dogfightCooldownDuration = 2f,
                dogfightPredictionTime = 0.45f,
                dogfightEntryBehindDot = 0.1f,
                dogfightEntryFacingDot = -0.25f,
            };
        }

        public static RouteAIPilotSettings AllRounder()
        {
            return new RouteAIPilotSettings
            {
                turnGain = 2.4f,
                verticalRange = 18f,
                routeSpeed = 32f,
                closeTargetSpeed = 12f,
                overshootSpeed = 6f,
                approachSlowRadius = 42f,
                overshootRecoveryRadius = 28f,
                speedTolerance = 2f,
                brakeInput = -0.85f,
                dogfightEngageDistance = 45f,
                dogfightBehindOffset = 3f,
                dogfightVerticalOffset = 0.5f,
                boostOnStraight = false,
                recoveryForwardDistance = 30f,
                recoverySideDistance = 18f,
                spinRecoveryThreshold = 1.8f,
                stuckRecoveryDuration = 2.2f,
                routePredictionStartRadius = 54f,
                routeExitLeadDistance = 16f,
                lookYawGain = 0.85f,
                lookPitchGain = 1.2f,
                maxLookDelta = 6f,
                dogfightMaxRouteDistance = 64f,
                dogfightMaxChaseTime = 5.5f,
                dogfightCooldownDuration = 3.5f,
                dogfightPredictionTime = 0.35f,
                dogfightEntryBehindDot = -0.18f,
                dogfightEntryFacingDot = -0.05f,
            };
        }

        public RouteAIPilotSettings Validated()
        {
            turnGain = Mathf.Max(0f, turnGain);
            verticalRange = Mathf.Max(0.01f, verticalRange);
            routeSpeed = Mathf.Max(0f, routeSpeed);
            closeTargetSpeed = Mathf.Max(0f, closeTargetSpeed);
            overshootSpeed = Mathf.Max(0f, overshootSpeed);
            approachSlowRadius = Mathf.Max(0f, approachSlowRadius);
            overshootRecoveryRadius = Mathf.Max(0f, overshootRecoveryRadius);
            speedTolerance = Mathf.Max(0f, speedTolerance);
            brakeInput = Mathf.Clamp(brakeInput, -1f, 0f);
            dogfightEngageDistance = Mathf.Max(0f, dogfightEngageDistance);
            dogfightBehindOffset = Mathf.Max(0f, dogfightBehindOffset);
            recoveryForwardDistance = recoveryForwardDistance > 0f ? recoveryForwardDistance : 30f;
            recoverySideDistance = recoverySideDistance > 0f ? recoverySideDistance : 18f;
            spinRecoveryThreshold = spinRecoveryThreshold > 0f ? spinRecoveryThreshold : 1.8f;
            stuckRecoveryDuration = stuckRecoveryDuration > 0f ? stuckRecoveryDuration : 2.2f;
            routePredictionStartRadius = routePredictionStartRadius > 0f ? routePredictionStartRadius : 54f;
            routeExitLeadDistance = routeExitLeadDistance > 0f ? routeExitLeadDistance : 16f;
            lookYawGain = lookYawGain > 0f ? lookYawGain : 0.85f;
            lookPitchGain = lookPitchGain > 0f ? lookPitchGain : 1.2f;
            maxLookDelta = maxLookDelta > 0f ? maxLookDelta : 6f;
            dogfightMaxRouteDistance = dogfightMaxRouteDistance > 0f ? dogfightMaxRouteDistance : 64f;
            dogfightMaxChaseTime = dogfightMaxChaseTime > 0f ? dogfightMaxChaseTime : 5.5f;
            dogfightCooldownDuration = dogfightCooldownDuration > 0f ? dogfightCooldownDuration : 3.5f;
            dogfightPredictionTime = dogfightPredictionTime > 0f ? dogfightPredictionTime : 0.35f;
            dogfightEntryBehindDot = Mathf.Clamp(dogfightEntryBehindDot, -1f, 1f);
            dogfightEntryFacingDot = Mathf.Clamp(dogfightEntryFacingDot, -1f, 1f);
            return this;
        }
    }
}
