using SkyCircuit.Combat;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Networking;
using SkyCircuit.Profiles;
using UnityEngine;

namespace SkyCircuit.AI
{
    public enum AiPilotState
    {
        Idle,
        RouteCruise,
        BuoyApproach,
        OvershootRecovery,
        DogfightAttack,
        StuckRecovery
    }

    [RequireComponent(typeof(SkyCircuitFlightController))]
    public sealed class RouteAIPilotController : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Competitor competitor;
        [SerializeField] private Competitor dogfightOpponent;
        [SerializeField] private BuoyRoute route;
        [SerializeField] private MatchController match;
        [SerializeField] private LanRaceSessionController lanSession;
        [SerializeField] private DogfightController dogfight;
        [SerializeField] private float turnGain = 2.4f;
        [SerializeField] private float verticalRange = 18f;
        [SerializeField] private float routeSpeed = 32f;
        [SerializeField] private float closeTargetSpeed = 12f;
        [SerializeField] private float overshootSpeed = 6f;
        [SerializeField] private float approachSlowRadius = 42f;
        [SerializeField] private float overshootRecoveryRadius = 28f;
        [SerializeField] private float speedTolerance = 2f;
        [SerializeField] private float brakeInput = -0.85f;
        [SerializeField] private float dogfightEngageDistance = 45f;
        [SerializeField] private float dogfightBehindOffset = 3f;
        [SerializeField] private float dogfightVerticalOffset = 0.5f;
        [SerializeField] private bool boostOnStraight = false;
        [SerializeField] private float recoveryForwardDistance = 30f;
        [SerializeField] private float recoverySideDistance = 18f;
        [SerializeField] private float spinRecoveryThreshold = 1.8f;
        [SerializeField] private float stuckRecoveryDuration = 2.2f;
        [SerializeField] private float routePredictionStartRadius = 54f;
        [SerializeField] private float routeExitLeadDistance = 16f;
        [SerializeField] private float lookYawGain = 0.85f;
        [SerializeField] private float lookPitchGain = 1.2f;
        [SerializeField] private float maxLookDelta = 6f;

        private float lastTurnDirection = 1f;
        private CompetitorProfile appliedProfile;
        private float recoveryRemaining;
        private float recoveryTurnDirection = 1f;
        private AiPilotState debugState = AiPilotState.Idle;
        private string debugTargetKind = "None";
        private Vector3 debugTargetPosition;
        private float debugTargetDistance;
        private float debugSpeed;
        private float debugThrottleInput;
        private float debugTurnInput;
        private float debugVerticalInput;
        private float debugSpinTime;
        private float debugStuckTime;
        private float debugNoProgressTime;
        private float debugBestTargetDistance = float.PositiveInfinity;
        private int debugTrackedTargetIndex = -1;
        private int debugOvershootCount;
        private bool debugWasOvershooting;

        public AiPilotState DebugState => debugState;
        public string DebugTargetKind => debugTargetKind;
        public Vector3 DebugTargetPosition => debugTargetPosition;
        public float DebugTargetDistance => debugTargetDistance;
        public float DebugSpeed => debugSpeed;
        public float DebugThrottleInput => debugThrottleInput;
        public float DebugTurnInput => debugTurnInput;
        public float DebugVerticalInput => debugVerticalInput;
        public float DebugSpinTime => debugSpinTime;
        public float DebugStuckTime => debugStuckTime;
        public int DebugOvershootCount => debugOvershootCount;
        public string DebugTelemetrySummary =>
            $"state={debugState} target={debugTargetKind} distance={debugTargetDistance:0.0} " +
            $"speed={debugSpeed:0.0} throttle={debugThrottleInput:0.00} turn={debugTurnInput:0.00} " +
            $"vertical={debugVerticalInput:0.00} spin={debugSpinTime:0.0} stuck={debugStuckTime:0.0} " +
            $"overshoots={debugOvershootCount}";

        public void Configure(
            SkyCircuitFlightController flightController,
            Competitor aiCompetitor,
            BuoyRoute buoyRoute,
            MatchController matchController)
        {
            controller = flightController;
            competitor = aiCompetitor;
            route = buoyRoute;
            match = matchController;
            lanSession = null;
            ApplyProfile(competitor != null ? competitor.Profile : null);
        }

        public void Configure(
            SkyCircuitFlightController flightController,
            Competitor aiCompetitor,
            BuoyRoute buoyRoute,
            LanRaceSessionController raceSession)
        {
            controller = flightController;
            competitor = aiCompetitor;
            route = buoyRoute;
            match = null;
            lanSession = raceSession;
            ApplyProfile(competitor != null ? competitor.Profile : null);
        }

