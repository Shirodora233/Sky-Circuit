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

        private bool useExternalVisualState;
        private float externalNormalizedSpeed;
        private bool externalEmitting;
        private bool externalBoosting;
        private static Material runtimeTrailMaterial;
        private static int runtimeTrailMaterialUserCount;
        private bool registeredRuntimeTrailMaterialUser;

        public int DebugTrailCount => trails != null ? trails.Length : 0;
        public bool DebugUseExternalVisualState => useExternalVisualState;
        public float DebugExternalNormalizedSpeed => externalNormalizedSpeed;
        public bool DebugExternalEmitting => externalEmitting;
        public bool DebugExternalBoosting => externalBoosting;
        public Color DebugCruiseColor => cruiseColor;
        public Color DebugFirstTrailStartColor => FirstTrail != null ? FirstTrail.startColor : Color.clear;
        public bool DebugFirstTrailEmitting => FirstTrail != null && FirstTrail.emitting;
        public string DebugFirstTrailMaterial => DescribeTrailMaterial(FirstTrail);

        public void Configure(SkyCircuitFlightController flightController, params TrailRenderer[] targetTrails)
        {
            controller = flightController;
            trails = targetTrails ?? Array.Empty<TrailRenderer>();
            trail = trails.Length > 0 ? trails[0] : null;
            ApplyStaticTrailShape();
        }

        public void ConfigureColors(Color cruise, Color boost)
        {
            cruiseColor = cruise;
            boostColor = boost;
            if (useExternalVisualState)
            {
                ApplyTrailState(externalNormalizedSpeed, externalEmitting, externalBoosting);
            }
        }

        public void SetExternalVisualState(float normalizedSpeed, bool emitting, bool boosting)
        {
            useExternalVisualState = true;
            externalNormalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            externalEmitting = emitting;
            externalBoosting = boosting;
            ApplyTrailState(externalNormalizedSpeed, externalEmitting, externalBoosting);
        }

        public void ClearExternalVisualState()
        {
            useExternalVisualState = false;
        }

        private void Awake()
        {
            RegisterRuntimeTrailMaterialUser();
            RefreshTrailReferences();

            if (controller == null)
            {
                controller = GetComponentInParent<SkyCircuitFlightController>();
            }

            ApplyStaticTrailShape();
        }

        private void OnDestroy()
        {
            UnregisterRuntimeTrailMaterialUser();
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
            if (useExternalVisualState)
            {
                ApplyTrailState(externalNormalizedSpeed, externalEmitting, externalBoosting);
                return;
            }

            if (controller == null || trails == null || trails.Length == 0)
            {
                return;
            }

            ApplyTrailState(
                controller.NormalizedSpeed,
                controller.CurrentSpeed > 1f,
                controller.IsBoosting || controller.IsDashing);
        }

        private void ApplyTrailState(float normalizedSpeed, bool emitting, bool boosting)
        {
            if (trails == null || trails.Length == 0)
            {
                return;
            }

            float speed = Mathf.Clamp01(normalizedSpeed);
            float width = Mathf.Lerp(minWidth, maxWidth, speed);
            float duration = Mathf.Lerp(minTrailTime, maxTrailTime, speed);
            float highlight = boosting ? 0.35f : Mathf.Clamp01(speed - 0.65f) * 0.18f;
            Color color = Color.Lerp(cruiseColor, boostColor, highlight);

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
                EnsureTrailMaterial(targetTrail);
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
                EnsureTrailMaterial(targetTrail);
            }
        }

        private static void EnsureTrailMaterial(TrailRenderer targetTrail)
        {
            if (targetTrail == null)
            {
                return;
            }

            if (targetTrail.sharedMaterial != null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return;
            }

            Material material = RuntimeTrailMaterial;
            if (material == null)
            {
                return;
            }

            targetTrail.sharedMaterial = material;
            targetTrail.Clear();
        }

        private static Material RuntimeTrailMaterial
        {
            get
            {
                if (runtimeTrailMaterial != null)
                {
                    return runtimeTrailMaterial;
                }

                Shader shader = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
                if (shader == null)
                {
                    return null;
                }

                runtimeTrailMaterial = new Material(shader)
                {
                    name = "SC_RuntimeContrailTrail",
                    color = Color.white,
                    hideFlags = HideFlags.HideAndDontSave
                };
                return runtimeTrailMaterial;
            }
        }

        private static string DescribeTrailMaterial(TrailRenderer targetTrail)
        {
            if (targetTrail == null)
            {
                return "trail=null";
            }

            Material material = targetTrail.sharedMaterial;
            if (material == null)
            {
                return "material=null";
            }

            string shaderName = material.shader != null ? material.shader.name : "shader=null";
            return $"{material.name}/{shaderName}";
        }

        private void RegisterRuntimeTrailMaterialUser()
        {
            if (!Application.isPlaying || registeredRuntimeTrailMaterialUser)
            {
                return;
            }

            registeredRuntimeTrailMaterialUser = true;
            runtimeTrailMaterialUserCount++;
        }

        private void UnregisterRuntimeTrailMaterialUser()
        {
            if (!registeredRuntimeTrailMaterialUser)
            {
                return;
            }

            registeredRuntimeTrailMaterialUser = false;
            runtimeTrailMaterialUserCount = Mathf.Max(0, runtimeTrailMaterialUserCount - 1);
            if (runtimeTrailMaterialUserCount == 0)
            {
                DestroyRuntimeTrailMaterial();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeTrailMaterial()
        {
            DestroyRuntimeTrailMaterial();
            runtimeTrailMaterialUserCount = 0;
        }

        private static void DestroyRuntimeTrailMaterial()
        {
            if (runtimeTrailMaterial == null)
            {
                return;
            }

            Material material = runtimeTrailMaterial;
            runtimeTrailMaterial = null;
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        private TrailRenderer FirstTrail
        {
            get
            {
                if (trail != null)
                {
                    return trail;
                }

                if (trails == null)
                {
                    return null;
                }

                for (int i = 0; i < trails.Length; i++)
                {
                    if (trails[i] != null)
                    {
                        return trails[i];
                    }
                }

                return null;
            }
        }
    }
}
