using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    [InitializeOnLoad]
    public static class SkyCircuitV09CloudSeaRaceSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V0_9_CloudSeaRacePrototype.unity";
        private const string CloudSeaSourceScenePath = "Assets/Scenes/V0_7_CloudSea.unity";
        private const string CompetitionSourceScenePath = "Assets/Scenes/V0_6_PresentationSlice.unity";
        private const string SceneRevisionMarker = "Cloud Sea Race Scene Revision 3";
        private const string AutoBuildSessionKey = "SkyCircuit.V09.CloudSeaRace.AutoBuildQueued.v4";
        private const float CloudSeaFarClipPlane = 20000f;
        private const float CompetitionTargetHeight = 200f;
        private const float CompetitionSourceBaseHeight = 18f;
        private const string CloudSeaSkyboxMaterialPath = "Assets/SkyCircuit/Art/Materials/SC_CloudSeaSkybox.mat";
        private const string DebugHudTitle = "Sky Circuit V0.9 Cloud Sea Race Prototype";

        static SkyCircuitV09CloudSeaRaceSceneBuilder()
        {
            if (Application.isBatchMode || SessionState.GetBool(AutoBuildSessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoBuildScene;
        }

        [DidReloadScripts]
        private static void QueueRefreshAfterReload()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            SessionState.SetBool(AutoBuildSessionKey, false);
            EditorApplication.delayCall += TryAutoBuildScene;
        }

        [MenuItem("Sky Circuit/Build V0.9 Cloud Sea Race Prototype")]
        public static void BuildScene()
        {
            if (!File.Exists(CloudSeaSourceScenePath))
            {
                Debug.LogWarning($"Cloud sea source scene not found at {CloudSeaSourceScenePath}.");
                return;
            }

            if (!File.Exists(CompetitionSourceScenePath))
            {
                Debug.LogWarning($"Competition source scene not found at {CompetitionSourceScenePath}.");
                return;
            }

            Scene destinationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            destinationScene.name = "V0_9_CloudSeaRacePrototype";

            int cloudRootCount = MoveRootsFromScene(CloudSeaSourceScenePath, destinationScene, ShouldMoveCloudSeaRoot);
            int competitionRootCount = MoveRootsFromScene(CompetitionSourceScenePath, destinationScene, ShouldMoveCompetitionRoot);

            if (cloudRootCount == 0 || competitionRootCount == 0)
            {
                Debug.LogWarning($"V0.9 scene build incomplete. Cloud roots: {cloudRootCount}, competition roots: {competitionRootCount}.");
            }

            ApplyCompetitionHeightOffset(destinationScene);
            CreateRevisionMarker(destinationScene);
            ConfigureCloudSeaRenderSettings();
            ConfigureCloudSeaCameraClipping();
            ConfigureDebugHudTitle();

            EditorSceneManager.SaveScene(destinationScene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.9 cloud sea race scene built at {ScenePath}");
        }

        private static void TryAutoBuildScene()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoBuildScene;
                return;
            }

            if (SessionState.GetBool(AutoBuildSessionKey, false))
            {
                return;
            }

            if (IsCurrentSceneBuilt())
            {
                SessionState.SetBool(AutoBuildSessionKey, true);
                return;
            }

            SessionState.SetBool(AutoBuildSessionKey, true);
            try
            {
                BuildScene();
            }
            catch
            {
                SessionState.SetBool(AutoBuildSessionKey, false);
                throw;
            }
        }

        private static bool IsCurrentSceneBuilt()
        {
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            string sceneText = File.ReadAllText(ScenePath);
            return sceneText.Contains(SceneRevisionMarker)
                && sceneText.Contains("Cloud Sea Surface")
                && sceneText.Contains("Horizon Blend Curtain")
                && sceneText.Contains("Gameplay")
                && sceneText.Contains("Match Controller")
                && sceneText.Contains("Player Competitor")
                && sceneText.Contains("AI Competitor")
                && sceneText.Contains("Cinemachine Follow Camera")
                && sceneText.Contains("m_LocalPosition: {x: 0, y: -0.0000705719, z: -58}")
                && sceneText.Contains("m_LocalPosition: {x: 48, y: 5.9999294, z: 48}")
                && sceneText.Contains("far clip plane: 20000")
                && sceneText.Contains("FarClipPlane: 20000")
                && sceneText.Contains("m_SkyboxMaterial: {fileID: 2100000, guid: 880c97565e7b19349872e98d8956b11a, type: 2}")
                && sceneText.Contains("m_AmbientSkyColor: {r: 0.86, g: 0.92, b: 0.98, a: 1}");
        }

        private static int MoveRootsFromScene(string sourceScenePath, Scene destinationScene, System.Func<GameObject, bool> shouldMoveRoot)
        {
            Scene sourceScene = EditorSceneManager.OpenScene(sourceScenePath, OpenSceneMode.Additive);
            int movedRootCount = 0;

            try
            {
                GameObject[] rootObjects = sourceScene.GetRootGameObjects();
                foreach (GameObject rootObject in rootObjects)
                {
                    if (!shouldMoveRoot(rootObject))
                    {
                        continue;
                    }

                    SceneManager.MoveGameObjectToScene(rootObject, destinationScene);
                    movedRootCount++;
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }

            return movedRootCount;
        }

        private static bool ShouldMoveCloudSeaRoot(GameObject rootObject)
        {
            if (rootObject == null)
            {
                return false;
            }

            return rootObject.name == "Environment" || rootObject.name == "Sun";
        }

        private static bool ShouldMoveCompetitionRoot(GameObject rootObject)
        {
            return rootObject != null && rootObject.name != "Environment";
        }

        private static void CreateRevisionMarker(Scene destinationScene)
        {
            GameObject marker = new GameObject(SceneRevisionMarker);
            GameObject environmentRoot = FindRoot(destinationScene, "Environment");

            if (environmentRoot != null)
            {
                marker.transform.SetParent(environmentRoot.transform);
            }
        }

        private static void ApplyCompetitionHeightOffset(Scene destinationScene)
        {
            GameObject gameplayRoot = FindRoot(destinationScene, "Gameplay");
            if (gameplayRoot == null)
            {
                Debug.LogWarning("V0.9 scene build could not find Gameplay root to align competition height.");
                return;
            }

            float heightDelta = CompetitionTargetHeight - CompetitionSourceBaseHeight;
            SetRootHeight(gameplayRoot, 0f);
            ShiftNamedChildLocalHeight(gameplayRoot.transform, "Player Spawn", heightDelta);
            ShiftNamedChildLocalHeight(gameplayRoot.transform, "AI Spawn", heightDelta);
            ShiftNamedChildLocalHeight(gameplayRoot.transform, "Player Competitor", heightDelta);
            ShiftNamedChildLocalHeight(gameplayRoot.transform, "AI Competitor", heightDelta);
            ShiftNamedChildLocalHeight(gameplayRoot.transform, "Camera Target Rig", heightDelta);
            ShiftBuoyHeights(gameplayRoot.transform, heightDelta);
            ShiftRouteLineRendererPositions(gameplayRoot.transform, heightDelta);
            MoveRootByHeightDelta(destinationScene, "Main Camera", heightDelta);
            MoveRootByHeightDelta(destinationScene, "Cinemachine Follow Camera", heightDelta);
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == rootName)
                {
                    return rootObject;
                }
            }

            return null;
        }

        private static void SetRootHeight(GameObject rootObject, float height)
        {
            Vector3 position = rootObject.transform.position;
            position.y = height;
            rootObject.transform.position = position;
            EditorUtility.SetDirty(rootObject.transform);
        }

        private static void ShiftNamedChildLocalHeight(Transform parent, string childName, float heightDelta)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                return;
            }

            ShiftLocalHeight(child, heightDelta);
        }

        private static void ShiftBuoyHeights(Transform gameplayRoot, float heightDelta)
        {
            Transform buoysRoot = gameplayRoot.Find("Buoys");
            if (buoysRoot == null)
            {
                return;
            }

            foreach (Transform buoy in buoysRoot)
            {
                ShiftLocalHeight(buoy, heightDelta);
            }
        }

        private static void ShiftRouteLineRendererPositions(Transform gameplayRoot, float heightDelta)
        {
            Transform routeLine = gameplayRoot.Find("Route Line");
            if (routeLine == null || !routeLine.TryGetComponent(out LineRenderer lineRenderer))
            {
                return;
            }

            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                Vector3 position = lineRenderer.GetPosition(i);
                position.y += heightDelta;
                lineRenderer.SetPosition(i, position);
            }

            EditorUtility.SetDirty(lineRenderer);
        }

        private static void ShiftLocalHeight(Transform transform, float heightDelta)
        {
            Vector3 position = transform.localPosition;
            position.y += heightDelta;
            transform.localPosition = position;
            EditorUtility.SetDirty(transform);
        }

        private static void MoveRootByHeightDelta(Scene scene, string rootName, float heightDelta)
        {
            GameObject rootObject = FindRoot(scene, rootName);
            if (rootObject == null)
            {
                return;
            }

            Vector3 position = rootObject.transform.position;
            position.y += heightDelta;
            rootObject.transform.position = position;
            EditorUtility.SetDirty(rootObject.transform);
        }

        private static void ConfigureCloudSeaRenderSettings()
        {
            RenderSettings.fog = false;
            RenderSettings.fogColor = new Color(0.58f, 0.72f, 0.86f, 1f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogDensity = 0.01f;
            RenderSettings.fogStartDistance = 3200f;
            RenderSettings.fogEndDistance = 7000f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.92f, 0.98f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.8f, 0.86f, 0.92f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.72f, 0.8f, 0.86f, 1f);
            RenderSettings.subtractiveShadowColor = new Color(0.42f, 0.478f, 0.627f, 1f);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(CloudSeaSkyboxMaterialPath);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.reflectionBounces = 1;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.sun = FindSunLight();
        }

        private static Light FindSunLight()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional && light.gameObject.name == "Sun")
                {
                    return light;
                }
            }

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }

        private static void ConfigureDebugHudTitle()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().FullName ?? string.Empty;
                    if (!typeName.EndsWith(".MatchDebugHud", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SerializedObject serializedHud = new SerializedObject(component);
                    SerializedProperty titleProperty = serializedHud.FindProperty("title");
                    if (titleProperty == null)
                    {
                        continue;
                    }

                    titleProperty.stringValue = DebugHudTitle;
                    serializedHud.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(component);
                }
            }
        }

        private static void ConfigureCloudSeaCameraClipping()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                camera.farClipPlane = CloudSeaFarClipPlane;
                EditorUtility.SetDirty(camera);
            }

            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                foreach (Component component in gameObject.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().FullName ?? string.Empty;
                    if (!typeName.EndsWith(".CinemachineCamera", System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SerializedObject serializedCamera = new SerializedObject(component);
                    SerializedProperty farClipProperty = serializedCamera.FindProperty("Lens.FarClipPlane");
                    if (farClipProperty == null)
                    {
                        continue;
                    }

                    farClipProperty.floatValue = CloudSeaFarClipPlane;
                    serializedCamera.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(component);
                }
            }
        }
    }
}
