using System;
using System.IO;
using System.Linq;
using SkyCircuit.Flight;
using SkyCircuit.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SkyCircuit.EditorTools
{
    internal static class SkyCircuitCharacterVisualSceneUtility
    {
        private const string ArtSearchFolder = "Assets/Art";
        private const string FlyingAnimationPath = "Assets/Animation/Flying.fbx";
        private const string DemoControllerPath = "Assets/SkyCircuit/Art/Animations/SC_DemoFlying.controller";
        private const string MaterialsFolder = "Assets/SkyCircuit/Art/Materials";

        private const float CharacterVisualScale = 3.2f;
        private const float CharacterVisualPitch = -18f;
        private const string VisualModelSuffix = " Model";

        private static readonly Vector3 CharacterVisualOffset = new Vector3(0f, -2.8f, 0f);

        public static void EnsureFolders()
        {
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Art");
            CreateFolder("Assets/SkyCircuit/Art", "Animations");
            CreateFolder("Assets/SkyCircuit/Art", "Materials");
        }

        public static AnimatorController EnsureFlyingAnimatorController()
        {
            EnsureFolders();

            AnimationClip clip = LoadFlyingClip();
            if (clip == null)
            {
                Debug.LogWarning($"Sky Circuit could not find an animation clip in {FlyingAnimationPath}.");
                return null;
            }

            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DemoControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(DemoControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                stateMachine.RemoveState(childState.state);
            }

            AnimatorState flyingState = stateMachine.AddState("Flying Loop");
            flyingState.motion = clip;
            flyingState.speed = 1f;
            stateMachine.defaultState = flyingState;

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(clip);
            return controller;
        }

        public static void EnsureCharacterVisual(GameObject flightRoot, string visualName)
        {
            if (flightRoot == null)
            {
                return;
            }

            AnimatorController animatorController = EnsureFlyingAnimatorController();
            HidePrototypeRenderer(flightRoot);
            RemoveForwardMarker(flightRoot.transform);

            EnsureVisualPivot(flightRoot.transform, visualName, animatorController);

            SkyCircuitFlightController controller = flightRoot.GetComponent<SkyCircuitFlightController>();
            Rigidbody body = flightRoot.GetComponent<Rigidbody>();
            EnsurePoseController(flightRoot, controller, body);
            EnsureWindController(flightRoot, controller, body);
        }

        private static Transform EnsureVisualPivot(
            Transform parent,
            string visualName,
            RuntimeAnimatorController animatorController)
        {
            Transform visualPivot = parent.Find(visualName) ?? FindAnyCharacterVisual(parent);
            if (visualPivot == null)
            {
                visualPivot = CreateCharacterVisual(parent, visualName, animatorController);
            }
            else if (NeedsPivotWrapper(visualPivot))
            {
                visualPivot = WrapVisualInPivot(parent, visualPivot, visualName);
                ConfigureAnimator(visualPivot.gameObject, animatorController);
            }
            else
            {
                visualPivot.name = visualName;
                ConfigureVisualPivotTransform(visualPivot);

                Transform model = FindVisualModel(visualPivot);
                if (model != null)
                {
                    ConfigureVisualModelTransform(model);
                }

                ConfigureAnimator(visualPivot.gameObject, animatorController);
            }

            return visualPivot;
        }

        private static Transform CreateCharacterVisual(
            Transform parent,
            string visualName,
            RuntimeAnimatorController animatorController)
        {
            Transform pivot = CreateVisualPivot(parent, visualName);
            GameObject prefab = LoadFirstCharacterPrefab();
            if (prefab == null)
            {
                Debug.LogWarning($"Sky Circuit could not find a character prefab under {ArtSearchFolder}.");
                CreateFallbackVisual(pivot, visualName);
                return pivot;
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = BuildModelName(visualName);
            visual.transform.SetParent(pivot, false);
            ConfigureVisualModelTransform(visual.transform);
            ConfigureAnimator(visual, animatorController);
            return pivot;
        }

        private static Transform CreateVisualPivot(Transform parent, string visualName)
        {
            GameObject pivotObject = new GameObject(visualName);
            Transform pivot = pivotObject.transform;
            pivot.SetParent(parent, false);
            ConfigureVisualPivotTransform(pivot);
            return pivot;
        }

        private static Transform WrapVisualInPivot(Transform parent, Transform model, string visualName)
        {
            int siblingIndex = model.GetSiblingIndex();
            Transform pivot = CreateVisualPivot(parent, visualName);
            pivot.SetSiblingIndex(siblingIndex);

            model.name = BuildModelName(visualName);
            model.SetParent(pivot, false);
            ConfigureVisualModelTransform(model);
            return pivot;
        }

        private static void ConfigureVisualPivotTransform(Transform pivot)
        {
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.Euler(CharacterVisualPitch, 0f, 0f);
            pivot.localScale = Vector3.one;
            EditorUtility.SetDirty(pivot);
        }

        private static void ConfigureVisualModelTransform(Transform model)
        {
            model.localPosition = CharacterVisualOffset;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one * CharacterVisualScale;
            EditorUtility.SetDirty(model);
        }

        private static void ConfigureAnimator(GameObject visual, RuntimeAnimatorController animatorController)
        {
            Animator animator = visual.GetComponent<Animator>() ?? visual.GetComponentInChildren<Animator>();
            if (animator == null || animatorController == null)
            {
                return;
            }

            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
        }

        private static void EnsurePoseController(GameObject flightRoot, SkyCircuitFlightController controller, Rigidbody body)
        {
            CharacterFlightPoseController pose = flightRoot.GetComponent<CharacterFlightPoseController>();
            if (pose == null)
            {
                pose = flightRoot.AddComponent<CharacterFlightPoseController>();
            }

            pose.Configure(controller, body);

            SerializedObject serializedPose = new SerializedObject(pose);
            SetFloat(serializedPose, "upperArmBackAngle", 22.81f);
            SetFloat(serializedPose, "upperArmOpenAngle", 0f);
            SetFloat(serializedPose, "forearmBackAngle", 10f);
            SetFloat(serializedPose, "forearmOpenAngle", 4f);
            SetFloat(serializedPose, "turnUpperArmOpenReduction", 0.65f);
            SetFloat(serializedPose, "leftUpperLegForwardAngle", 21.3f);
            SetFloat(serializedPose, "rightUpperLegForwardAngle", 14.68f);
            SetFloat(serializedPose, "leftLowerLegBackAngle", 34.41f);
            SetFloat(serializedPose, "rightLowerLegBackAngle", 23.23f);
            SetFloat(serializedPose, "turnUpperLegForwardAngle", 12f);
            SetFloat(serializedPose, "turnLowerLegBackAngle", 18f);
            SetFloat(serializedPose, "turnLegFullRate", 70f);
            SetFloat(serializedPose, "turnLegInputGain", 1.35f);
            SetFloat(serializedPose, "turnLegInputResponse", 0.7f);
            SetFloat(serializedPose, "turnLegDeadZone", 2f);
            SetFloat(serializedPose, "turnLegPoseSmoothing", 7f);
            SetVector3(serializedPose, "visualBaseLocalEuler", new Vector3(CharacterVisualPitch, 0f, 0f));
            SetFloat(serializedPose, "visualYawAngle", 8f);
            SetFloat(serializedPose, "visualYawFullErrorAngle", 28f);
            SetFloat(serializedPose, "visualYawSharpness", 10f);
            SetFloat(serializedPose, "visualBankAngle", 42f);
            SetFloat(serializedPose, "visualBankEnterSharpness", 3f);
            SetFloat(serializedPose, "visualBankReturnSharpness", 1.2f);
            SetBool(serializedPose, "openHands", false);
            SetFloat(serializedPose, "handOpenWeight", 1f);
            SetVector3(serializedPose, "fingerOpenEuler", new Vector3(-18f, 0f, 0f));
            SetVector3(serializedPose, "thumbOpenEuler", new Vector3(-10f, 0f, 0f));
            SetFloat(serializedPose, "dashExtraBackAngle", 12f);
            SetFloat(serializedPose, "dashPoseWeightAdd", 0.25f);
            SetFloat(serializedPose, "dashPoseEnterTime", 0.18f);
            SetFloat(serializedPose, "dashPoseExitTime", 0.28f);
            SetFloat(serializedPose, "poseWeightOverride", -1f);
            SetBool(serializedPose, "previewInEditMode", true);
            SetBool(serializedPose, "sampleEditModeBaseClip", true);
            SetObjectReference(serializedPose, "editModeBaseClip", LoadFlyingClip());
            SetFloat(serializedPose, "editModeBaseClipTime", 0f);
            SetFloat(serializedPose, "editModePreviewWeight", 1f);
            SetFloat(serializedPose, "editModeDashPreviewWeight", 0f);
            SetFloat(serializedPose, "editModeTurnAmount", 0f);
            serializedPose.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureWindController(GameObject flightRoot, SkyCircuitFlightController controller, Rigidbody body)
        {
            CharacterFlightVisualWind wind = flightRoot.GetComponent<CharacterFlightVisualWind>();
            if (wind == null)
            {
                wind = flightRoot.AddComponent<CharacterFlightVisualWind>();
            }

            wind.Configure(controller, body);
            EditorUtility.SetDirty(wind);
        }

        private static AnimationClip LoadFlyingClip()
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(FlyingAnimationPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__", StringComparison.Ordinal));
        }

        private static GameObject LoadFirstCharacterPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ArtSearchFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    return prefab;
                }
            }

            return null;
        }

        private static Transform FindAnyCharacterVisual(Transform root)
        {
            foreach (Transform child in root)
            {
                if (child.name.IndexOf("Character Visual", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool NeedsPivotWrapper(Transform visual)
        {
            return visual.GetComponent<Animator>() != null ||
                visual.GetComponent<Renderer>() != null ||
                PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject) != null;
        }

        private static Transform FindVisualModel(Transform visualPivot)
        {
            if (visualPivot.childCount > 0)
            {
                return visualPivot.GetChild(0);
            }

            return null;
        }

        private static string BuildModelName(string pivotName)
        {
            return pivotName.EndsWith(VisualModelSuffix, StringComparison.Ordinal)
                ? pivotName
                : pivotName + VisualModelSuffix;
        }

        private static void HidePrototypeRenderer(GameObject flightRoot)
        {
            Renderer renderer = flightRoot.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void RemoveForwardMarker(Transform root)
        {
            Transform marker = root.Find("Forward Marker");
            if (marker != null)
            {
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }
        }

        private static void CreateFallbackVisual(Transform parent, string visualName)
        {
            Material material = CreateFallbackMaterial();
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = BuildModelName(visualName);
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            body.transform.localScale = new Vector3(0.7f, 1.5f, 0.7f);

            Renderer renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        }

        private static Material CreateFallbackMaterial()
        {
            string path = $"{MaterialsFolder}/SC_CharacterFallback.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetColor("_BaseColor", new Color(0.12f, 0.78f, 1f));
            material.SetColor("_EmissionColor", new Color(0.1f, 0.55f, 1f));
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
