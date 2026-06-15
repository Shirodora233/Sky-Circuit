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
        [FormerlySerializedAs("editModeTurnPreview")]
        [Tooltip("Scene preview for all turn-driven pose layers. -1 previews left turn, 1 previews right turn.")]
        [SerializeField, Range(-1f, 1f)] private float editModeTurnAmount = 0f;

        [Header("Arm Pose")]
        [SerializeField] private float upperArmBackAngle = 34f;
        [SerializeField] private float upperArmOpenAngle = 20f;
        [SerializeField] private float forearmBackAngle = 10f;
        [SerializeField] private float forearmOpenAngle = 4f;

        [Header("Turn Arm Pose")]
        [Tooltip("Reduces the upper arm open amount on the turn-side arm. 1 fully pulls that arm's open angle toward zero.")]
        [SerializeField, Range(0f, 1f)] private float turnUpperArmOpenReduction = 0.65f;

        [Header("Leg Pose")]
        [FormerlySerializedAs("upperLegForwardAngle")]
        [SerializeField] private float leftUpperLegForwardAngle = 4f;
        [SerializeField] private float rightUpperLegForwardAngle = 4f;
        [FormerlySerializedAs("lowerLegBackAngle")]
        [SerializeField] private float leftLowerLegBackAngle = 6f;
        [SerializeField] private float rightLowerLegBackAngle = 6f;

        [Header("Turn Leg Pose")]
        [SerializeField] private float turnUpperLegForwardAngle = 12f;
        [SerializeField] private float turnLowerLegBackAngle = 18f;
        [SerializeField, Min(1f)] private float turnLegFullRate = 70f;
        [SerializeField, Min(0f)] private float turnLegInputGain = 1.35f;
        [SerializeField, Range(0.25f, 2f)] private float turnLegInputResponse = 0.7f;
        [SerializeField, Min(0f)] private float turnLegDeadZone = 2f;
        [SerializeField, Min(0.01f)] private float turnLegPoseSmoothing = 7f;

        [Header("Visual Turn Pose")]
        [Tooltip("Neutral local rotation for the character visual root. Turn pose yaw and bank are applied on top of this value.")]
        [SerializeField] private Vector3 visualBaseLocalEuler = new Vector3(-18f, 0f, 0f);
        [SerializeField] private float visualYawAngle = 8f;
        [Tooltip("Control yaw error, in degrees, needed to reach full visual yaw pose.")]
        [SerializeField, Min(1f)] private float visualYawFullErrorAngle = 28f;
        [Tooltip("How quickly the character visual yaw follows the camera/control yaw offset.")]
        [SerializeField, Min(0.01f)] private float visualYawSharpness = 10f;
        [SerializeField] private float visualBankAngle = 42f;
        [SerializeField, Min(0.01f)] private float visualBankEnterSharpness = 3f;
        [SerializeField, Min(0.01f)] private float visualBankReturnSharpness = 1.2f;

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
        [SerializeField] private float effectiveTurnRate;
        [SerializeField] private float effectiveTurnLegPose;
        [SerializeField] private float effectiveVisualYaw;
        [SerializeField] private float effectiveVisualBank;
        [SerializeField] private string status = "Not started";

        private Transform visualRoot;
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
        private float smoothedTurnLegPose;
        private float smoothedVisualYawPose;
        private float smoothedVisualBankPose;
        private bool visualBaseCaptured;
        private bool editModePoseCaptured;
        private Quaternion visualBaseLocalRotation;
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
            smoothedTurnLegPose = 0f;
            smoothedVisualYawPose = 0f;
            smoothedVisualBankPose = 0f;
            visualBaseCaptured = false;
            CaptureVisualBaseRotation();
            if (!Application.isPlaying)
            {
                CaptureEditModePose();
            }
        }

        private void OnDisable()
        {
            RestoreVisualBaseRotation();
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
            RestoreVisualBaseRotation();
            UpdateDashPose(dt);
            UpdateTurnLegPose(dt);
            UpdateVisualTurnPose(dt);
            float targetWeight = BuildPoseWeight();
            smoothedWeight = Mathf.Lerp(smoothedWeight, targetWeight, DampBlend(poseSmoothing, dt));
            effectivePoseWeight = smoothedWeight;
            status = HasAllPoseBones() ? "Running" : "Missing pose bones";
            ApplyArmPose(smoothedWeight, smoothedTurnLegPose);
            ApplyLegPose(smoothedWeight, smoothedTurnLegPose);
            ApplyHandPose(smoothedWeight);
            ApplyVisualTurnPose(effectiveVisualYaw, effectiveVisualBank);
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
                effectiveTurnRate = 0f;
                effectiveTurnLegPose = 0f;
                effectiveVisualYaw = 0f;
                effectiveVisualBank = 0f;
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
            CaptureVisualBaseRotation(true);

            effectivePoseWeight = editModePreviewWeight;
            effectiveDashPoseWeight = editModeDashPreviewWeight;
            effectiveTurnRate = editModeTurnAmount * turnLegFullRate;
            effectiveTurnLegPose = editModeTurnAmount;
            effectiveVisualYaw = editModeTurnAmount * visualYawAngle;
            effectiveVisualBank = -editModeTurnAmount * visualBankAngle;
            status = HasAllPoseBones() ? BuildEditPreviewStatus() : "Missing pose bones";
            ApplyArmPose(effectivePoseWeight, effectiveTurnLegPose);
            ApplyLegPose(effectivePoseWeight, effectiveTurnLegPose);
            ApplyHandPose(effectivePoseWeight);
            ApplyVisualTurnPose(effectiveVisualYaw, effectiveVisualBank);
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

            if (animator != null)
            {
                visualRoot = animator.transform;
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

            CaptureVisualBaseRotation(true);
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

            RestoreVisualBaseRotation();
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

        private void CaptureVisualBaseRotation(bool force = false)
        {
            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.transform;
            }

            if (visualRoot == null || (visualBaseCaptured && !force))
            {
                return;
            }

            visualBaseLocalRotation = Quaternion.Euler(visualBaseLocalEuler);
            visualBaseCaptured = true;
        }

        private void RestoreVisualBaseRotation()
        {
            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.transform;
            }

            if (visualRoot != null)
            {
                CaptureVisualBaseRotation(true);
                visualRoot.localRotation = visualBaseLocalRotation;
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

        private void UpdateTurnLegPose(float dt)
        {
            float target = BuildActualTurnPoseInput();
            smoothedTurnLegPose = Mathf.Lerp(smoothedTurnLegPose, target, DampBlend(turnLegPoseSmoothing, dt));
            effectiveTurnLegPose = smoothedTurnLegPose;
        }

        private void UpdateVisualTurnPose(float dt)
        {
            float yawTarget = BuildControlYawPoseInput();
            smoothedVisualYawPose = Mathf.Lerp(smoothedVisualYawPose, yawTarget, DampBlend(visualYawSharpness, dt));

            float bankTarget = BuildActualTurnPoseInput();
            float bankSharpness = Mathf.Abs(bankTarget) > Mathf.Abs(smoothedVisualBankPose)
                ? visualBankEnterSharpness
                : visualBankReturnSharpness;

            smoothedVisualBankPose = Mathf.Lerp(smoothedVisualBankPose, bankTarget, DampBlend(bankSharpness, dt));
            effectiveVisualYaw = smoothedVisualYawPose * visualYawAngle;
            effectiveVisualBank = -smoothedVisualBankPose * visualBankAngle;
        }

        private float BuildControlYawPoseInput()
        {
            if (controller == null)
            {
                return 0f;
            }

            Quaternion bodyRotation = sourceBody != null ? sourceBody.rotation : transform.rotation;
            float yawError = Mathf.DeltaAngle(bodyRotation.eulerAngles.y, controller.ControlRotation.eulerAngles.y);
            return Mathf.Clamp(yawError / Mathf.Max(1f, visualYawFullErrorAngle), -1f, 1f);
        }

        private float BuildActualTurnPoseInput()
        {
            if (controller == null)
            {
                effectiveTurnRate = 0f;
                return 0f;
            }

            effectiveTurnRate = controller.SignedTurnRate;
            float sign = Mathf.Sign(effectiveTurnRate);
            float rate = Mathf.Max(0f, Mathf.Abs(effectiveTurnRate) - turnLegDeadZone);
            float normalizedRate = Mathf.Clamp01(rate / Mathf.Max(1f, turnLegFullRate));
            float shapedRate = Mathf.Pow(normalizedRate, turnLegInputResponse);
            return sign * Mathf.Clamp01(shapedRate * turnLegInputGain);
        }

        private void ApplyArmPose(float weight, float turnPose)
        {
            if (weight <= 0.001f)
            {
                return;
            }

            Quaternion bodyRotation = sourceBody != null ? sourceBody.rotation : transform.rotation;
            Vector3 bodyRight = bodyRotation * Vector3.right;
            Vector3 bodyUp = bodyRotation * Vector3.up;

            float dashBack = dashExtraBackAngle * effectiveDashPoseWeight;
            float upperBack = (upperArmBackAngle + dashBack) * weight;
            float lowerBack = (forearmBackAngle + dashBack * 0.45f) * weight;
            float leftTurn = Mathf.Clamp01(-turnPose);
            float rightTurn = Mathf.Clamp01(turnPose);
            float leftUpperOpen = BuildTurnSideUpperArmOpen(upperArmOpenAngle, leftTurn) * weight;
            float rightUpperOpen = BuildTurnSideUpperArmOpen(upperArmOpenAngle, rightTurn) * weight;
            float lowerOpen = forearmOpenAngle * weight;

            ApplyMirroredArmPose(leftUpperArm, -1f, bodyRight, bodyUp, upperBack, leftUpperOpen, leftUpperArmExtraEuler, weight);
            ApplyMirroredArmPose(rightUpperArm, 1f, bodyRight, bodyUp, upperBack, rightUpperOpen, rightUpperArmExtraEuler, weight);
            ApplyMirroredArmPose(leftLowerArm, -1f, bodyRight, bodyUp, lowerBack, lowerOpen, leftForearmExtraEuler, weight);
            ApplyMirroredArmPose(rightLowerArm, 1f, bodyRight, bodyUp, lowerBack, lowerOpen, rightForearmExtraEuler, weight);
        }

        private float BuildTurnSideUpperArmOpen(float baseOpenAngle, float turnAmount)
        {
            float reduction = Mathf.Clamp01(turnAmount) * turnUpperArmOpenReduction;
            return Mathf.Lerp(baseOpenAngle, 0f, reduction);
        }

        private void ApplyLegPose(float weight, float turnPose)
        {
            if (weight <= 0.001f ||
                (Mathf.Abs(leftUpperLegForwardAngle) <= 0.001f &&
                 Mathf.Abs(rightUpperLegForwardAngle) <= 0.001f &&
                 Mathf.Abs(leftLowerLegBackAngle) <= 0.001f &&
                 Mathf.Abs(rightLowerLegBackAngle) <= 0.001f &&
                 Mathf.Abs(turnUpperLegForwardAngle) <= 0.001f &&
                 Mathf.Abs(turnLowerLegBackAngle) <= 0.001f))
            {
                return;
            }

            Quaternion bodyRotation = sourceBody != null ? sourceBody.rotation : transform.rotation;
            Vector3 bodyRight = bodyRotation * Vector3.right;
            float leftTurn = Mathf.Clamp01(-turnPose);
            float rightTurn = Mathf.Clamp01(turnPose);
            float leftUpperForward = leftUpperLegForwardAngle + turnUpperLegForwardAngle * leftTurn;
            float rightUpperForward = rightUpperLegForwardAngle + turnUpperLegForwardAngle * rightTurn;
            float leftLowerBack = leftLowerLegBackAngle + turnLowerLegBackAngle * leftTurn;
            float rightLowerBack = rightLowerLegBackAngle + turnLowerLegBackAngle * rightTurn;

            ApplyLegBonePose(leftUpperLeg, bodyRight, -leftUpperForward * weight);
            ApplyLegBonePose(rightUpperLeg, bodyRight, -rightUpperForward * weight);
            ApplyLegBonePose(leftLowerLeg, bodyRight, leftLowerBack * weight);
            ApplyLegBonePose(rightLowerLeg, bodyRight, rightLowerBack * weight);
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

        private void ApplyVisualTurnPose(float yawAngle, float bankAngle)
        {
            if (visualRoot == null && animator != null)
            {
                visualRoot = animator.transform;
            }

            if (visualRoot == null)
            {
                return;
            }

            CaptureVisualBaseRotation(true);
            visualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(0f, yawAngle, bankAngle);
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
            Vector3 bodyUp,
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
            bone.rotation = Quaternion.AngleAxis(-side * openAngle, bodyUp) * bone.rotation;

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
