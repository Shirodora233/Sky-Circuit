using System;
using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Presentation
{
    public sealed class FlightContrailFeedback : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private TrailRenderer[] trails = Array.Empty<TrailRenderer>();
        [SerializeField] private float minWidth = 0.055f;
        [SerializeField] private float maxWidth = 0.28f;
        [SerializeField] private float minTrailTime = 0.45f;
        [SerializeField] private float maxTrailTime = 1.05f;
        [SerializeField, Range(0.05f, 1f)] private float nearCharacterWidthScale = 0.18f;
        [SerializeField] private Color cruiseColor = new Color(0.85f, 0.38f, 1f, 0.95f);
        [SerializeField] private Color boostColor = new Color(1f, 0.95f, 0.55f, 1f);

        public void Configure(SkyCircuitFlightController flightController, params TrailRenderer[] targetTrails)
        {
            controller = flightController;
            trails = targetTrails ?? Array.Empty<TrailRenderer>();
            trail = trails.Length > 0 ? trails[0] : null;
            ApplyStaticTrailShape();
        }

        private void Awake()
        {
            RefreshTrailReferences();

            if (controller == null)
            {
                controller = GetComponentInParent<SkyCircuitFlightController>();
            }

            ApplyStaticTrailShape();
        }

        private void OnValidate()
        {
            RefreshTrailReferences();
            ApplyStaticTrailShape();
        }

        private void RefreshTrailReferences()
        {
            if (trail == null)
            {
                trail = GetComponentInChildren<TrailRenderer>();
            }

            TrailRenderer[] childTrails = GetComponentsInChildren<TrailRenderer>();
            if (trails == null || trails.Length == 0 || (childTrails.Length > trails.Length && trail != null))
            {
                trails = childTrails;
                if ((trails == null || trails.Length == 0) && trail != null)
                {
                    trails = new[] { trail };
                }
            }
        }

        private void Update()
        {
            if (controller == null || trails == null || trails.Length == 0)
            {
                return;
            }

            float speed = controller.NormalizedSpeed;
            bool emitting = controller.CurrentSpeed > 1f;
            float width = Mathf.Lerp(minWidth, maxWidth, speed);
            float duration = Mathf.Lerp(minTrailTime, maxTrailTime, speed);
            Color color = Color.Lerp(cruiseColor, boostColor, controller.IsBoosting ? 1f : Mathf.Clamp01(speed - 0.65f));

            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer targetTrail = trails[i];
                if (targetTrail == null)
                {
                    continue;
                }

                targetTrail.emitting = emitting;
                targetTrail.widthMultiplier = width;
                targetTrail.time = duration;
                targetTrail.startColor = color;
                targetTrail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        private void ApplyStaticTrailShape()
        {
            if (trails == null)
            {
                return;
            }

            AnimationCurve widthCurve = new AnimationCurve(
                new Keyframe(0f, Mathf.Clamp01(nearCharacterWidthScale)),
                new Keyframe(0.22f, 0.72f),
                new Keyframe(1f, 1f));

            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer targetTrail = trails[i];
                if (targetTrail == null)
                {
                    continue;
                }

                targetTrail.alignment = LineAlignment.View;
                targetTrail.autodestruct = false;
                targetTrail.minVertexDistance = 0.12f;
                targetTrail.numCornerVertices = 2;
                targetTrail.numCapVertices = 2;
                targetTrail.textureMode = LineTextureMode.Stretch;
                targetTrail.widthCurve = widthCurve;
            }
        }
    }
}
