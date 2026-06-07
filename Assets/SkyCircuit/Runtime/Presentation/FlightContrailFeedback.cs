using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Presentation
{
    public sealed class FlightContrailFeedback : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private float minWidth = 0.08f;
        [SerializeField] private float maxWidth = 0.45f;
        [SerializeField] private Color cruiseColor = new Color(0.25f, 0.85f, 1f, 1f);
        [SerializeField] private Color boostColor = new Color(1f, 0.95f, 0.35f, 1f);

        public void Configure(SkyCircuitFlightController flightController, TrailRenderer targetTrail)
        {
            controller = flightController;
            trail = targetTrail;
        }

        private void Awake()
        {
            if (trail == null)
            {
                trail = GetComponentInChildren<TrailRenderer>();
            }

            if (controller == null)
            {
                controller = GetComponentInParent<SkyCircuitFlightController>();
            }
        }

        private void Update()
        {
            if (controller == null || trail == null)
            {
                return;
            }

            float speed = controller.NormalizedSpeed;
            trail.emitting = controller.CurrentSpeed > 1f;
            trail.widthMultiplier = Mathf.Lerp(minWidth, maxWidth, speed);
            trail.time = Mathf.Lerp(0.35f, 0.9f, speed);

            Color color = Color.Lerp(cruiseColor, boostColor, controller.IsBoosting ? 1f : Mathf.Clamp01(speed - 0.65f));
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
