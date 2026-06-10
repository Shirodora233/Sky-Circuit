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
        [SerializeField] private bool boostOnStraight = false;

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
            Vector3 localTarget = body.InverseTransformDirection(toTarget.normalized);

            float turn = Mathf.Clamp(localTarget.x * turnGain, -1f, 1f);
            if (localTarget.z < 0f && Mathf.Abs(turn) < 0.2f)
            {
                turn = 1f;
            }

            float vertical = Mathf.Clamp(toTarget.y / verticalRange, -1f, 1f);
            bool boost = boostOnStraight && Mathf.Abs(turn) < 0.2f && Mathf.Abs(vertical) < 0.2f;
            controller.SetInput(new FlightInputState(1f, turn, vertical, Vector2.zero, boost));
        }
    }
}
