using System;
using SkyCircuit.Networking;
using SkyCircuit.Flight;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitV08LanMultiplayerSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V0_8_LanMultiplayerPrototype.unity";
        private const string SettingsFolder = "Assets/SkyCircuit/Networking";
        private const string SettingsAssetPath = SettingsFolder + "/SC_LanConnectionSettings.asset";
        private const string PlayerPrefabPath = SettingsFolder + "/SC_LanFlightPlayer.prefab";
        private const string PlayerMaterialPath = SettingsFolder + "/SC_LanFlightPlayer.mat";

        [MenuItem("Sky Circuit/Legacy Scene Builders/Build V0.8 LAN Multiplayer Prototype Scene")]
        public static void BuildLanMultiplayerPrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot build the V0.8 LAN multiplayer prototype scene while Unity is in Play Mode.");
                return;
            }

            EnsureFolders();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "V0_8_LanMultiplayerPrototype";

            LanConnectionSettings settings = EnsureSettingsAsset();
            GameObject playerPrefab = EnsurePlayerPrefab();
            CreateEnvironment();
            CreateNetworkRig(settings, playerPrefab);

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.8 LAN multiplayer prototype scene built at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Scenes");
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Networking");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static LanConnectionSettings EnsureSettingsAsset()
        {
            LanConnectionSettings settings = AssetDatabase.LoadAssetAtPath<LanConnectionSettings>(SettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<LanConnectionSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void CreateEnvironment()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.07f, 0.11f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 250f;
            camera.fieldOfView = 72f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 26f, -72f), Quaternion.Euler(18f, 0f, 0f));
            cameraObject.AddComponent<AudioListener>();

            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1f;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LAN Connection Test Marker";
            marker.transform.position = Vector3.zero;
            marker.transform.localScale = new Vector3(46f, 0.18f, 46f);
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateMarkerMaterial();
            }
        }

        private static Material CreateMarkerMaterial()
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "SC_LanConnectionTestMarker";
            material.SetColor("_BaseColor", new Color(0.08f, 0.72f, 0.88f));
            material.SetColor("_EmissionColor", new Color(0.02f, 0.25f, 0.32f));
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static GameObject EnsurePlayerPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            Material material = EnsurePlayerMaterial();
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "SC_LanFlightPlayer";
            player.transform.localScale = new Vector3(1f, 1.7f, 1f);
            Renderer renderer = player.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Rigidbody body = player.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = 1f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            player.AddComponent<NetworkObject>();
            NetworkTransform networkTransform = player.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = true;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.SyncScaleX = false;
            networkTransform.SyncScaleY = false;
            networkTransform.SyncScaleZ = false;

            player.AddComponent<SkyCircuitFlightController>();
            Type inputBridgeType = FindType("SkyCircuit.Networking.NetworkFlightInputBridge");
            if (inputBridgeType != null)
            {
                player.AddComponent(inputBridgeType);
            }
            else
            {
                Debug.LogError("Could not find SkyCircuit.Networking.NetworkFlightInputBridge while creating LAN player prefab.");
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return prefab;
        }

        private static Material EnsurePlayerMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PlayerMaterialPath);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "SC_LanFlightPlayer";
            material.SetColor("_BaseColor", new Color(0.12f, 0.78f, 1f));
            material.SetColor("_EmissionColor", new Color(0.02f, 0.28f, 0.4f));
            material.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, PlayerMaterialPath);
            return material;
        }

        private static void CreateNetworkRig(LanConnectionSettings settings, GameObject playerPrefab)
        {
            GameObject networkObject = new GameObject("LAN Network Rig");
            UnityTransport transport = networkObject.AddComponent<UnityTransport>();
            transport.SetConnectionData(settings.DefaultHostAddress, settings.Port, "0.0.0.0");
            transport.ConnectTimeoutMS = Mathf.RoundToInt(settings.ConnectTimeoutSeconds * 1000f);

            NetworkManager networkManager = networkObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                TickRate = (uint)Mathf.RoundToInt(settings.StateSendRate),
                ClientConnectionBufferTimeout = Mathf.CeilToInt(settings.ConnectTimeoutSeconds),
                ConnectionApproval = true,
                PlayerPrefab = playerPrefab,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                AutoSpawnPlayerPrefabClientSide = false,
            };

            LanNetworkBootstrap bootstrap = networkObject.AddComponent<LanNetworkBootstrap>();
            LanNetworkStatusHud hud = networkObject.AddComponent<LanNetworkStatusHud>();
            ConfigureBootstrap(bootstrap, settings, networkManager, transport);
            ConfigureHud(hud, bootstrap, settings);
        }

        private static void ConfigureBootstrap(
            LanNetworkBootstrap bootstrap,
            LanConnectionSettings settings,
            NetworkManager networkManager,
            UnityTransport transport)
        {
            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("networkManager").objectReferenceValue = networkManager;
            serialized.FindProperty("transport").objectReferenceValue = transport;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static void ConfigureHud(
            LanNetworkStatusHud hud,
            LanNetworkBootstrap bootstrap,
            LanConnectionSettings settings)
        {
            SerializedObject serialized = new SerializedObject(hud);
            serialized.FindProperty("bootstrap").objectReferenceValue = bootstrap;
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);
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

        private static void SetBuildScene(string scenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true),
            };
        }
    }
}
