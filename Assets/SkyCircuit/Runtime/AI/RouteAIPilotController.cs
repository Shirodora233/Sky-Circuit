using SkyCircuit.Flight;
using SkyCircuit.Match;
using UnityEngine;

namespace SkyCircuit.AI
{
    [RequireComponent(typeof(SkyCircuitFlightController))]
    public sealed class RouteAIPilotController : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Competitor competitor;
        [SerializeField] private BuoyRoute route;
        [SerializeField] private MatchController match;
        [SerializeField] private float turnGain = 2.4f;
        [SerializeField] private float verticalRange = 18f;
        [SerializeField] private float routeSpeed = 32f;
        [SerializeField] private float closeTargetSpeed = 12f;
        [SerializeField] private float overshootSpeed = 6f;
        [SerializeField] private float approachSlowRadius = 42f;
        [SerializeField] private float overshootRecoveryRadius = 28f;
        [SerializeField] private float speedTolerance = 2f;
        [SerializeField] private float brakeInput = -0.85f;
        [SerializeField] private bool boostOnStraight = false;

        private float lastTurnDirection = 1f;

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
        }

        private void Update()
        {
            if (controller == null)
            {
                return;
            }

            if (match == null || match.Phase != MatchPhase.Running)
            {
                controller.SetInput(new FlightInputState(0f, 0f, 0f, Vector2.zero, false));
                return;
            }

            Transform target = route != null ? route.GetTarget(competitor) : null;
            Transform body = competitor != null ? competitor.Body : transform;
            if (target == null || body == null)
            {
                controller.SetInput(new FlightInputState(0f, 0f, 0f, Vector2.zero, false));
                return;
            }

            Vector3 toTarget = target.position - body.position;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                controller.SetInput(new FlightInputState(0f, 0f, 0f, Vector2.zero, false));
                return;
            }

            Vector3 localTarget = body.InverseTransformDirection(toTarget / distance);
            float turn = CalculateTurn(localTarget);
            float throttle = CalculateThrottle(distance, localTarget, turn);

            float vertical = Mathf.Clamp(toTarget.y / verticalRange, -1f, 1f);
            bool boost = boostOnStraight && throttle > 0f && Mathf.Abs(turn) < 0.2f && Mathf.Abs(vertical) < 0.2f;
            controller.SetInput(new FlightInputState(throttle, turn, vertical, Vector2.zero, boost));
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
