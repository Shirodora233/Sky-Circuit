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

        private float lastTurnDirection = 1f;
        private CompetitorProfile appliedProfile;
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
            }

            Vector3 toTarget = targetPosition - body.position;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                controller.SetInput(FlightInputState.Neutral);
                ResetTelemetry("AtTarget");
                return;
            }

            Vector3 localTarget = body.InverseTransformDirection(toTarget / distance);
            float turn = CalculateTurn(localTarget);
            float throttle = CalculateThrottle(distance, localTarget, turn);

            float vertical = Mathf.Clamp(toTarget.y / verticalRange, -1f, 1f);
            bool boost = boostOnStraight && throttle > 0f && Mathf.Abs(turn) < 0.2f && Mathf.Abs(vertical) < 0.2f;
            UpdateTelemetry(targetingDogfight, targetPosition, distance, localTarget, throttle, turn, vertical);
            controller.SetInput(new FlightInputState(throttle, turn, vertical, Vector2.zero, boost));
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
            float vertical)
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

            if (debugStuckTime > 0f)
            {
                debugState = AiPilotState.StuckRecovery;
            }
            else if (targetingDogfight)
            {
                debugState = AiPilotState.DogfightAttack;
            }
            else if (overshooting)
            {
                debugState = AiPilotState.OvershootRecovery;
            }
            else if (distance <= approachSlowRadius)
            {
                debugState = AiPilotState.BuoyApproach;
            }
            else
            {
                debugState = AiPilotState.RouteCruise;
            }
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
            float turn = Mathf.Clamp(localTarget.x * turnGain, -1f, 1f);
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

        private float CalculateThrottle(float distance, Vector3 localTarget, float turn)
        {
            float touchRadius = route != null ? route.TouchRadius : 6f;
            float slowRadius = Mathf.Max(approachSlowRadius, touchRadius * 4f);
            float radiusBand = Mathf.Max(0.01f, slowRadius - touchRadius);
            float approach01 = 1f - Mathf.Clamp01((distance - touchRadius) / radiusBand);
            float turn01 = Mathf.Abs(turn);

            float desiredSpeed = Mathf.Lerp(routeSpeed, closeTargetSpeed, approach01);
            desiredSpeed = Mathf.Lerp(desiredSpeed, closeTargetSpeed, turn01 * turn01);
            if (localTarget.z < 0f && distance < overshootRecoveryRadius)
            {
                desiredSpeed = overshootSpeed;
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
