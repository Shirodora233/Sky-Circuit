using System;
using System.IO;
using System.Linq;
using SkyCircuit.CameraRigging;
using SkyCircuit.Combat;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Practice;
using SkyCircuit.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitV06PresentationSliceBuilder
    {
        private const string SourceScenePath = "Assets/Scenes/V0_3_DogfightPrototype.unity";
        private const string ScenePath = "Assets/Scenes/V0_6_PresentationSlice.unity";
        private const string ArtSearchFolder = "Assets/Art";
        private const string FlyingAnimationPath = "Assets/Animation/Flying.fbx";
        private const string DemoControllerPath = "Assets/SkyCircuit/Art/Animations/SC_DemoFlying.controller";
        private const string DemoMaterialsFolder = "Assets/SkyCircuit/Art/Materials";
        private const float CharacterVisualScale = 3.2f;
        private const float CharacterVisualPitch = -18f;
        private static readonly Vector3 DemoCameraOffset = new Vector3(1.8f, 4.6f, -7.6f);
        private static readonly Vector3 DemoAimOffset = new Vector3(0f, 0.8f, 1.6f);
        private static readonly Vector3 PresentationCameraOffset = new Vector3(0f, 7.5f, -5.7f);
        private const float PresentationLookAheadDistance = 6.5f;
        private const float PresentationVerticalLookOffset = 2.5f;
        private const float PresentationViewPitchDownDegrees = 23f;
        private const float PresentationMovementVerticalOffsetDegrees = 0f;
        private const float PresentationFieldOfView = 62f;

        [MenuItem("Sky Circuit/Build V0.6 Presentation Slice Scene")]
        public static void BuildCharacterFlightDemoScene()
        {
            EnsureFolders();
            if (!File.Exists(SourceScenePath))
            {
                Debug.LogWarning($"Sky Circuit V0.6 build needs {SourceScenePath}. Building earlier prototype scenes first.");
                SkyCircuitV01SceneBuilder.BuildPrototypeScene();
                SkyCircuitV02SceneBuilder.BuildMatchPrototypeScene();
                SkyCircuitV03SceneBuilder.BuildDogfightPrototypeScene();
                SkyCircuitV04SceneBuilder.BuildProfilePrototypeScene();
            }

            if (!File.Exists(SourceScenePath))
            {
                Debug.LogError($"Sky Circuit V0.6 build could not find {SourceScenePath}.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            scene.name = "V0_6_PresentationSlice";

            Competitor player = FindComponentOnNamedObject<Competitor>("Player Competitor");
            Competitor opponent = FindComponentOnNamedObject<Competitor>("AI Competitor");
            MatchController match = FindComponentOnNamedObject<MatchController>("Match Controller");
            DogfightController dogfight = FindComponentOnNamedObject<DogfightController>("Dogfight Controller");
            BuoyRoute route = FindComponentOnNamedObject<BuoyRoute>("Buoy Route");
            Camera mainCamera = FindComponentOnNamedObject<Camera>("Main Camera");
            MatchDebugHud hud = FindComponentOnNamedObject<MatchDebugHud>("Match HUD");

            if (player == null || opponent == null)
            {
                Debug.LogError("Sky Circuit V0.6 build could not find player and AI competitors in the V0.4 source scene.");
                return;
            }

            SkyCircuitCharacterVisualSceneUtility.EnsureCharacterVisual(player.gameObject, "Player Character Visual");
            SkyCircuitCharacterVisualSceneUtility.EnsureCharacterVisual(opponent.gameObject, "AI Character Visual");
            ConfigureMovementDirectionOffset(player.Controller);
            ConfigureMovementDirectionOffset(opponent.Controller);
            ConfigurePresentationCamera();
            if (hud != null)
            {
                hud.SetTitle("Sky Circuit V0.6 Presentation Slice");
                ConfigureIndicators(hud, mainCamera, match, route, dogfight);
                EditorUtility.SetDirty(hud);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.6 presentation slice scene built at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Scenes");
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Art");
            CreateFolder("Assets/SkyCircuit/Art", "Animations");
            CreateFolder("Assets/SkyCircuit/Art", "Materials");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static AnimatorController EnsureDemoAnimatorController()
        {
            AnimationClip clip = LoadFlyingClip();
            if (clip == null)
            {
                Debug.LogWarning($"Sky Circuit V0.6 demo could not find an animation clip in {FlyingAnimationPath}.");
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

        private static AnimationClip LoadFlyingClip()
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(FlyingAnimationPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__", StringComparison.Ordinal));
        }

        private static void CreateLighting(Transform parent)
        {
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(parent);
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.88f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.65f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.38f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.09f, 0.12f);
        }

        private static void CreateReferencePlane(Transform parent)
        {
            Material material = CreateMaterial(
                "SC_V06_ReferencePlane.mat",
                new Color(0.02f, 0.28f, 0.46f, 0.55f),
                new Color(0.02f, 0.08f, 0.12f),
                true);

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Reference Ocean Plane";
            plane.transform.SetParent(parent);
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(32f, 1f, 32f);
            plane.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(string fileName, Color baseColor, Color emissionColor, bool transparent = false)
        {
            string path = $"{DemoMaterialsFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = Path.GetFileNameWithoutExtension(fileName);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_EmissionColor", emissionColor);

            if (emissionColor.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_AlphaClip", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.renderQueue = -1;
                material.SetOverrideTag("RenderType", "Opaque");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform CreateSpawnPoint(Transform parent)
        {
            GameObject spawn = new GameObject("Demo Spawn");
            spawn.transform.SetParent(parent);
            spawn.transform.SetPositionAndRotation(new Vector3(0f, 18f, -48f), Quaternion.identity);
            return spawn.transform;
        }

        private static GameObject CreateFlightRoot(Transform parent, Transform spawnPoint)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player Flight Root";
            player.transform.SetParent(parent);
            player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            player.transform.localScale = new Vector3(0.7f, 1f, 0.7f);

            Renderer renderer = player.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            Rigidbody body = player.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;

            player.AddComponent<SkyCircuitFlightController>();
            player.AddComponent<PlayerFlightInput>();
            return player;
        }

        private static void CreateCharacterVisual(Transform parent, RuntimeAnimatorController animatorController)
        {
            GameObject prefab = LoadFirstCharacterPrefab();
            if (prefab == null)
            {
                Debug.LogWarning($"Sky Circuit V0.6 demo could not find a character prefab under {ArtSearchFolder}.");
                CreateFallbackVisual(parent);
                return;
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = "Player Character Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, -2.8f, 0f);
            visual.transform.localRotation = Quaternion.Euler(CharacterVisualPitch, 0f, 0f);
            visual.transform.localScale = Vector3.one * CharacterVisualScale;

            Animator animator = visual.GetComponent<Animator>() ?? visual.GetComponentInChildren<Animator>();
            if (animator != null && animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);
            }
        }

        private static void CreateCharacterWind(GameObject player, SkyCircuitFlightController flightController)
        {
            Type windType = FindType("SkyCircuit.Presentation.CharacterFlightVisualWind");
            if (windType == null)
            {
                Debug.LogWarning("Sky Circuit V0.6 demo could not find CharacterFlightVisualWind.");
                return;
            }

            Component wind = player.AddComponent(windType);
            var configure = windType.GetMethod("Configure");
            configure?.Invoke(wind, new object[] { flightController, player.GetComponent<Rigidbody>() });
        }

        private static void CreateCharacterPose(GameObject player, SkyCircuitFlightController flightController)
        {
            Type poseType = FindType("SkyCircuit.Presentation.CharacterFlightPoseController");
            if (poseType == null)
            {
                Debug.LogWarning("Sky Circuit V0.6 demo could not find CharacterFlightPoseController.");
                return;
            }

            Component pose = player.AddComponent(poseType);
            var configure = poseType.GetMethod("Configure");
            configure?.Invoke(pose, new object[] { flightController, player.GetComponent<Rigidbody>() });

            SerializedObject serializedPose = new SerializedObject(pose);
            SetFloat(serializedPose, "upperArmBackAngle", 22.81f);
            SetFloat(serializedPose, "upperArmOpenAngle", 0f);
            SetFloat(serializedPose, "forearmBackAngle", 10f);
            SetFloat(serializedPose, "forearmOpenAngle", 4f);
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

        private static void CreateFallbackVisual(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fallback Character Visual";
            body.transform.SetParent(parent, false);
            body.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            body.transform.localScale = new Vector3(0.7f, 1.5f, 0.7f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        }

        private static void CreateContrail(GameObject player, SkyCircuitFlightController controller)
        {
            GameObject trailObject = new GameObject("Demo Contrail");
            trailObject.transform.SetParent(player.transform, false);
            trailObject.transform.localPosition = new Vector3(0f, 0.25f, -2.3f);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.alignment = LineAlignment.View;
            trail.autodestruct = false;
            trail.emitting = true;
            trail.time = 0.65f;
            trail.minVertexDistance = 0.18f;
            trail.widthMultiplier = 0.22f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(0.25f, 0.85f, 1f, 1f);
            trail.endColor = new Color(0.25f, 0.85f, 1f, 0f);

            FlightContrailFeedback feedback = player.AddComponent<FlightContrailFeedback>();
            feedback.Configure(controller, trail);
        }

        private static FlightCameraTargetRig CreateCameraTargetRig(
            Transform parent,
            Transform player,
            SkyCircuitFlightController flightController)
        {
            GameObject rigObject = new GameObject("Camera Target Rig");
            rigObject.transform.SetParent(parent);

            GameObject aimObject = new GameObject("Camera Aim Target");
            aimObject.transform.SetParent(rigObject.transform);

            FlightCameraTargetRig rig = rigObject.AddComponent<FlightCameraTargetRig>();
            rig.Configure(player, flightController, player.GetComponent<Rigidbody>(), aimObject.transform);
            SerializedObject serializedRig = new SerializedObject(rig);
            SetFloat(serializedRig, "lookAheadDistance", DemoAimOffset.z);
            SetFloat(serializedRig, "verticalLookOffset", DemoAimOffset.y);
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
            return rig;
        }

        private static void CreateCamera(Transform followTarget, Transform lookTarget)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();

            if (!TryCreateCinemachineCamera(cameraObject, followTarget, lookTarget))
            {
                SkyCircuitCameraRig rig = cameraObject.AddComponent<SkyCircuitCameraRig>();
                rig.SetTargets(followTarget, lookTarget);
                SerializedObject serializedRig = new SerializedObject(rig);
                SetVector3(serializedRig, "localOffset", DemoCameraOffset);
                SetFloat(serializedRig, "lookAhead", DemoAimOffset.z);
                SetFloat(serializedRig, "verticalLookOffset", DemoAimOffset.y);
                serializedRig.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool TryCreateCinemachineCamera(GameObject mainCamera, Transform followTarget, Transform lookTarget)
        {
            Type brainType = FindType("Unity.Cinemachine.CinemachineBrain");
            Type cameraType = FindType("Unity.Cinemachine.CinemachineCamera");
            Type followType = FindType("Unity.Cinemachine.CinemachineFollow");
            Type rotationComposerType = FindType("Unity.Cinemachine.CinemachineRotationComposer");

            if (brainType == null || cameraType == null)
            {
                return false;
            }

            Component brain = mainCamera.AddComponent(brainType);
            SetMember(brain, "UpdateMethod", 1);
            SetMember(brain, "BlendUpdateMethod", 1);

            GameObject cinemachineObject = new GameObject("Cinemachine Follow Camera");
            Component virtualCamera = cinemachineObject.AddComponent(cameraType);
            SetMember(virtualCamera, "Follow", followTarget);
            SetMember(virtualCamera, "LookAt", lookTarget != null ? lookTarget : followTarget);
            SetNestedMember(virtualCamera, "Lens", "FieldOfView", 58f);

            if (followType != null)
            {
                Component follow = cinemachineObject.AddComponent(followType);
                SetNestedMember(follow, "TrackerSettings", "BindingMode", 2);
                SetNestedMember(follow, "TrackerSettings", "PositionDamping", Vector3.zero);
                SetNestedMember(follow, "TrackerSettings", "RotationDamping", Vector3.zero);
                SetNestedMember(follow, "TrackerSettings", "QuaternionDamping", 0f);
                SetMember(follow, "FollowOffset", DemoCameraOffset);
            }

            if (rotationComposerType != null)
            {
                Component rotationComposer = cinemachineObject.AddComponent(rotationComposerType);
                SetMember(rotationComposer, "Damping", Vector2.zero);
            }

            return true;
        }

        private static void CreateHud(Transform parent, SkyCircuitFlightController controller)
        {
            GameObject hudObject = new GameObject("Demo HUD");
            hudObject.transform.SetParent(parent);
            PresentationFlightDemoHud hud = hudObject.AddComponent<PresentationFlightDemoHud>();
            hud.Configure(controller, "Sky Circuit V0.6 Character Flight Demo");
        }

        private static void ConfigureIndicators(
            MatchDebugHud hud,
            Camera mainCamera,
            MatchController match,
            BuoyRoute route,
            DogfightController dogfight)
        {
            if (hud == null)
            {
                return;
            }

            MatchWorldIndicator worldIndicator = hud.GetComponent<MatchWorldIndicator>();
            if (worldIndicator == null)
            {
                worldIndicator = hud.gameObject.AddComponent<MatchWorldIndicator>();
            }

            worldIndicator.Configure(mainCamera, match, route);
            EditorUtility.SetDirty(worldIndicator);

            MatchPlayerIndicator playerIndicator = hud.GetComponent<MatchPlayerIndicator>();
            if (playerIndicator == null)
            {
                playerIndicator = hud.gameObject.AddComponent<MatchPlayerIndicator>();
            }

            playerIndicator.Configure(mainCamera, match, dogfight);
            EditorUtility.SetDirty(playerIndicator);
        }

        private static void ConfigureMovementDirectionOffset(SkyCircuitFlightController controller)
        {
            if (controller == null)
            {
                return;
            }

            SerializedObject serializedController = new SerializedObject(controller);
            SetFloat(serializedController, "movementVerticalOffsetDegrees", PresentationMovementVerticalOffsetDegrees);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePresentationCamera()
        {
            FlightCameraTargetRig targetRig = FindComponentOnNamedObject<FlightCameraTargetRig>("Camera Target Rig");
            if (targetRig != null)
            {
                SerializedObject serializedRig = new SerializedObject(targetRig);
                SetFloat(serializedRig, "lookAheadDistance", PresentationLookAheadDistance);
                SetFloat(serializedRig, "verticalLookOffset", PresentationVerticalLookOffset);
                SetBool(serializedRig, "alignViewWithForward", true);
                SetVector3(serializedRig, "cameraFollowOffset", PresentationCameraOffset);
                SetFloat(serializedRig, "viewPitchDownDegrees", PresentationViewPitchDownDegrees);
                serializedRig.ApplyModifiedPropertiesWithoutUndo();
            }

            Camera mainCamera = FindComponentOnNamedObject<Camera>("Main Camera");
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = PresentationFieldOfView;
                EditorUtility.SetDirty(mainCamera);
            }

            GameObject cinemachineObject = FindNamedObject("Cinemachine Follow Camera");
            Component cinemachineFollowComponent = null;
            if (cinemachineObject != null)
            {
                foreach (Component component in cinemachineObject.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().FullName ?? string.Empty;
                    if (typeName.EndsWith(".CinemachineFollow", StringComparison.Ordinal))
                    {
                        cinemachineFollowComponent = component;
                        SetMember(component, "FollowOffset", PresentationCameraOffset);
                        EditorUtility.SetDirty(component);
                    }
                    else if (typeName.EndsWith(".CinemachineCamera", StringComparison.Ordinal))
                    {
                        SetNestedMember(component, "Lens", "FieldOfView", PresentationFieldOfView);
                        EditorUtility.SetDirty(component);
                    }
                }
            }

            if (targetRig != null)
            {
                targetRig.ConfigureCameraFollowOffset(cinemachineFollowComponent, PresentationCameraOffset);
                targetRig.SnapAimTargetToSettings();
                if (targetRig.AimTarget != null)
                {
                    EditorUtility.SetDirty(targetRig.AimTarget);
                }

                EditorUtility.SetDirty(targetRig);
            }
        }

        private static GameObject FindNamedObject(string objectName)
        {
            foreach (GameObject gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
            {
                if (gameObject.name == objectName)
                {
                    return gameObject;
                }
            }

            return null;
        }

        private static T FindComponentOnNamedObject<T>(string objectName) where T : Component
        {
            GameObject gameObject = FindNamedObject(objectName);
            return gameObject != null ? gameObject.GetComponent<T>() : null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void SetMember(object target, string memberName, object value)
        {
            Type type = target.GetType();
            var property = type.GetProperty(memberName);
            if (property != null && property.CanWrite && TryPrepareValue(value, property.PropertyType, out object propertyValue))
            {
                property.SetValue(target, propertyValue);
                return;
            }

            var field = type.GetField(memberName);
            if (field != null && TryPrepareValue(value, field.FieldType, out object fieldValue))
            {
                field.SetValue(target, fieldValue);
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

        private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.vector3Value = value;
            }
        }

        private static void SetNestedMember(object target, string fieldName, string memberName, object value)
        {
            Type type = target.GetType();
            var field = type.GetField(fieldName);
            if (field == null)
            {
                return;
            }

            object nestedValue = field.GetValue(target);
            SetMember(nestedValue, memberName, value);
            field.SetValue(target, nestedValue);
        }

        private static bool TryPrepareValue(object value, Type targetType, out object preparedValue)
        {
            if (value == null)
            {
                preparedValue = null;
                return !targetType.IsValueType;
            }

            Type valueType = value.GetType();
            if (targetType.IsAssignableFrom(valueType))
            {
                preparedValue = value;
                return true;
            }

            if (targetType.IsEnum && value is int intValue)
            {
                preparedValue = Enum.ToObject(targetType, intValue);
                return true;
            }

            preparedValue = null;
            return false;
        }

        private static void SetBuildScene(string scenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true),
            };
        }
    }
}
