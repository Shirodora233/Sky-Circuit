using System;
using System.Collections.Generic;
using SkyCircuit.Flight;
using UnityEngine;
using VRM;

namespace SkyCircuit.Presentation
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10950)]
    public sealed class CharacterFlightVisualWind : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Rigidbody sourceBody;

        [Header("Wind")]
        [SerializeField] private float minWindSpeed = 4f;
        [SerializeField] private float referenceWindSpeed = 58f;
        [SerializeField] private float windForce = 1.15f;
        [SerializeField] private float verticalLift = 0.18f;
        [SerializeField] private float turnForce = 0.35f;
        [SerializeField] private float turnRateForFullEffect = 140f;
        [SerializeField] private float smoothing = 10f;

        [Header("Targets")]
        [SerializeField] private float hairMultiplier = 0.7f;
        [SerializeField] private float skirtMultiplier = 1.15f;
        [SerializeField] private float sleeveMultiplier = 0.8f;
        [SerializeField] private float ribbonMultiplier = 1f;

        [Header("Spring Tuning")]
        [SerializeField] private float highSpeedDragAdd = 0.08f;
        [SerializeField] private float highSpeedStiffnessScale = 1.08f;

        private readonly List<SpringTarget> targets = new();
        private Quaternion previousRotation;
        private Vector3 previousForward;
        private Vector3 smoothedForce;
        private bool initialized;

        public void Configure(SkyCircuitFlightController flightController, Rigidbody body)
        {
            controller = flightController;
            sourceBody = body;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheSpringTargets();
            CaptureRotationState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheSpringTargets();
            CaptureRotationState();
        }

        private void LateUpdate()
        {
            ResolveReferences();

            if (targets.Count == 0)
            {
                CacheSpringTargets();
            }

            if (sourceBody == null || targets.Count == 0)
            {
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            if (!initialized)
            {
                CaptureRotationState();
            }

            float speed = GetWindSpeed();
            float speed01 = Mathf.InverseLerp(minWindSpeed, Mathf.Max(minWindSpeed + 0.01f, referenceWindSpeed), speed);
            Vector3 force = BuildWindForce(dt, speed01);
            smoothedForce = Vector3.Lerp(smoothedForce, force, DampBlend(smoothing, dt));

            for (int i = 0; i < targets.Count; i++)
            {
                ApplyTarget(targets[i], smoothedForce, speed01);
            }
        }

        private void OnDisable()
        {
            RestoreTargets();
            smoothedForce = Vector3.zero;
            initialized = false;
        }

        private void ResolveReferences()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<SkyCircuitFlightController>() ?? GetComponentInChildren<SkyCircuitFlightController>();
            }

            if (sourceBody == null)
            {
                sourceBody = controller != null ? controller.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();
            }
        }

        private void CacheSpringTargets()
        {
            RestoreTargets();
            targets.Clear();

            foreach (VRMSpringBone spring in GetComponentsInChildren<VRMSpringBone>(true))
            {
                if (!TryGetKind(spring, out SpringTargetKind kind))
                {
                    continue;
                }

                targets.Add(new SpringTarget(spring, kind));
            }
        }

        private void CaptureRotationState()
        {
            Quaternion rotation = sourceBody != null ? sourceBody.rotation : transform.rotation;
            previousRotation = rotation;
            previousForward = rotation * Vector3.forward;
            initialized = true;
        }

        private float GetWindSpeed()
        {
            float bodySpeed = sourceBody != null ? sourceBody.linearVelocity.magnitude : 0f;
            float controllerSpeed = controller != null ? controller.CurrentSpeed : 0f;
            return Mathf.Max(bodySpeed, controllerSpeed);
        }

        private Vector3 BuildWindForce(float dt, float speed01)
        {
            Quaternion currentRotation = sourceBody.rotation;
            Vector3 velocity = sourceBody.linearVelocity;
            Vector3 apparentWind = velocity.sqrMagnitude > 0.01f
                ? -velocity.normalized
                : -(currentRotation * Vector3.forward);

            Vector3 currentForward = currentRotation * Vector3.forward;
            Vector3 currentRight = currentRotation * Vector3.right;
            Vector3 currentUp = currentRotation * Vector3.up;

            float turnRate = Quaternion.Angle(previousRotation, currentRotation) / dt;
            float turn01 = Mathf.Clamp01(turnRate / Mathf.Max(1f, turnRateForFullEffect));
            float turnSign = Mathf.Sign(Vector3.Dot(Vector3.Cross(previousForward, currentForward), currentUp));

            previousRotation = currentRotation;
            previousForward = currentForward;

            Vector3 wind = apparentWind * (windForce * speed01);
            wind += Vector3.up * (verticalLift * speed01);
            wind += -currentRight * (turnSign * turnForce * turn01 * speed01);
            return wind;
        }

        private void ApplyTarget(SpringTarget target, Vector3 force, float speed01)
        {
            float multiplier = GetMultiplier(target.Kind);
            target.Bone.ExternalForce = force * multiplier;
            target.Bone.m_dragForce = Mathf.Clamp01(target.BaseDrag + highSpeedDragAdd * speed01 * multiplier);
            target.Bone.m_stiffnessForce = target.BaseStiffness * Mathf.Lerp(1f, highSpeedStiffnessScale, speed01);
        }

        private void RestoreTargets()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                SpringTarget target = targets[i];
                if (target.Bone == null)
                {
                    continue;
                }

                target.Bone.ExternalForce = target.BaseExternalForce;
                target.Bone.m_dragForce = target.BaseDrag;
                target.Bone.m_stiffnessForce = target.BaseStiffness;
            }
        }

        private float GetMultiplier(SpringTargetKind kind)
        {
            return kind switch
            {
                SpringTargetKind.Hair => hairMultiplier,
                SpringTargetKind.Skirt => skirtMultiplier,
                SpringTargetKind.Sleeve => sleeveMultiplier,
                SpringTargetKind.Ribbon => ribbonMultiplier,
                _ => 1f
            };
        }

        private static bool TryGetKind(VRMSpringBone spring, out SpringTargetKind kind)
        {
            string comment = spring.m_comment ?? string.Empty;
            if (comment.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = SpringTargetKind.Hair;
                return true;
            }

            if (comment.IndexOf("Skirt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = SpringTargetKind.Skirt;
                return true;
            }

            if (comment.IndexOf("Sleeve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                comment.IndexOf("Tops", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = SpringTargetKind.Sleeve;
                return true;
            }

            if (comment.IndexOf("Ribbon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                comment.IndexOf("Ribon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = SpringTargetKind.Ribbon;
                return true;
            }

            kind = SpringTargetKind.Other;
            return false;
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * dt);
        }

        private enum SpringTargetKind
        {
            Other,
            Hair,
            Skirt,
            Sleeve,
            Ribbon
        }

        private readonly struct SpringTarget
        {
            public readonly VRMSpringBone Bone;
            public readonly SpringTargetKind Kind;
            public readonly float BaseDrag;
            public readonly float BaseStiffness;
            public readonly Vector3 BaseExternalForce;

            public SpringTarget(VRMSpringBone bone, SpringTargetKind kind)
            {
                Bone = bone;
                Kind = kind;
                BaseDrag = bone.m_dragForce;
                BaseStiffness = bone.m_stiffnessForce;
                BaseExternalForce = bone.ExternalForce;
            }
        }
    }
}
