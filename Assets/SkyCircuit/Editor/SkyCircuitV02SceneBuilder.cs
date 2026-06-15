using System;
using System.IO;
using SkyCircuit.AI;
using SkyCircuit.CameraRigging;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Practice;
using SkyCircuit.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitV02SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V0_2_MatchPrototype.unity";
        private const string MaterialsFolder = "Assets/SkyCircuit/Art/Materials";

        [MenuItem("Sky Circuit/Build V0.2 Match Prototype Scene")]
        public static void BuildMatchPrototypeScene()
        {
            EnsureFolders();

            Material playerMaterial = CreateMaterial("SC_PlayerPrototype.mat", new Color(0.12f, 0.78f, 1f), new Color(0.1f, 0.55f, 1f));
            Material aiMaterial = CreateMaterial("SC_AIPrototype.mat", new Color(1f, 0.28f, 0.24f), new Color(0.85f, 0.05f, 0.05f));
            Material waterMaterial = CreateMaterial("SC_WaterPrototype.mat", new Color(0.02f, 0.34f, 0.55f, 0.72f), new Color(0.02f, 0.13f, 0.22f), true);
            Material routeLineMaterial = CreateMaterial("SC_RouteLine.mat", new Color(0.1f, 0.9f, 1f), new Color(0.1f, 0.9f, 1f));
            Material buoyIdleMaterial = CreateMaterial("SC_BuoyIdle.mat", new Color(0.18f, 0.45f, 1f), new Color(0.04f, 0.18f, 0.65f));
            Material buoyTargetMaterial = CreateMaterial("SC_BuoyTarget.mat", new Color(1f, 0.82f, 0.16f), new Color(1f, 0.55f, 0.08f));
            Material buoyCompletedMaterial = CreateMaterial("SC_BuoyCompleted.mat", new Color(0.18f, 1f, 0.55f), new Color(0.05f, 0.65f, 0.25f));
            Material touchRangeMaterial = CreateMaterial("SC_TouchRange.mat", new Color(1f, 1f, 1f, 0.08f), Color.clear, true);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "V0_2_MatchPrototype";

            GameObject environmentRoot = new GameObject("Environment");
            GameObject gameplayRoot = new GameObject("Gameplay");
            GameObject matchRoot = new GameObject("Match");

            CreateLighting(environmentRoot.transform);
            CreateWater(environmentRoot.transform, waterMaterial);

            Transform playerSpawn = CreateSpawnPoint(gameplayRoot.transform, "Player Spawn", new Vector3(0f, 18f, -58f));
            Transform aiSpawn = CreateSpawnPoint(gameplayRoot.transform, "AI Spawn", new Vector3(-10f, 18f, -58f));
            CompetitorSetup player = CreateCompetitor(
                gameplayRoot.transform,
                "Player Competitor",
                "Player",
                true,
                playerSpawn,
                playerMaterial,
                true);
            CompetitorSetup ai = CreateCompetitor(
                gameplayRoot.transform,
                "AI Competitor",
                "AI",
                false,
                aiSpawn,
                aiMaterial,
                false);

            Transform[] buoys = CreateBuoys(gameplayRoot.transform, buoyIdleMaterial, touchRangeMaterial);
            LineRenderer routeLine = CreateRouteLine(gameplayRoot.transform, routeLineMaterial, buoys);
            BuoyRoute route = CreateBuoyRoute(matchRoot.transform, buoys, routeLine, buoyIdleMaterial, buoyTargetMaterial, buoyCompletedMaterial);

            MatchController match = CreateMatchController(matchRoot.transform, route, player.Competitor, ai.Competitor);
            route.RefreshPlayerTargetVisual(player.Competitor);

            RouteAIPilotController pilot = ai.GameObject.AddComponent<RouteAIPilotController>();
            pilot.Configure(ai.Controller, ai.Competitor, route, match);

            FlightCameraTargetRig cameraTargetRig = CreateCameraTargetRig(gameplayRoot.transform, player.GameObject.transform, player.Controller);
            CreateCamera(cameraTargetRig.transform, cameraTargetRig.AimTarget);
            CreateHud(matchRoot.transform, match);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.2 match prototype scene built at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Art");
            CreateFolder("Assets/SkyCircuit/Art", "Materials");
            CreateFolder("Assets", "Scenes");
            SkyCircuitCharacterVisualSceneUtility.EnsureFolders();
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material CreateMaterial(string fileName, Color baseColor, Color emissionColor, bool transparent = false)
        {
            string path = $"{MaterialsFolder}/{fileName}";
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

        private static void CreateLighting(Transform parent)
        {
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(parent);
            sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.96f, 0.88f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.62f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.38f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.09f, 0.12f);
        }

        private static void CreateWater(Transform parent, Material waterMaterial)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water Plane";
            water.transform.SetParent(parent);
            water.transform.position = Vector3.zero;
            water.transform.localScale = new Vector3(26f, 1f, 26f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
        }

        private static Transform CreateSpawnPoint(Transform parent, string name, Vector3 position)
        {
            GameObject spawn = new GameObject(name);
            spawn.transform.SetParent(parent);
            spawn.transform.SetPositionAndRotation(position, Quaternion.identity);
            return spawn.transform;
        }

        private static CompetitorSetup CreateCompetitor(
            Transform parent,
            string objectName,
            string displayName,
            bool isPlayer,
            Transform spawnPoint,
            Material bodyMaterial,
            bool addPlayerInput)
        {
            GameObject competitorObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            competitorObject.name = objectName;
            competitorObject.transform.SetParent(parent);
            competitorObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            competitorObject.transform.localScale = new Vector3(1f, 1.7f, 1f);
            competitorObject.GetComponent<Renderer>().sharedMaterial = bodyMaterial;

            Rigidbody body = competitorObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;

            Collider collider = competitorObject.GetComponent<Collider>();
            if (!isPlayer && collider != null)
            {
                collider.isTrigger = true;
            }

            SkyCircuitFlightController controller = competitorObject.AddComponent<SkyCircuitFlightController>();
            ConfigurePrototypeFlight(controller);
            if (addPlayerInput)
            {
                competitorObject.AddComponent<PlayerFlightInput>();
            }

            FlightResetVolume reset = competitorObject.AddComponent<FlightResetVolume>();
            reset.Configure(controller, spawnPoint);

            Competitor competitor = competitorObject.AddComponent<Competitor>();
            competitor.Configure(displayName, isPlayer, controller, spawnPoint);

            string visualName = isPlayer ? "Player Character Visual" : "AI Character Visual";
            SkyCircuitCharacterVisualSceneUtility.EnsureCharacterVisual(competitorObject, visualName);
            CreateContrail(competitorObject.transform, controller);
            return new CompetitorSetup(competitorObject, controller, competitor);
        }

        private static void ConfigurePrototypeFlight(SkyCircuitFlightController controller)
        {
            var serializedController = new SerializedObject(controller);
            SetFloat(serializedController, "mouseYawSensitivity", 0.22f);
            SetFloat(serializedController, "mousePitchSensitivity", 0.18f);
            SetFloat(serializedController, "keyboardYawRate", 115f);
            SetFloat(serializedController, "maxPitch", 58f);
            SetFloat(serializedController, "maxBank", 46f);
            SetFloat(serializedController, "rotationSharpness", 26f);

            SerializedProperty speedModule = serializedController.FindProperty("speedModule");
            if (speedModule != null)
            {
                SetFloat(speedModule, "minSpeed", 0f);
                SetFloat(speedModule, "cruiseSpeed", 24f);
                SetFloat(speedModule, "poweredMaxSpeed", 42f);
                SetFloat(speedModule, "absoluteMaxSpeed", 58f);
                SetFloat(speedModule, "acceleration", 22f);
                SetFloat(speedModule, "deceleration", 30f);
                SetFloat(speedModule, "highSpeedAccelerationScale", 0.18f);
                SetFloat(speedModule, "overspeedReturnRate", 10f);
                SetFloat(speedModule, "velocitySharpness", 8f);
                SetFloat(speedModule, "verticalAssistSpeed", 17f);
                SetFloat(speedModule, "gravityEnergy", 9.8f);
                SetFloat(speedModule, "climbEfficiency", 0.9f);
                SetFloat(speedModule, "diveEfficiency", 0.8f);
                SetFloat(speedModule, "turnLossReferenceRate", 140f);
                SetFloat(speedModule, "turnSpeedLossRate", 0.18f);
                SetFloat(speedModule, "turnLossMinSpeed", 4f);
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetFloat(SerializedProperty parent, string propertyName, float value)
        {
            SerializedProperty property = parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void CreateContrail(Transform parent, SkyCircuitFlightController controller)
        {
            GameObject trailObject = new GameObject("Contrail");
            trailObject.transform.SetParent(parent);
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

            FlightContrailFeedback feedback = parent.gameObject.AddComponent<FlightContrailFeedback>();
            feedback.Configure(controller, trail);
        }

        private static Transform[] CreateBuoys(Transform parent, Material idleMaterial, Material touchRangeMaterial)
        {
            Vector3[] positions =
            {
                new Vector3(48f, 20f, -48f),
                new Vector3(48f, 24f, 48f),
                new Vector3(-48f, 20f, 48f),
                new Vector3(-48f, 24f, -48f),
            };

            Transform[] buoys = new Transform[positions.Length];
            GameObject root = new GameObject("Buoys");
            root.transform.SetParent(parent);

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                buoy.name = $"Buoy {i + 1}";
                buoy.transform.SetParent(root.transform);
                buoy.transform.position = positions[i];
                buoy.transform.localScale = Vector3.one * 4f;
                buoy.GetComponent<Renderer>().sharedMaterial = idleMaterial;
                buoy.AddComponent<BuoyMarkerPulse>();

                Light light = buoy.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 16f;
                light.intensity = 1.5f;
                light.color = new Color(0.2f, 0.7f, 1f);

                GameObject touchRange = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                touchRange.name = "Touch Radius";
                touchRange.transform.SetParent(buoy.transform);
                touchRange.transform.localPosition = Vector3.zero;
                touchRange.transform.localScale = Vector3.one * 3f;
                touchRange.GetComponent<Renderer>().sharedMaterial = touchRangeMaterial;
                UnityEngine.Object.DestroyImmediate(touchRange.GetComponent<Collider>());

                buoys[i] = buoy.transform;
            }

            return buoys;
        }

        private static LineRenderer CreateRouteLine(Transform parent, Material material, Transform[] buoys)
        {
            GameObject lineObject = new GameObject("Route Line");
            lineObject.transform.SetParent(parent);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.loop = false;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.22f;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.material = material;
            line.startColor = new Color(0.15f, 0.9f, 1f, 0.8f);
            line.endColor = new Color(0.15f, 0.9f, 1f, 0.8f);
            line.positionCount = buoys.Length + 1;

            for (int i = 0; i < buoys.Length; i++)
            {
                line.SetPosition(i, buoys[i].position);
            }

            line.SetPosition(buoys.Length, buoys[0].position);
            return line;
        }

        private static BuoyRoute CreateBuoyRoute(
            Transform parent,
            Transform[] buoys,
            LineRenderer routeLine,
            Material idleMaterial,
            Material targetMaterial,
            Material completedMaterial)
        {
            GameObject routeObject = new GameObject("Buoy Route");
            routeObject.transform.SetParent(parent);
            BuoyRoute route = routeObject.AddComponent<BuoyRoute>();
            route.Configure(buoys, routeLine, idleMaterial, targetMaterial, completedMaterial);
            return route;
        }

        private static MatchController CreateMatchController(
            Transform parent,
            BuoyRoute route,
            Competitor player,
            Competitor opponent)
        {
            GameObject matchObject = new GameObject("Match Controller");
            matchObject.transform.SetParent(parent);
            MatchController match = matchObject.AddComponent<MatchController>();
            match.Configure(route, player, opponent);
            return match;
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
            return rig;
        }

        private static void CreateCamera(Transform followTarget, Transform lookTarget)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();

            if (!TryCreateCinemachineCamera(cameraObject, followTarget, lookTarget))
            {
                SkyCircuitCameraRig rig = cameraObject.AddComponent<SkyCircuitCameraRig>();
                rig.SetTargets(followTarget, lookTarget);
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
            SetNestedMember(virtualCamera, "Lens", "FieldOfView", 72f);

            if (followType != null)
            {
                Component follow = cinemachineObject.AddComponent(followType);
                SetNestedMember(follow, "TrackerSettings", "BindingMode", 2);
                SetNestedMember(follow, "TrackerSettings", "PositionDamping", Vector3.zero);
                SetNestedMember(follow, "TrackerSettings", "RotationDamping", Vector3.zero);
                SetNestedMember(follow, "TrackerSettings", "QuaternionDamping", 0f);
                SetMember(follow, "FollowOffset", new Vector3(0f, 5.2f, -14f));
            }

            if (rotationComposerType != null)
            {
                Component rotationComposer = cinemachineObject.AddComponent(rotationComposerType);
                SetMember(rotationComposer, "Damping", Vector2.zero);
            }

            return true;
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

        private static void CreateHud(Transform parent, MatchController match)
        {
            GameObject hudObject = new GameObject("Match HUD");
            hudObject.transform.SetParent(parent);
            MatchDebugHud hud = hudObject.AddComponent<MatchDebugHud>();
            hud.Configure(match);
        }

        private static void SetBuildScene(string scenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true),
            };
        }

        private readonly struct CompetitorSetup
        {
            public readonly GameObject GameObject;
            public readonly SkyCircuitFlightController Controller;
            public readonly Competitor Competitor;

            public CompetitorSetup(GameObject gameObject, SkyCircuitFlightController controller, Competitor competitor)
            {
                GameObject = gameObject;
                Controller = controller;
                Competitor = competitor;
            }
        }
    }
}
