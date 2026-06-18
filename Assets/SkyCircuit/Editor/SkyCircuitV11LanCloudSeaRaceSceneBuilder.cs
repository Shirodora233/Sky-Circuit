using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SkyCircuit.AI;
using SkyCircuit.CameraRigging;
using SkyCircuit.Combat;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Networking;
using SkyCircuit.Presentation;
using SkyCircuit.Profiles;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitV11LanCloudSeaRaceSceneBuilder
    {
        private const string SourceScenePath = "Assets/Scenes/V0_9_CloudSeaRacePrototype.unity";
        private const string ScenePath = "Assets/SkyCircuit/Scenes/CloudSeaRace.unity";
        private const string SettingsFolder = "Assets/SkyCircuit/Networking";
        private const string RacePlayerPrefabPath = SettingsFolder + "/SC_LanRaceFlightPlayer.prefab";
        private const string ContrailTrailMaterialPath = SettingsFolder + "/SC_ContrailTrail.mat";
        private const string RevisionMarkerName = "LAN Cloud Sea Race Scene Revision 4";
        private static readonly MethodInfo NetworkObjectOnValidate =
            typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Sky Circuit/Build Cloud Sea Race Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot build the cloud sea race scene while Unity is in Play Mode.");
                return;
            }

            if (!File.Exists(SourceScenePath))
            {
                Debug.LogWarning($"Cloud sea race source scene not found at {SourceScenePath}.");
                return;
            }

            EnsureFolders();

            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            GameObject gameplayRoot = FindRoot(scene, "Gameplay");
            if (gameplayRoot == null)
            {
                Debug.LogWarning("Cloud sea race scene build could not find Gameplay root.");
                return;
            }

            Transform playerSpawn = FindChildRecursive(gameplayRoot.transform, "Player Spawn");
            Transform opponentSpawn = FindChildRecursive(gameplayRoot.transform, "AI Spawn");
            BuoyRoute route = FindComponentInScene<BuoyRoute>(scene);
            GameObject playerTemplate = FindChildRecursive(gameplayRoot.transform, "Player Competitor")?.gameObject;

            GameObject racePlayerPrefab = EnsureRacePlayerPrefab(playerTemplate);
            if (racePlayerPrefab == null)
            {
                Debug.LogWarning("Cloud sea race scene build could not create or load a LAN race player prefab.");
                return;
            }

            RemoveSinglePlayerActors(gameplayRoot.transform);
            RemoveSinglePlayerSystems(scene);
            ConfigureV09Camera(scene, gameplayRoot.transform);
            CreateRevisionMarker(scene);
            LanRaceSessionController session = CreateRaceSession(racePlayerPrefab, route, playerSpawn, opponentSpawn);
            ConfigureLanPresentation(scene, session, route);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RefreshNetworkObjectHashes(scene, racePlayerPrefab);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit cloud sea race scene built at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Scenes");
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Networking");
            CreateFolder("Assets/SkyCircuit", "Scenes");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static GameObject EnsureRacePlayerPrefab(GameObject playerTemplate)
        {
            if (playerTemplate == null)
            {
                GameObject fallback = AssetDatabase.LoadAssetAtPath<GameObject>(RacePlayerPrefabPath);
                RefreshNetworkObjectHash(fallback != null ? fallback.GetComponent<NetworkObject>() : null);
                AssetDatabase.SaveAssets();
                return fallback;
            }

            GameObject prefabInstance = UnityEngine.Object.Instantiate(playerTemplate);
            prefabInstance.name = "SC_LanRaceFlightPlayer";
            prefabInstance.SetActive(true);

            RemoveComponent<PlayerFlightInput>(prefabInstance);
            RemoveComponent<PlayerProfileSwitcher>(prefabInstance);
            RemoveComponent<RouteAIPilotController>(prefabInstance);

            EnsureComponent<Rigidbody>(prefabInstance);

            SkyCircuitFlightController controller = EnsureComponent<SkyCircuitFlightController>(prefabInstance);
            Competitor competitor = EnsureComponent<Competitor>(prefabInstance);
            ConfigureCompetitorTemplate(competitor, controller);

            EnsureComponent<NetworkObject>(prefabInstance);
            NetworkTransform networkTransform = EnsureComponent<NetworkTransform>(prefabInstance);
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.Interpolate = true;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            NetworkFlightInputBridge inputBridge = EnsureComponent<NetworkFlightInputBridge>(prefabInstance);
            ConfigureInputBridge(inputBridge);
            ConfigureContrailTrails(prefabInstance);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabInstance, RacePlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(prefabInstance);
            AssetDatabase.ImportAsset(RacePlayerPrefabPath, ImportAssetOptions.ForceUpdate);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RacePlayerPrefabPath);
            RefreshNetworkObjectHash(prefab != null ? prefab.GetComponent<NetworkObject>() : null);
            if (prefab != null)
            {
                EditorUtility.SetDirty(prefab);
            }

            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void ConfigureCompetitorTemplate(Competitor competitor, SkyCircuitFlightController controller)
        {
            SerializedObject serialized = new SerializedObject(competitor);
            serialized.FindProperty("displayName").stringValue = "LAN Pilot";
            serialized.FindProperty("isPlayer").boolValue = true;
            serialized.FindProperty("body").objectReferenceValue = competitor.transform;
            serialized.FindProperty("controller").objectReferenceValue = controller;
            serialized.FindProperty("spawnPoint").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(competitor);
        }

        private static void ConfigureInputBridge(NetworkFlightInputBridge inputBridge)
        {
            SerializedObject serialized = new SerializedObject(inputBridge);
            serialized.FindProperty("bindOwnerCameraTargetRig").boolValue = true;
            serialized.FindProperty("logContrailDebug").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(inputBridge);
        }

        private static void ConfigureContrailTrails(GameObject prefabInstance)
        {
            Material contrailMaterial = EnsureContrailTrailMaterial();
            foreach (TrailRenderer trailRenderer in prefabInstance.GetComponentsInChildren<TrailRenderer>(true))
            {
                if (trailRenderer == null)
                {
                    continue;
                }

                if (contrailMaterial != null)
                {
                    trailRenderer.sharedMaterial = contrailMaterial;
                }

                trailRenderer.alignment = LineAlignment.View;
                trailRenderer.autodestruct = false;
                trailRenderer.minVertexDistance = 0.12f;
                trailRenderer.numCornerVertices = 2;
                trailRenderer.numCapVertices = 2;
                trailRenderer.textureMode = LineTextureMode.Stretch;
                EditorUtility.SetDirty(trailRenderer);
            }
        }

        private static Material EnsureContrailTrailMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ContrailTrailMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogWarning("Could not find a shader for the LAN contrail material.");
                return null;
            }

            material = new Material(shader)
            {
                name = "SC_ContrailTrail",
                color = Color.white
            };
            AssetDatabase.CreateAsset(material, ContrailTrailMaterialPath);
            AssetDatabase.ImportAsset(ContrailTrailMaterialPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Material>(ContrailTrailMaterialPath);
        }

        private static void RemoveSinglePlayerActors(Transform gameplayRoot)
        {
            DestroyChildIfPresent(gameplayRoot, "Player Competitor");
            DestroyChildIfPresent(gameplayRoot, "AI Competitor");
        }

        private static void RemoveSinglePlayerSystems(Scene scene)
        {
            DestroyComponents<MatchController>(scene);
            DestroyComponents<DogfightController>(scene);
            DestroyComponents<MatchDebugHud>(scene);
        }

        private static void ConfigureV09Camera(Scene scene, Transform gameplayRoot)
        {
            GameObject virtualCamera = FindRoot(scene, "Cinemachine Follow Camera");
            if (virtualCamera != null)
            {
                virtualCamera.SetActive(true);
                EditorUtility.SetDirty(virtualCamera);
            }

            Transform cameraTargetRig = FindChildRecursive(gameplayRoot, "Camera Target Rig");
            if (cameraTargetRig != null)
            {
                cameraTargetRig.gameObject.SetActive(true);
                EditorUtility.SetDirty(cameraTargetRig.gameObject);
            }

            Camera mainCamera = Camera.main ?? FindComponentInScene<Camera>(scene);
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, 20000f);
            EditorUtility.SetDirty(mainCamera);
        }

        private static LanRaceSessionController CreateRaceSession(
            GameObject playerPrefab,
            BuoyRoute route,
            Transform playerSpawn,
            Transform opponentSpawn)
        {
            GameObject sessionObject = new GameObject("LAN Race Session");
            sessionObject.AddComponent<NetworkObject>();
            LanRaceSessionController session = sessionObject.AddComponent<LanRaceSessionController>();

            SerializedObject serialized = new SerializedObject(session);
            serialized.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            serialized.FindProperty("route").objectReferenceValue = route;
            SetObjectArray(serialized.FindProperty("spawnPoints"), new UnityEngine.Object[] { playerSpawn, opponentSpawn });
            serialized.FindProperty("expectedPlayers").intValue = 2;
            serialized.FindProperty("countdownDuration").floatValue = 3f;
            serialized.FindProperty("matchDuration").floatValue = 180f;
            serialized.FindProperty("showHud").boolValue = false;
            serialized.FindProperty("showContrailDebug").boolValue = false;
            serialized.FindProperty("hudArea").rectValue = new Rect(18f, 18f, 520f, 245f);
            serialized.FindProperty("contrailDebugArea").rectValue = new Rect(18f, 272f, 850f, 190f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(session);
            return session;
        }

        private static void ConfigureLanPresentation(Scene scene, LanRaceSessionController session, BuoyRoute route)
        {
            Camera camera = Camera.main ?? FindComponentInScene<Camera>(scene);

            foreach (MatchScoreboardHud scoreboard in FindComponentsInScene<MatchScoreboardHud>(scene))
            {
                scoreboard.enabled = true;
                scoreboard.gameObject.SetActive(true);
                scoreboard.Configure(session);
                EditorUtility.SetDirty(scoreboard);
            }

            foreach (MatchWorldIndicator indicator in FindComponentsInScene<MatchWorldIndicator>(scene))
            {
                indicator.enabled = true;
                indicator.gameObject.SetActive(true);
                indicator.Configure(camera, session, route);
                EditorUtility.SetDirty(indicator);
            }

            foreach (MatchPlayerIndicator indicator in FindComponentsInScene<MatchPlayerIndicator>(scene))
            {
                indicator.enabled = true;
                indicator.gameObject.SetActive(true);
                indicator.Configure(camera, session);
                EditorUtility.SetDirty(indicator);
            }
        }

        private static void CreateRevisionMarker(Scene scene)
        {
            GameObject marker = new GameObject(RevisionMarkerName);
            GameObject environment = FindRoot(scene, "Environment");
            if (environment != null)
            {
                marker.transform.SetParent(environment.transform, false);
            }
        }

        private static void RefreshNetworkObjectHashes(Scene scene, GameObject playerPrefab)
        {
            RefreshNetworkObjectHash(playerPrefab != null ? playerPrefab.GetComponent<NetworkObject>() : null);

            foreach (NetworkObject networkObject in FindComponentsInScene<NetworkObject>(scene))
            {
                RefreshNetworkObjectHash(networkObject);
            }

            AssetDatabase.SaveAssets();
        }

        private static void RefreshNetworkObjectHash(NetworkObject networkObject)
        {
            if (networkObject == null)
            {
                return;
            }

            NetworkObjectOnValidate?.Invoke(networkObject, null);
            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(networkObject.gameObject);
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

        private static GameObject FindObjectInScene(Scene scene, string objectName)
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                Transform match = FindChildRecursive(rootObject.transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }

                if (rootObject.name == objectName)
                {
                    return rootObject;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                T component = rootObject.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static T[] FindComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> components = new List<T>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                components.AddRange(rootObject.GetComponentsInChildren<T>(true));
            }

            return components.ToArray();
        }

        private static void DestroyComponents<T>(Scene scene) where T : Component
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (T component in rootObject.GetComponentsInChildren<T>(true))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }
        }

        private static void DestroyChildIfPresent(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RemoveComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SetObjectArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            if (property == null || values == null)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