        public void ConfigureDogfight(Competitor opponent, DogfightController dogfightController)
        {
            dogfightOpponent = opponent;
            dogfight = dogfightController;
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<SkyCircuitFlightController>();
            }

            if (competitor == null)
            {
                competitor = GetComponent<Competitor>();
            }

            ApplyProfile(competitor != null ? competitor.Profile : null);
        }

        public void ApplyProfile(CompetitorProfile profile)
        {
            if (profile == null || appliedProfile == profile)
            {
                return;
            }

            RouteAIPilotSettings settings = profile.AiPilot.Validated();
            turnGain = settings.turnGain;
            verticalRange = settings.verticalRange;
            routeSpeed = settings.routeSpeed;
            closeTargetSpeed = settings.closeTargetSpeed;
            overshootSpeed = settings.overshootSpeed;
            approachSlowRadius = settings.approachSlowRadius;
            overshootRecoveryRadius = settings.overshootRecoveryRadius;
            speedTolerance = settings.speedTolerance;
            brakeInput = settings.brakeInput;
            dogfightEngageDistance = settings.dogfightEngageDistance;
            dogfightBehindOffset = settings.dogfightBehindOffset;
            dogfightVerticalOffset = settings.dogfightVerticalOffset;
            boostOnStraight = settings.boostOnStraight;
            appliedProfile = profile;
        }

        private void Update()
        {
            ApplyProfile(competitor != null ? competitor.Profile : null);

            if (controller == null)
            {
                ResetTelemetry("NoController");
                return;
            }

            if (!IsRaceRunning())
            {
                controller.SetInput(FlightInputState.Neutral);
                ResetTelemetry("NotRunning");
                return;
            }

            Transform body = competitor != null ? competitor.Body : transform;
            if (body == null)
            {
                controller.SetInput(FlightInputState.Neutral);
                ResetTelemetry("NoBody");
                return;
            }

            bool targetingDogfight = TryGetDogfightTarget(body, out Vector3 targetPosition);
            Vector3 preferredSteeringTarget = targetPosition;
            if (!targetingDogfight)
            {
                Transform routeTarget = route != null ? route.GetTarget(competitor) : null;
                if (routeTarget == null)
                {
                    controller.SetInput(FlightInputState.Neutral);
                    ResetTelemetry("NoTarget");
                    return;
                }

                targetPosition = routeTarget.position;
                preferredSteeringTarget = ResolveRoutePredictionTarget(body, targetPosition);
            }
            else
            {
                preferredSteeringTarget = targetPosition;
            }

            Vector3 rawToTarget = targetPosition - body.position;
            float rawDistance = rawToTarget.magnitude;
            if (rawDistance <= Mathf.Epsilon)
            {
                controller.SetInput(FlightInputState.Neutral);
                ResetTelemetry("AtTarget");
                return;
            }

            Vector3 rawLocalTarget = body.InverseTransformDirection(rawToTarget / rawDistance);
            AiPilotState state = SelectPilotState(targetingDogfight, rawDistance, rawLocalTarget);
            Vector3 steeringTarget = ResolveSteeringTarget(body, preferredSteeringTarget, rawLocalTarget, state);
            Vector3 toTarget = steeringTarget - body.position;
            float steeringDistance = toTarget.magnitude;
            if (steeringDistance <= Mathf.Epsilon)
            {
                controller.SetInput(FlightInputState.Neutral);
                ResetTelemetry("AtSteeringTarget");
                return;
            }

            Vector3 localTarget = body.InverseTransformDirection(toTarget / steeringDistance);
            float turn = CalculateTurn(localTarget);
            float throttle = CalculateThrottle(rawDistance, localTarget, turn, state);

            float vertical = Mathf.Clamp(toTarget.y / verticalRange, -1f, 1f);
            Vector2 lookDelta = BuildLookDelta(localTarget);
            bool boost = boostOnStraight
                && state == AiPilotState.RouteCruise
                && throttle > 0f
                && Mathf.Abs(turn) < 0.2f
                && Mathf.Abs(vertical) < 0.2f;
            UpdateTelemetry(targetingDogfight, steeringTarget, rawDistance, rawLocalTarget, throttle, turn, vertical, state);
            controller.SetInput(new FlightInputState(throttle, turn, vertical, lookDelta, boost));
        }

        private void ResetTelemetry(string targetKind)
        {
            debugState = AiPilotState.Idle;
            debugTargetKind = targetKind;
            debugTargetPosition = Vector3.zero;
            debugTargetDistance = 0f;
            debugSpeed = controller != null ? controller.CurrentSpeed : 0f;
            debugThrottleInput = 0f;
            debugTurnInput = 0f;
            debugVerticalInput = 0f;
            debugSpinTime = 0f;
            debugStuckTime = 0f;
            debugNoProgressTime = 0f;
            debugBestTargetDistance = float.PositiveInfinity;
            debugTrackedTargetIndex = competitor != null ? competitor.TargetIndex : -1;
            debugWasOvershooting = false;
        }

        private void UpdateTelemetry(
            bool targetingDogfight,
            Vector3 targetPosition,
            float distance,
            Vector3 localTarget,
            float throttle,
            float turn,
            float vertical,
            AiPilotState selectedState)
        {
            debugTargetKind = targetingDogfight ? "Dogfight" : "Route";
            debugTargetPosition = targetPosition;
            debugTargetDistance = distance;
            debugSpeed = controller != null ? controller.CurrentSpeed : 0f;
            debugThrottleInput = throttle;
            debugTurnInput = turn;
            debugVerticalInput = vertical;

            bool overshooting = localTarget.z < -0.05f && distance < overshootRecoveryRadius;
            if (overshooting && !debugWasOvershooting)
            {
                debugOvershootCount++;
            }

            debugWasOvershooting = overshooting;

            int targetIndex = competitor != null ? competitor.TargetIndex : -1;
            if (targetIndex != debugTrackedTargetIndex)
            {
                debugTrackedTargetIndex = targetIndex;
                debugBestTargetDistance = distance;
                debugNoProgressTime = 0f;
                debugStuckTime = 0f;
                debugSpinTime = 0f;
            }
            else if (distance < debugBestTargetDistance - 0.5f)
            {
                debugBestTargetDistance = distance;
                debugNoProgressTime = 0f;
                debugStuckTime = 0f;
            }
            else
            {
                debugNoProgressTime += Time.deltaTime;
            }

            bool spinning = Mathf.Abs(turn) > 0.85f && debugSpeed < Mathf.Max(closeTargetSpeed + 5f, 10f);
            debugSpinTime = spinning ? debugSpinTime + Time.deltaTime : 0f;

            bool stuck = debugNoProgressTime > 3f && debugSpeed < Mathf.Max(closeTargetSpeed + 4f, 8f);
            debugStuckTime = stuck ? debugStuckTime + Time.deltaTime : 0f;

            debugState = selectedState;
        }

        private bool IsRaceRunning()
        {
            if (match != null)
            {
                return match.Phase == MatchPhase.Running;
            }

            if (lanSession != null)
            {
                return lanSession.Phase == LanRacePhase.Running;
            }

            return false;
        }

        private bool TryGetDogfightTarget(Transform body, out Vector3 targetPosition)
        {
            targetPosition = Vector3.zero;
            Transform opponentBody = dogfightOpponent != null ? dogfightOpponent.Body : null;
            if (dogfight == null || !dogfight.IsUnlocked || opponentBody == null)
            {
                return false;
            }

            float distanceToOpponent = Vector3.Distance(body.position, opponentBody.position);
            if (distanceToOpponent > dogfightEngageDistance)
            {
                return false;
            }

            targetPosition = opponentBody.position - opponentBody.forward * dogfightBehindOffset + Vector3.up * dogfightVerticalOffset;
            return true;
        }

        private float CalculateTurn(Vector3 localTarget)
        {
            float yawError = Mathf.Atan2(localTarget.x, Mathf.Max(0.05f, localTarget.z)) * Mathf.Rad2Deg;
            float turn = Mathf.Clamp((yawError / 60f) * turnGain, -1f, 1f);
            if (Mathf.Abs(turn) > 0.05f)
            {
                lastTurnDirection = Mathf.Sign(turn);
            }

            if (localTarget.z < 0f && Mathf.Abs(turn) < 0.2f)
            {
                turn = lastTurnDirection;
            }

            return turn;
        }

        private Vector2 BuildLookDelta(Vector3 localTarget)
        {
            float yawError = Mathf.Atan2(localTarget.x, Mathf.Max(0.05f, localTarget.z)) * Mathf.Rad2Deg;
            float pitchError = Mathf.Asin(Mathf.Clamp(localTarget.y, -1f, 1f)) * Mathf.Rad2Deg;
            float dt = Mathf.Max(0.001f, Time.deltaTime);
            float yawDelta = Mathf.Clamp(yawError * lookYawGain * dt, -maxLookDelta, maxLookDelta);
            float pitchDelta = Mathf.Clamp(pitchError * lookPitchGain * dt, -maxLookDelta, maxLookDelta);
            return new Vector2(yawDelta, pitchDelta);
        }

        private AiPilotState SelectPilotState(bool targetingDogfight, float distance, Vector3 localTarget)
        {
            recoveryRemaining = Mathf.Max(0f, recoveryRemaining - Time.deltaTime);
            if (targetingDogfight)
            {
                return AiPilotState.DogfightAttack;
            }

            if (recoveryRemaining > 0f)
            {
                return AiPilotState.StuckRecovery;
            }

            if (debugStuckTime > 0.2f || debugSpinTime > spinRecoveryThreshold)
            {
                BeginRecovery(localTarget);
                return AiPilotState.StuckRecovery;
            }

            if (localTarget.z < -0.05f && distance < overshootRecoveryRadius)
            {
                recoveryTurnDirection = ResolveTurnDirection(localTarget);
                return AiPilotState.OvershootRecovery;
            }

            return distance <= approachSlowRadius ? AiPilotState.BuoyApproach : AiPilotState.RouteCruise;
        }

        private Vector3 ResolveSteeringTarget(
            Transform body,
            Vector3 routeOrDogfightTarget,
            Vector3 localTarget,
            AiPilotState state)
        {
            if (state != AiPilotState.OvershootRecovery && state != AiPilotState.StuckRecovery)
            {
                return routeOrDogfightTarget;
            }

            recoveryTurnDirection = ResolveTurnDirection(localTarget);
            float verticalOffset = Mathf.Clamp(routeOrDogfightTarget.y - body.position.y, -verticalRange, verticalRange);
            return body.position
                + body.forward * Mathf.Max(1f, recoveryForwardDistance)
                + body.right * recoveryTurnDirection * Mathf.Max(0f, recoverySideDistance)
                + Vector3.up * verticalOffset;
        }

        private Vector3 ResolveRoutePredictionTarget(Transform body, Vector3 routeTargetPosition)
        {
            Transform nextTarget = route != null ? route.GetNextTarget(competitor) : null;
            if (nextTarget == null)
            {
                return routeTargetPosition;
            }

            Vector3 exitDirection = nextTarget.position - routeTargetPosition;
            if (exitDirection.sqrMagnitude <= 0.0001f)
            {
                return routeTargetPosition;
            }

            exitDirection.Normalize();
            float distance = Vector3.Distance(body.position, routeTargetPosition);
            float touchRadius = route != null ? route.TouchRadius : 6f;
            float predictionSpan = Mathf.Max(0.01f, routePredictionStartRadius - touchRadius);
            float prediction01 = 1f - Mathf.Clamp01((distance - touchRadius) / predictionSpan);
            return routeTargetPosition + exitDirection * routeExitLeadDistance * prediction01;
        }

        private void BeginRecovery(Vector3 localTarget)
        {
            recoveryRemaining = Mathf.Max(0.1f, stuckRecoveryDuration);
            recoveryTurnDirection = ResolveTurnDirection(localTarget);
            debugSpinTime = 0f;
            debugStuckTime = 0f;
            debugNoProgressTime = 0f;
        }

        private float ResolveTurnDirection(Vector3 localTarget)
        {
            if (Mathf.Abs(localTarget.x) > 0.08f)
            {
                return Mathf.Sign(localTarget.x);
            }

            return Mathf.Abs(lastTurnDirection) > 0.01f ? Mathf.Sign(lastTurnDirection) : 1f;
        }

        private float CalculateThrottle(float distance, Vector3 localTarget, float turn, AiPilotState state)
        {
            float touchRadius = route != null ? route.TouchRadius : 6f;
            float slowRadius = Mathf.Max(approachSlowRadius, touchRadius * 4f);
            float radiusBand = Mathf.Max(0.01f, slowRadius - touchRadius);
            float approach01 = 1f - Mathf.Clamp01((distance - touchRadius) / radiusBand);
            float turn01 = Mathf.Abs(turn);

            float desiredSpeed = Mathf.Lerp(routeSpeed, closeTargetSpeed, approach01);
            desiredSpeed = Mathf.Lerp(desiredSpeed, closeTargetSpeed, turn01 * turn01);
            if (state == AiPilotState.OvershootRecovery)
            {
                desiredSpeed = overshootSpeed;
            }
            else if (state == AiPilotState.StuckRecovery)
            {
                desiredSpeed = Mathf.Max(closeTargetSpeed, overshootSpeed, 10f);
            }

            float speed = controller != null ? controller.CurrentSpeed : desiredSpeed;
            if (speed > desiredSpeed + speedTolerance)
            {
                return brakeInput;
            }

            if (speed < desiredSpeed - speedTolerance)
            {
                return 1f;
            }

            return 0f;
        }
    }
}
