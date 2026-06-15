using System.Collections.Generic;
using SkyCircuit.Flight;
using UnityEngine;
using UnityEngine.Serialization;

namespace SkyCircuit.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10850)]
    public sealed class CharacterFlightPoseController : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Rigidbody sourceBody;
        [SerializeField] private Animator animator;

        [Header("Speed Response")]
        [SerializeField] private float minPoseSpeed = 8f;
        [SerializeField] private float fullPoseSpeed = 48f;
        [SerializeField] private float basePoseWeight = 0.18f;
        [SerializeField] private float poseSmoothing = 9f;
        [SerializeField, Range(-1f, 1f)] private float poseWeightOverride = -1f;

        [Header("Edit Mode Preview")]
        [SerializeField] private bool previewInEditMode = true;
        [SerializeField] private bool sampleEditModeBaseClip = true;
        [SerializeField] private AnimationClip editModeBaseClip = null;
        [SerializeField, Min(0f)] private float editModeBaseClipTime = 0f;
        [SerializeField, Range(0f, 1f)] private float editModePreviewWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float editModeDashPreviewWeight = 0f;

        [Header("Arm Pose")]
        [SerializeField] private float upperArmBackAngle = 34f;
        [SerializeField] private float upperArmOpenAngle = 20f;
        [SerializeField] private float forearmBackAngle = 10f;
        [SerializeField] private float forearmOpenAngle = 4f;

        [Header("Leg Pose")]
        [FormerlySerializedAs("upperLegForwardAngle")]
        [SerializeField] private float leftUpperLegForwardAngle = 4f;
        [SerializeField] private float rightUpperLegForwardAngle = 4f;
        [FormerlySerializedAs("lowerLegBackAngle")]
        [SerializeField] private float leftLowerLegBackAngle = 6f;
        [SerializeField] private float rightLowerLegBackAngle = 6f;

        [Header("Hand Pose")]
        [SerializeField] private bool openHands = false;
        [SerializeField, Range(0f, 1f)] private float handOpenWeight = 1f;
        [SerializeField] private Vector3 fingerOpenEuler = new Vector3(-18f, 0f, 0f);
        [SerializeField] private Vector3 thumbOpenEuler = new Vector3(-10f, 0f, 0f);

        [Header("Dash Pose Animation")]
        [SerializeField] private float dashExtraBackAngle = 12f;
        [SerializeField] private float dashPoseWeightAdd = 0.25f;
        [SerializeField] private float dashPoseEnterTime = 0.18f;
        [SerializeField] private float dashPoseExitTime = 0.28f;

        [Header("Fine Tune")]
        [SerializeField] private Vector3 leftUpperArmExtraEuler = Vector3.zero;
        [SerializeField] private Vector3 rightUpperArmExtraEuler = Vector3.zero;
        [SerializeField] private Vector3 leftForearmExtraEuler = Vector3.zero;
        [SerializeField] private Vector3 rightForearmExtraEuler = Vector3.zero;

        [Header("Runtime Status")]
        [SerializeField] private float effectivePoseWeight;
        [SerializeField] private float effectiveDashPoseWeight;
        [SerializeField] private string status = "Not started";

        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private Transform leftLowerArm;
        private Transform rightLowerArm;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftLowerLeg;
        private Transform rightLowerLeg;
        private Transform[] leftThumbBones = new Transform[0];
        private Transform[] rightThumbBones = new Transform[0];
        private Transform[] leftFingerBones = new Transform[0];
        private Transform[] rightFingerBones = new Transform[0];
        private float smoothedWeight;
        private float dashPoseBlend;
        private bool editModePoseCaptured;
        private Quaternion leftUpperArmEditLocalRotation;
        private Quaternion rightUpperArmEditLocalRotation;
        private Quaternion leftLowerArmEditLocalRotation;
        private Quaternion rightLowerArmEditLocalRotation;
        private Quaternion leftUpperLegEditLocalRotation;
        private Quaternion rightUpperLegEditLocalRotation;
        private Quaternion leftLowerLegEditLocalRotation;
        private Quaternion rightLowerLegEditLocalRotation;
        private Quaternion[] leftThumbEditLocalRotations = new Quaternion[0];
        private Quaternion[] rightThumbEditLocalRotations = new Quaternion[0];
        private Quaternion[] leftFingerEditLocalRotations = new Quaternion[0];
        private Quaternion[] rightFingerEditLocalRotations = new Quaternion[0];

        public void Configure(SkyCircuitFlightController flightController, Rigidbody body)
        {
            controller = flightController;
            sourceBody = body;
        }

        private void Awake()
        {
            ResolveReferences();
            CacheBones();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheBones();
            smoothedWeight = 0f;
            dashPoseBlend = 0f;
            if (!Application.isPlaying)
            {
                CaptureEditModePose();
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                RestoreEditModePose();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying && isActiveAndEnabled)
            {
                ApplyEditModePreview();
            }
        }

        private void LateUpdate()
        {
            ResolveReferences();

            if (animator == null || !animator.isHuman)
            {
                status = animator == null ? "No Animator found" : "Animator is not humanoid";
                return;
            }

            if (!Application.isPlaying)
            {
                ApplyEditModePreview();
                return;
            }

            if (leftUpperArm == null || rightUpperArm == null || leftLowerArm == null || rightLowerArm == null ||
                leftUpperLeg == null || rightUpperLeg == null || leftLowerLeg == null || rightLowerLeg == null)
            {
                CacheBones();
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            UpdateDashPose(dt);
            float targetWeight = BuildPoseWeight();
            smoothedWeight = Mathf.Lerp(smoothedWeight, targetWeight, DampBlend(poseSmoothing, dt));
            effectivePoseWeight = smoothedWeight;
            status = HasAllPoseBones() ? "Running" : "Missing pose bones";
            ApplyArmPose(smoothedWeight);
            ApplyLegPose(smoothedWeight);
            ApplyHandPose(smoothedWeight);
        }

        [ContextMenu("Recapture Edit Mode Pose")]
        private void RecaptureEditModePose()
        {
            ResolveReferences();
            CacheBones();
            editModePoseCaptured = false;
            CaptureEditModePose();
            ApplyEditModePreview();
        }

        [ContextMenu("Restore Edit Mode Pose")]
        private void RestoreEditModePoseMenu()
        {
            RestoreEditModePose();
        }

        private void ApplyEditModePreview()
        {
            if (!previewInEditMode)
            {
                RestoreEditModePose();
                effectivePoseWeight = 0f;
                effectiveDashPoseWeight = 0f;
                status = "Edit preview disabled";
                return;
            }

            ResolveReferences();
            if (animator == null || !animator.isHuman)
            {
                status = animator == null ? "No Animator found" : "Animator is not humanoid";
                return;
            }

            if (leftUpperArm == null || rightUpperArm == null || leftLowerArm == null || rightLowerArm == null ||
                leftUpperLeg == null || rightUpperLeg == null || leftLowerLeg == null || rightLowerLeg == null)
            {
                CacheBones();
            }

            CaptureEditModePose();
            RestoreEditModePose();
            SampleEditModeBaseClip();

            effectivePoseWeight = editModePreviewWeight;
            effectiveDashPoseWeight = editModeDashPreviewWeight;
            status = HasAllPoseBones() ? BuildEditPreviewStatus() : "Missing pose bones";
            ApplyArmPose(effectivePoseWeight);
            ApplyLegPose(effectivePoseWeight);
            ApplyHandPose(effectivePoseWeight);
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

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftThumbBones = BuildFingerBones(
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftThumbIntermediate,
                HumanBodyBones.LeftThumbDistal);
            rightThumbBones = BuildFingerBones(
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightThumbIntermediate,
                HumanBodyBones.RightThumbDistal);
            leftFingerBones = BuildFingerBones(
                HumanBodyBones.LeftIndexProximal,
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftIndexDistal,
                HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.LeftMiddleDistal,
                HumanBodyBones.LeftRingProximal,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.LeftRingDistal,
                HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.LeftLittleDistal);
            rightFingerBones = BuildFingerBones(
                HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightIndexDistal,
                HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.RightMiddleDistal,
                HumanBodyBones.RightRingProximal,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.RightRingDistal,
                HumanBodyBones.RightLittleProximal,
                HumanBodyBones.RightLittleIntermediate,
                HumanBodyBones.RightLittleDistal);
        }

        private Transform[] BuildFingerBones(params HumanBodyBones[] bones)
        {
            List<Transform> result = new List<Transform>(bones.Length);
            foreach (HumanBodyBones bone in bones)
            {
                Transform boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null)
                {
                    result.Add(boneTransform);
                }
            }

            return result.ToArray();
        }

        private void CaptureEditModePose()
        {
            if (editModePoseCaptured || !HasAllPoseBones())
            {
                return;
            }

            leftUpperArmEditLocalRotation = leftUpperArm.localRotation;
            rightUpperArmEditLocalRotation = rightUpperArm.localRotation;
            leftLowerArmEditLocalRotation = leftLowerArm.localRotation;
            rightLowerArmEditLocalRotation = rightLowerArm.localRotation;
            leftUpperLegEditLocalRotation = leftUpperLeg.localRotation;
            rightUpperLegEditLocalRotation = rightUpperLeg.localRotation;
            leftLowerLegEditLocalRotation = leftLowerLeg.localRotation;
            rightLowerLegEditLocalRotation = rightLowerLeg.localRotation;
            leftThumbEditLocalRotations = CaptureLocalRotations(leftThumbBones);
            rightThumbEditLocalRotations = CaptureLocalRotations(rightThumbBones);
            leftFingerEditLocalRotations = CaptureLocalRotations(leftFingerBones);
            rightFingerEditLocalRotations = CaptureLocalRotations(rightFingerBones);
            editModePoseCaptured = true;
        }

        private void RestoreEditModePose()
        {
            if (!editModePoseCaptured || !HasAllPoseBones())
            {
                return;
            }

            leftUpperArm.localRotation = leftUpperArmEditLocalRotation;
            rightUpperArm.localRotation = rightUpperArmEditLocalRotation;
            leftLowerArm.localRotation = leftLowerArmEditLocalRotation;
            rightLowerArm.localRotation = rightLowerArmEditLocalRotation;
            leftUpperLeg.localRotation = leftUpperLegEditLocalRotation;
            rightUpperLeg.localRotation = rightUpperLegEditLocalRotation;
            leftLowerLeg.localRotation = leftLowerLegEditLocalRotation;
            rightLowerLeg.localRotation = rightLowerLegEditLocalRotation;
            RestoreLocalRotations(leftThumbBones, leftThumbEditLocalRotations);
            RestoreLocalRotations(rightThumbBones, rightThumbEditLocalRotations);
            RestoreLocalRotations(leftFingerBones, leftFingerEditLocalRotations);
            RestoreLocalRotations(rightFingerBones, rightFingerEditLocalRotations);
        }

        private static Quaternion[] CaptureLocalRotations(Transform[] bones)
        {
            Quaternion[] rotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                rotations[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
            }

            return rotations;
        }

        private static void RestoreLocalRotations(Transform[] bones, Quaternion[] rotations)
        {
            int count = Mathf.Min(bones.Length, rotations.Length);
            for (int i = 0; i < count; i++)
            {
                if (bones[i] != null)
                {
                    bones[i].localRotation = rotations[i];
                }
            }
        }

        private void SampleEditModeBaseClip()
        {
            if (!sampleEditModeBaseClip || editModeBaseClip == null || animator == null)
            {
                return;
            }

            float clipLength = Mathf.Max(0.0001f, editModeBaseClip.length);
            float sampleTime = Mathf.Repeat(editModeBaseClipTime, clipLength);
            editModeBaseClip.SampleAnimation(animator.gameObject, sampleTime);
            CacheBones();
        }

        private string BuildEditPreviewStatus()
        {
            if (!sampleEditModeBaseClip)
            {
                return "Edit preview";
            }

            return editModeBaseClip != null ? "Edit preview: base clip" : "Edit preview: no base clip";
        }

        private float BuildPoseWeight()
        {
            if (poseWeightOverride >= 0f)
            {
                return Mathf.Clamp01(poseWeightOverride);
            }

            float speed = 0f;
            if (sourceBody != null)
            {
                speed = sourceBody.linearVelocity.magnitude;
            }

            if (controller != null)
            {
                speed = Mathf.Max(speed, controller.CurrentSpeed);
            }

            float speedWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minPoseSpeed, fullPoseSpeed, speed));
            float dashWeight = dashPoseWeightAdd * effectiveDashPoseWeight;
            return Mathf.Clamp01(basePoseWeight + speedWeight + dashWeight);
        }

        private void UpdateDashPose(float dt)
        {
            float target = controller != null && controller.IsDashing ? 1f : 0f;
            float duration = target > dashPoseBlend ? dashPoseEnterTime : dashPoseExitTime;

            if (duration <= 0.001f)
            {
                dashPoseBlend = target;
            }
            else
            {
                dashPoseBlend = Mathf.MoveTowards(dashPoseBlend, target, dt / duration);
            }

            effectiveDashPoseWeight = Mathf.SmoothStep(0f, 1f, dashPoseBlend);
        }

        private void ApplyArmPose(float weight)
        {
            if (weight <= 0.001f)
            {
                return;
            }

            Quaternion bodyRotation = sourceBody != null ? sourceBody.rotation : transform.rotation;
            Vector3 bodyRight = bodyRotation * Vector3.right;
            Vector3 bodyForward = bodyRotation * Vector3.forward;

            float dashBack = dashExtraBackAngle * effectiveDashPoseWeight;
            float upperBack = (upperArmBackAngle + dashBack) * weight;
            float lowerBack = (forearmBackAngle + dashBack * 0.45f) * weight;
            float upperOpen = upperArmOpenAngle * weight;
            float lowerOpen = forearmOpenAngle * weight;

            ApplyMirroredArmPose(leftUpperArm, -1f, bodyRight, bodyForward, upperBack, upperOpen, leftUpperArmExtraEuler, weight);
            ApplyMirroredArmPose(rightUpperArm, 1f, bodyRight, bodyForward, upperBack, upperOpen, rightUpperArmExtraEuler, weight);
            ApplyMirroredArmPose(leftLowerArm, -1f, bodyRight, bodyForward, lowerBack, lowerOpen, leftForearmExtraEuler, weight);
            ApplyMirroredArmPose(rightLowerArm, 1f, bodyRight, bodyForward, lowerBack, lowerOpen, rightForearmExtraEuler, weight);
        }

        private void ApplyLegPose(float weight)
        {
            if (weight <= 0.001f ||
                (Mathf.Abs(leftUpperLegForwardAngle) <= 0.001f &&
                 Mathf.Abs(rightUpperLegForwardAngle) <= 0.001f &&
                 Mathf.Abs(leftLowerLegBackAngle) <= 0.001f &&
                 Mathf.Abs(rightLowerLegBackAngle) <= 0.001f))
            {
                return;
            }

            Quaternion bodyRotation = sourceBody != null ? sourceBody.rotation : transform.rotation;
            Vector3 bodyRight = bodyRotation * Vector3.right;

            ApplyLegBonePose(leftUpperLeg, bodyRight, -leftUpperLegForwardAngle * weight);
            ApplyLegBonePose(rightUpperLeg, bodyRight, -rightUpperLegForwardAngle * weight);
            ApplyLegBonePose(leftLowerLeg, bodyRight, leftLowerLegBackAngle * weight);
            ApplyLegBonePose(rightLowerLeg, bodyRight, rightLowerLegBackAngle * weight);
        }

        private void ApplyHandPose(float weight)
        {
            if (!openHands || weight <= 0.001f || handOpenWeight <= 0.001f)
            {
                return;
            }

            float blend = weight * handOpenWeight;
            ApplyLocalFingerPose(leftThumbBones, thumbOpenEuler * blend);
            ApplyLocalFingerPose(rightThumbBones, thumbOpenEuler * blend);
            ApplyLocalFingerPose(leftFingerBones, fingerOpenEuler * blend);
            ApplyLocalFingerPose(rightFingerBones, fingerOpenEuler * blend);
        }

        private static void ApplyLocalFingerPose(Transform[] bones, Vector3 euler)
        {
            if (bones == null || euler.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(euler);
            foreach (Transform bone in bones)
            {
                if (bone != null)
                {
                    bone.localRotation *= rotation;
                }
            }
        }

        private static void ApplyLegBonePose(Transform bone, Vector3 bodyRight, float angle)
        {
            if (bone == null)
            {
                return;
            }

            bone.rotation = Quaternion.AngleAxis(angle, bodyRight) * bone.rotation;
        }

        private static void ApplyMirroredArmPose(
            Transform bone,
            float side,
            Vector3 bodyRight,
            Vector3 bodyForward,
            float backAngle,
            float openAngle,
            Vector3 extraEuler,
            float weight)
        {
            if (bone == null)
            {
                return;
            }

            bone.rotation = Quaternion.AngleAxis(backAngle, bodyRight) * bone.rotation;
            bone.rotation = Quaternion.AngleAxis(side * openAngle, bodyForward) * bone.rotation;

            if (extraEuler.sqrMagnitude > 0.0001f)
            {
                bone.localRotation *= Quaternion.Euler(extraEuler * weight);
            }
        }

        private bool HasArmBones()
        {
            return leftUpperArm != null && rightUpperArm != null && leftLowerArm != null && rightLowerArm != null;
        }

        private bool HasAllPoseBones()
        {
            return HasArmBones() &&
                leftUpperLeg != null &&
                rightUpperLeg != null &&
                leftLowerLeg != null &&
                rightLowerLeg != null;
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * dt);
        }
    }
}
