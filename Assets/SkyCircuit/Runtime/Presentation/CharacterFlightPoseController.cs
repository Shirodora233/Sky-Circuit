using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Presentation
{
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

        [Header("Arm Pose")]
        [SerializeField] private float upperArmBackAngle = 34f;
        [SerializeField] private float upperArmOpenAngle = 20f;
        [SerializeField] private float forearmBackAngle = 10f;
        [SerializeField] private float forearmOpenAngle = 4f;

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
        private float smoothedWeight;
        private float dashPoseBlend;

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
        }

        private void LateUpdate()
        {
            ResolveReferences();

            if (animator == null || !animator.isHuman)
            {
                status = animator == null ? "No Animator found" : "Animator is not humanoid";
                return;
            }

            if (leftUpperArm == null || rightUpperArm == null || leftLowerArm == null || rightLowerArm == null)
            {
                CacheBones();
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            UpdateDashPose(dt);
            float targetWeight = BuildPoseWeight();
            smoothedWeight = Mathf.Lerp(smoothedWeight, targetWeight, DampBlend(poseSmoothing, dt));
            effectivePoseWeight = smoothedWeight;
            status = HasArmBones() ? "Running" : "Missing arm bones";
            ApplyArmPose(smoothedWeight);
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

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * dt);
        }
    }
}
