using System;
using System.Collections.Generic;
using System.IO;
using SkyCircuit.Menu;
using SkyCircuit.Networking;
using SkyCircuit.Presentation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCircuit.EditorTools
{
    [InitializeOnLoad]
    public static class SkyCircuitV10MainMenuSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/V0_10_MainMenu.unity";
        private const string RaceScenePath = "Assets/SkyCircuit/Scenes/CloudSeaRace.unity";
        private const string OnlineCombatScenePath = RaceScenePath;
        private const string TrainingScenePath = RaceScenePath;
        private const string AnimationsFolder = "Assets/SkyCircuit/Art/Animations";
        private const string MaterialsFolder = "Assets/SkyCircuit/Art/Materials";
        private const string MenuArtFolder = "Assets/SkyCircuit/Art/Menu";
        private const string NetworkingFolder = "Assets/SkyCircuit/Networking";
        private const string LanConnectionSettingsPath = NetworkingFolder + "/SC_LanConnectionSettings.asset";
        private const string MainMenuIdleAnimationPath = AnimationsFolder + "/SC_MainMenuIdleLightweight.anim";
        private const string MainMenuIdleControllerPath = AnimationsFolder + "/SC_MainMenuIdle.controller";
        private const string LogoTexturePath = MenuArtFolder + "/SC_MainMenuLogo.png";
        private const string IconTexturePath = MenuArtFolder + "/SC_MainMenuIcons.png";
        private const string CombatPreviewTexturePath = MenuArtFolder + "/SC_MainMenuCombatPreviewBanner.jpg";
        private const string CombatTitleTexturePath = MenuArtFolder + "/SC_MainMenuCombatTitle.png";
        private const string TrainingTitleTexturePath = MenuArtFolder + "/SC_MainMenuTrainingTitle.png";
        private const string TutorialTitleTexturePath = MenuArtFolder + "/SC_MainMenuTutorialTitle.png";
        private const string SettingsTitleTexturePath = MenuArtFolder + "/SC_MainMenuSettingsTitle.png";
        private const string CloudSeaMeshPath = "Assets/SkyCircuit/Art/SC_CloudSeaSurface.asset";
        private const string FogRingMeshPath = "Assets/SkyCircuit/Art/SC_HeightFogRing.asset";
        private const string SceneRevisionMarker = "Main Menu Scene Revision 17";
        private const string AutoBuildSessionKey = "SkyCircuit.V10.MainMenu.AutoBuildQueued.v17";
        private const float CanvasScale = 0.00255f;

        static SkyCircuitV10MainMenuSceneBuilder()
        {
            if (Application.isBatchMode
                || EditorApplication.isPlayingOrWillChangePlaymode
                || SessionState.GetBool(AutoBuildSessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoBuildScene;
        }

        [DidReloadScripts]
        private static void QueueRefreshAfterReload()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            SessionState.SetBool(AutoBuildSessionKey, false);
            EditorApplication.delayCall += TryAutoBuildScene;
        }

        [MenuItem("Sky Circuit/Legacy Scene Builders/Build V0.10 Main Menu Scene")]
        public static void BuildMainMenuScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot build the V0.10 main menu scene while Unity is in Play Mode.");
                return;
            }

            EnsureFolders();
            ConfigureMenuTexture(LogoTexturePath, 2048);
            ConfigureMenuTexture(IconTexturePath, 1024);
            ConfigureMenuTexture(CombatPreviewTexturePath, 2048);
            ConfigureMenuTexture(CombatTitleTexturePath, 1024);
            ConfigureMenuTexture(TrainingTitleTexturePath, 1024);
            ConfigureMenuTexture(TutorialTitleTexturePath, 1024);
            ConfigureMenuTexture(SettingsTitleTexturePath, 1024);
            AnimatorController idleController = EnsureMainMenuIdleAnimatorController();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "V0_10_MainMenu";

            GameObject environmentRoot = new GameObject("Environment");
            GameObject characterRoot = new GameObject("Character Display");
            GameObject uiRoot = new GameObject("Main Menu UI");

            CreateRevisionMarker(environmentRoot.transform);
            ConfigureLightingAndSky();
            CreateEnvironment(environmentRoot.transform);
            Camera camera = CreateCamera();
            LanConnectionSettings lanSettings = EnsureLanConnectionSettings();
            LanNetworkBootstrap lanBootstrap = CreateLanNetworkRig(lanSettings);
            CreateFloatingCharacter(characterRoot.transform, camera, idleController);
            CreateMainMenuCanvas(uiRoot.transform, camera, lanBootstrap);
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.10 main menu scene built at {ScenePath}");
        }

        private static void TryAutoBuildScene()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
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
                BuildMainMenuScene();
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
                && sceneText.Contains("Floating Character")
                && sceneText.Contains("Main Menu Canvas")
                && sceneText.Contains("Main Menu Combat Card")
                && sceneText.Contains("Combat Preview")
                && sceneText.Contains("SkyCircuitMainMenuView");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Scenes");
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Art");
            CreateFolder("Assets/SkyCircuit/Art", "Animations");
            CreateFolder("Assets/SkyCircuit/Art", "Menu");
            CreateFolder("Assets/SkyCircuit/Art", "Materials");
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

        private static void ConfigureMenuTexture(string path, int maxTextureSize)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Main menu texture not found: {path}");
                return;
            }

            AssetDatabase.ImportAsset(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = maxTextureSize;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureLightingAndSky()
        {
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/SC_CloudSeaSkybox.mat");
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.9f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.76f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.34f, 0.44f);
            RenderSettings.fog = false;

            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(55f, -28f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.45f;
            sun.color = new Color(1f, 0.97f, 0.9f);
            RenderSettings.sun = sun;
        }

        private static void CreateEnvironment(Transform parent)
        {
            Mesh cloudSeaMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CloudSeaMeshPath);
            Material cloudSeaMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/SC_CloudSea.mat");
            if (cloudSeaMesh != null && cloudSeaMaterial != null)
            {
                GameObject cloudSea = new GameObject("Menu Cloud Sea Surface");
                cloudSea.transform.SetParent(parent);
                cloudSea.transform.position = new Vector3(0f, -2.2f, 0f);

                MeshFilter filter = cloudSea.AddComponent<MeshFilter>();
                filter.sharedMesh = cloudSeaMesh;

                MeshRenderer renderer = cloudSea.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = cloudSeaMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                CloudSeaMaterialScroller scroller = cloudSea.AddComponent<CloudSeaMaterialScroller>();
                scroller.Configure(renderer, new Vector2(0.0014f, 0.0005f), new Vector2(-0.0006f, 0.001f));
            }
            else
            {
                CreateFallbackCloudPlane(parent);
            }

            Mesh fogRingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FogRingMeshPath);
            Material fogMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/SC_HeightFog.mat");
            if (fogRingMesh == null || fogMaterial == null)
            {
                return;
            }

            GameObject fogRing = new GameObject("Menu Horizon Blend Curtain");
            fogRing.transform.SetParent(parent);
            fogRing.transform.position = new Vector3(0f, -2.2f, 0f);

            MeshFilter fogFilter = fogRing.AddComponent<MeshFilter>();
            fogFilter.sharedMesh = fogRingMesh;

            MeshRenderer fogRenderer = fogRing.AddComponent<MeshRenderer>();
            fogRenderer.sharedMaterial = fogMaterial;
            fogRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fogRenderer.receiveShadows = false;

            CloudSeaMaterialScroller fogScroller = fogRing.AddComponent<CloudSeaMaterialScroller>();
            fogScroller.Configure(fogRenderer, new Vector2(0.0002f, 0.00008f), new Vector2(-0.00012f, 0.00018f));
        }

        private static void CreateFallbackCloudPlane(Transform parent)
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Menu Fallback Cloud Plane";
            plane.transform.SetParent(parent);
            plane.transform.position = new Vector3(0f, -2.2f, 0f);
            plane.transform.localScale = new Vector3(120f, 1f, 120f);

            Renderer renderer = plane.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateSimpleMaterial("SC_MenuFallbackCloud.mat", new Color(0.82f, 0.91f, 0.96f, 1f));
            }
        }

        private static Material CreateSimpleMaterial(string fileName, Color color)
        {
            string path = $"{MaterialsFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = Path.GetFileNameWithoutExtension(fileName);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static LanConnectionSettings EnsureLanConnectionSettings()
        {
            LanConnectionSettings settings = AssetDatabase.LoadAssetAtPath<LanConnectionSettings>(LanConnectionSettingsPath);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<LanConnectionSettings>();
            AssetDatabase.CreateAsset(settings, LanConnectionSettingsPath);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static LanNetworkBootstrap CreateLanNetworkRig(LanConnectionSettings settings)
        {
            GameObject networkObject = new GameObject("LAN Network Rig");
            UnityTransport transport = networkObject.AddComponent<UnityTransport>();
            ushort port = settings != null ? settings.Port : (ushort)7777;
            string hostAddress = settings != null ? settings.DefaultHostAddress : "127.0.0.1";
            float timeoutSeconds = settings != null ? settings.ConnectTimeoutSeconds : 3f;
            float stateSendRate = settings != null ? settings.StateSendRate : 20f;
            transport.SetConnectionData(hostAddress, port, "0.0.0.0");
            transport.ConnectTimeoutMS = Mathf.RoundToInt(timeoutSeconds * 1000f);

            NetworkManager networkManager = networkObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                TickRate = (uint)Mathf.RoundToInt(stateSendRate),
                ClientConnectionBufferTimeout = Mathf.CeilToInt(timeoutSeconds),
                ConnectionApproval = true,
                EnableSceneManagement = true,
                ForceSamePrefabs = false,
                AutoSpawnPlayerPrefabClientSide = false,
            };

            LanNetworkBootstrap bootstrap = networkObject.AddComponent<LanNetworkBootstrap>();
            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.FindProperty("networkManager").objectReferenceValue = networkManager;
            serialized.FindProperty("transport").objectReferenceValue = transport;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(bootstrap);
            return bootstrap;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-0.56f, 1.28f, -4.89f),
                new Quaternion(0.0030340103f, -0.010620309f, 0.00003222409f, 0.999939f));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.965f, 0.98f, 1f, 1f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20000f;
            camera.fieldOfView = 27.3f;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateFloatingCharacter(
            Transform parent,
            Camera camera,
            RuntimeAnimatorController idleController)
        {
            GameObject root = new GameObject("Floating Character");
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(new Vector3(1.22f, 1.12f, 3.41f), Quaternion.identity);
            FaceCharacterTowardsCamera(root.transform, camera);

            GameObject prefab = LoadFirstCharacterPrefab();
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.name = "Floating Character Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                NormalizeCharacterModel(model, root.transform);
                ConfigureCharacterAnimator(model, camera, idleController);
            }
            else
            {
                CreateFallbackCharacter(root.transform);
            }

            SkyCircuitMenuFloat floater = root.AddComponent<SkyCircuitMenuFloat>();
            floater.Configure(new Vector3(0f, 0.065f, 0f), 0.18f, new Vector3(0f, 3.2f, 0f), 0.12f);

            GameObject rimLightObject = new GameObject("Character Rim Light");
            rimLightObject.transform.SetParent(root.transform);
            rimLightObject.transform.localPosition = new Vector3(-0.55f, 1.2f, -1.2f);
            Light rimLight = rimLightObject.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 4.6f;
            rimLight.intensity = 1.8f;
            rimLight.color = new Color(0.55f, 0.86f, 1f);
        }

        private static AnimatorController EnsureMainMenuIdleAnimatorController()
        {
            if (!File.Exists(MainMenuIdleAnimationPath))
            {
                Debug.LogWarning($"Main menu idle animation not found: {MainMenuIdleAnimationPath}");
                return null;
            }

            AssetDatabase.ImportAsset(MainMenuIdleAnimationPath);
            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MainMenuIdleAnimationPath);
            if (idleClip == null)
            {
                Debug.LogWarning($"Main menu idle animation could not be loaded: {MainMenuIdleAnimationPath}");
                return null;
            }

            AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(idleClip);
            if (!clipSettings.loopTime)
            {
                clipSettings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(idleClip, clipSettings);
                EditorUtility.SetDirty(idleClip);
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MainMenuIdleControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(MainMenuIdleControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = null;
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state != null && childState.state.name == "Idle Loop")
                {
                    idleState = childState.state;
                    break;
                }
            }

            idleState ??= stateMachine.AddState("Idle Loop");
            idleState.motion = idleClip;
            idleState.speed = 1f;
            idleState.writeDefaultValues = true;
            stateMachine.defaultState = idleState;

            AnimatorControllerLayer[] layers = controller.layers;
            if (layers.Length > 0)
            {
                layers[0].iKPass = true;
                controller.layers = layers;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject LoadFirstCharacterPrefab()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Art" });
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

        private static void FaceCharacterTowardsCamera(Transform character, Camera camera)
        {
            if (character == null || camera == null)
            {
                return;
            }

            Vector3 toCamera = camera.transform.position - character.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            character.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        private static void ConfigureCharacterAnimator(
            GameObject model,
            Camera camera,
            RuntimeAnimatorController idleController)
        {
            if (model == null)
            {
                return;
            }

            Animator animator = model.GetComponent<Animator>() ?? model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            if (idleController != null)
            {
                animator.runtimeAnimatorController = idleController;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            SkyCircuitMenuCharacterLookAtCamera lookAt = animator.GetComponent<SkyCircuitMenuCharacterLookAtCamera>();
            if (lookAt == null)
            {
                lookAt = animator.gameObject.AddComponent<SkyCircuitMenuCharacterLookAtCamera>();
            }

            lookAt.Configure(camera);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(lookAt);
        }

        private static void NormalizeCharacterModel(GameObject model, Transform root)
        {
            if (!TryCalculateRendererBounds(model, out Bounds bounds) || bounds.size.y <= 0.001f)
            {
                return;
            }

            const float targetHeight = 2.98f;
            float scale = targetHeight / bounds.size.y;
            model.transform.localScale *= scale;

            if (!TryCalculateRendererBounds(model, out bounds))
            {
                return;
            }

            Vector3 desiredCenter = root.position + new Vector3(0f, 0.12f, 0f);
            model.transform.position += desiredCenter - bounds.center;
        }

        private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static void CreateFallbackCharacter(Transform parent)
        {
            Material material = CreateSimpleMaterial("SC_MenuFallbackCharacter.mat", new Color(0.12f, 0.76f, 1f, 1f));

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Fallback Floating Character";
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(0.42f, 1.18f, 0.42f);
            Renderer renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        }

        private static void CreateMainMenuCanvas(Transform parent, Camera camera, LanNetworkBootstrap lanBootstrap)
        {
            Texture2D logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoTexturePath);
            Texture2D iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconTexturePath);
            Texture2D combatPreviewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CombatPreviewTexturePath);
            Texture2D combatTitleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(CombatTitleTexturePath);
            Texture2D trainingTitleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TrainingTitleTexturePath);
            Texture2D tutorialTitleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialTitleTexturePath);
            Texture2D settingsTitleTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SettingsTitleTexturePath);
            Font menuFont = CreateMenuFont();

            GameObject canvasObject = new GameObject("Main Menu Canvas");
            canvasObject.transform.SetParent(parent);
            canvasObject.transform.SetPositionAndRotation(
                new Vector3(-1.05f, 1.32f, 0.38f),
                Quaternion.Euler(0f, -9.5f, 0f));
            canvasObject.transform.localScale = Vector3.one * CanvasScale;

            RectTransform canvasRect = canvasObject.AddComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1280f, 720f);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 16f;

            canvasObject.AddComponent<GraphicRaycaster>();

            SkyCircuitMenuFloat menuFloat = canvasObject.AddComponent<SkyCircuitMenuFloat>();
            menuFloat.Configure(new Vector3(0f, 0.018f, 0f), 0.08f, new Vector3(0f, 0.7f, 0f), 0.07f);

            GameObject controllerObject = new GameObject("Main Menu Controller");
            controllerObject.transform.SetParent(canvasObject.transform, false);
            SkyCircuitMainMenuController controller = controllerObject.AddComponent<SkyCircuitMainMenuController>();

            RectTransform logoRect = CreateRect("Sky Circuit Logo", canvasRect, new Vector2(407.8f, 280.8f), new Vector2(-471.8f, 217.15f));
            RawImage logo = logoRect.gameObject.AddComponent<RawImage>();
            logo.texture = logoTexture;
            logo.color = new Color(1f, 1f, 1f, 0.96f);
            logo.raycastTarget = false;

            SkyCircuitMainMenuView.CardLayout combatCard = CreateMenuCard(
                canvasRect,
                "Main Menu Combat Card",
                "\u4f5c\u6218",
                new Vector2(-152f, 42f),
                new Vector2(760f, 278f),
                iconTexture,
                combatPreviewTexture,
                combatTitleTexture,
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                menuFont,
                true,
                new Color(1f, 0.31f, 0.04f, 1f));
            UnityEventTools.AddPersistentListener(combatCard.Button.onClick, controller.StartCombat);

            SkyCircuitMainMenuView.CardLayout trainingCard = CreateMenuCard(
                canvasRect,
                "Main Menu Training Card",
                "\u8bad\u7ec3\u573a",
                new Vector2(-410f, -212f),
                new Vector2(244f, 188f),
                iconTexture,
                null,
                trainingTitleTexture,
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                menuFont,
                false,
                new Color(1f, 0.31f, 0.04f, 1f));
            UnityEventTools.AddPersistentListener(trainingCard.Button.onClick, controller.OpenTraining);

            SkyCircuitMainMenuView.CardLayout tutorialCard = CreateMenuCard(
                canvasRect,
                "Main Menu Tutorial Card",
                "\u6559\u7a0b",
                new Vector2(-152f, -212f),
                new Vector2(244f, 188f),
                iconTexture,
                null,
                tutorialTitleTexture,
                new Rect(0.5f, 0f, 0.5f, 0.5f),
                menuFont,
                false,
                new Color(1f, 0.31f, 0.04f, 1f));
            UnityEventTools.AddPersistentListener(tutorialCard.Button.onClick, controller.OpenTutorial);

            SkyCircuitMainMenuView.CardLayout settingsCard = CreateMenuCard(
                canvasRect,
                "Main Menu Settings Card",
                "\u8bbe\u7f6e",
                new Vector2(106f, -212f),
                new Vector2(244f, 188f),
                iconTexture,
                null,
                settingsTitleTexture,
                new Rect(0f, 0f, 0.5f, 0.5f),
                menuFont,
                false,
                new Color(0.04f, 0.5f, 1f, 1f));
            UnityEventTools.AddPersistentListener(settingsCard.Button.onClick, controller.OpenSettings);

            logoRect.SetSiblingIndex(5);

            SettingsPanelReferences settingsPanel = CreateSettingsPanel(parent, menuFont, logoTexture, iconTexture, camera, lanBootstrap, controller);
            Text statusText = CreateText(
                "Menu Status",
                canvasRect,
                string.Empty,
                menuFont,
                28,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.05f, 0.08f, 0.12f, 0.82f),
                new Vector2(900f, 44f),
                new Vector2(0f, -444f));

            string combatScene = Path.GetFileNameWithoutExtension(OnlineCombatScenePath);
            string trainingScene = Path.GetFileNameWithoutExtension(TrainingScenePath);
            controller.Configure(combatScene, trainingScene, lanBootstrap, settingsPanel.GameObject, statusText);

            SkyCircuitMainMenuView view = canvasObject.AddComponent<SkyCircuitMainMenuView>();
            view.Configure(
                menuFont,
                logoTexture,
                iconTexture,
                combatPreviewTexture,
                combatTitleTexture,
                trainingTitleTexture,
                tutorialTitleTexture,
                settingsTitleTexture,
                canvasRect,
                logo,
                logoRect,
                combatCard,
                trainingCard,
                settingsCard,
                tutorialCard,
                settingsPanel.PanelRect,
                settingsPanel.TitleText,
                settingsPanel.RowTexts,
                settingsPanel.CloseButtonText,
                statusText);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(view);
        }

        private static SkyCircuitMainMenuView.CardLayout CreateMenuCard(
            RectTransform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Texture2D iconTexture,
            Texture2D previewTexture,
            Texture2D titleTexture,
            Rect iconUv,
            Font font,
            bool primary,
            Color accentColor)
        {
            RectTransform rect = CreateRect(name, parent, size, anchoredPosition);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.98f, 0.985f, 0.99f, 0.96f);

            Shadow shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
            shadow.effectDistance = new Vector2(5f, -5f);

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.58f, 0.62f, 0.66f, 0.28f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = BuildButtonColors();

            RawImage preview = null;
            RectTransform previewRect = null;
            if (primary && previewTexture != null)
            {
                previewRect = CreateRect("Combat Preview", rect, new Vector2(448f, 248f), new Vector2(-156f, 6f));
                preview = previewRect.gameObject.AddComponent<RawImage>();
                preview.texture = previewTexture;
                preview.uvRect = new Rect(0f, 0f, 1f, 1f);
                preview.color = new Color(1f, 1f, 1f, 0.92f);
                preview.raycastTarget = false;

                Mask previewMask = previewRect.gameObject.AddComponent<Mask>();
                previewMask.showMaskGraphic = true;
            }

            RectTransform iconRect = CreateRect(
                "Icon",
                rect,
                primary ? new Vector2(250f, 250f) : new Vector2(126f, 116f),
                primary ? new Vector2(285.8f, -39.5f) : new Vector2(58f, -16f));
            RawImage icon = iconRect.gameObject.AddComponent<RawImage>();
            icon.texture = iconTexture;
            icon.uvRect = iconUv;
            icon.color = primary
                ? new Color(0.72f, 0.75f, 0.78f, 0.23529412f)
                : new Color(0.72f, 0.75f, 0.78f, 0.23529412f);
            icon.raycastTarget = false;

            RawImage titleImage = null;
            RectTransform titleImageRect = null;
            if (titleTexture != null)
            {
                Vector2 titleImageSize = ResolveTitleImageSize(label, primary);
                Vector2 titleImagePosition = ResolveTitleImagePosition(label, primary);
                titleImageRect = CreateRect(primary ? "Combat Title Image" : "Title Image", rect, titleImageSize, titleImagePosition);
                titleImage = titleImageRect.gameObject.AddComponent<RawImage>();
                titleImage.texture = titleTexture;
                titleImage.color = Color.black;
                titleImage.raycastTarget = false;
            }

            Text title = CreateText(
                "Label",
                rect,
                label,
                font,
                primary ? 96 : 78,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                titleTexture != null ? new Color(0f, 0f, 0f, 0f) : Color.black,
                ResolveTitleImageSize(label, primary),
                ResolveTitleImagePosition(label, primary));
            title.raycastTarget = false;
            if (titleImage != null)
            {
                title.gameObject.SetActive(false);
            }

            Text detailText = null;
            RectTransform detailRect = null;
            Text modeText = null;
            RectTransform modeRect = null;
            if (primary)
            {
                detailText = CreateText(
                    "Track Detail",
                    rect,
                    "\u5f53\u524d\u8d5b\u9053\uff1a<color=#f05a18>\u5929\u7a79\u73af\u7ebf</color>",
                    font,
                    24,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Color(0.08f, 0.09f, 0.1f, 0.92f),
                    new Vector2(288f, 38f),
                    new Vector2(239f, -18f));
                detailText.supportRichText = true;
                detailText.raycastTarget = false;

                modeText = CreateText(
                    "Mode Detail",
                    rect,
                    "\u6807\u51c6\u7ade\u901f",
                    font,
                    18,
                    FontStyle.Bold,
                    TextAnchor.MiddleLeft,
                    new Color(0.16f, 0.18f, 0.2f, 0.66f),
                    new Vector2(220f, 30f),
                    new Vector2(205f, -56f));
                modeText.raycastTarget = false;

                detailRect = detailText.rectTransform;
                modeRect = modeText.rectTransform;
            }

            RectTransform cornerClip = CreateRect(
                "Corner Accent Clip",
                rect,
                new Vector2(22f, 22f),
                new Vector2(size.x * 0.5f - 11f, size.y * 0.5f - 11f));
            cornerClip.gameObject.AddComponent<RectMask2D>();

            RectTransform corner = CreateRect("Corner Accent", cornerClip, new Vector2(30f, 30f), new Vector2(8.4f, 8.4f));
            corner.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image cornerImage = corner.gameObject.AddComponent<Image>();
            cornerImage.color = accentColor;
            cornerImage.raycastTarget = false;

            float initialAccentWidth = primary ? 310f : size.x;
            float initialAccentOffsetX = primary ? 225f : 0f;
            RectTransform accent = CreateRect(
                "Orange Accent",
                rect,
                new Vector2(initialAccentWidth, primary ? 8f : 9f),
                new Vector2(initialAccentOffsetX, -size.y * 0.5f + (primary ? 4f : 4.5f)));
            Image accentImage = accent.gameObject.AddComponent<Image>();
            accentImage.color = accentColor;
            accentImage.raycastTarget = false;

            SkyCircuitMainMenuView.CardLayout card = primary
                ? SkyCircuitMainMenuView.CardLayout.CreatePrimary()
                : SkyCircuitMainMenuView.CardLayout.CreateSecondary(label, anchoredPosition, size, iconUv, accentColor);
            card.Bind(rect, background, button, preview, previewRect, title, title.rectTransform, titleImage, titleImageRect, detailText, detailRect, modeText, modeRect, icon, iconRect, cornerClip, corner, cornerImage, accent, accentImage);
            card.Apply(font, iconTexture, previewTexture, titleTexture);
            return card;
        }

        private static Vector2 ResolveTitleImageSize(string label, bool primary)
        {
            if (primary)
            {
                return new Vector2(235f, 116f);
            }

            return !string.IsNullOrEmpty(label) && label.Length > 2
                ? new Vector2(132f, 62f)
                : new Vector2(96f, 62f);
        }

        private static Vector2 ResolveTitleImagePosition(string label, bool primary)
        {
            if (primary)
            {
                return new Vector2(189.5f, 70f);
            }

            return !string.IsNullOrEmpty(label) && label.Length > 2
                ? new Vector2(-45f, 44f)
                : new Vector2(-63f, 44f);
        }

        private static SettingsPanelReferences CreateSettingsPanel(
            Transform parent,
            Font font,
            Texture2D logoTexture,
            Texture2D iconTexture,
            Camera camera,
            LanNetworkBootstrap lanBootstrap,
            SkyCircuitMainMenuController controller)
        {
            Color accent = new Color(1f, 0.31f, 0.04f, 1f);
            Color ink = new Color(0.02f, 0.025f, 0.03f, 1f);
            Color mutedInk = new Color(0.15f, 0.16f, 0.18f, 0.9f);
            Color border = new Color(0.72f, 0.76f, 0.8f, 0.38f);

            GameObject canvasObject = new GameObject("LAN Settings Canvas");
            canvasObject.transform.SetParent(parent, false);

            RectTransform overlayRect = canvasObject.AddComponent<RectTransform>();
            overlayRect.sizeDelta = new Vector2(1280f, 720f);

            Canvas overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            overlayCanvas.worldCamera = camera;
            overlayCanvas.planeDistance = 0.4f;
            overlayCanvas.sortingOrder = 80;

            CanvasScaler overlayScaler = canvasObject.AddComponent<CanvasScaler>();
            overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayScaler.referenceResolution = new Vector2(1280f, 720f);
            overlayScaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform panel = CreateStretchRect("Settings Panel", overlayRect, Vector2.zero, Vector2.zero);
            Image panelBackground = panel.gameObject.AddComponent<Image>();
            panelBackground.color = new Color(0.965f, 0.975f, 0.985f, 0.98f);
            SkyCircuitLanSettingsPanelController lanPanelController = panel.gameObject.AddComponent<SkyCircuitLanSettingsPanelController>();

            RectTransform frame = CreateRect("LAN Frame", panel, new Vector2(1230f, 672f), Vector2.zero);
            Image frameBackground = frame.gameObject.AddComponent<Image>();
            frameBackground.color = new Color(0.995f, 0.997f, 1f, 0.96f);

            Outline frameOutline = frame.gameObject.AddComponent<Outline>();
            frameOutline.effectColor = border;
            frameOutline.effectDistance = new Vector2(1.4f, -1.4f);

            Shadow frameShadow = frame.gameObject.AddComponent<Shadow>();
            frameShadow.effectColor = new Color(0f, 0f, 0f, 0.08f);
            frameShadow.effectDistance = new Vector2(5f, -5f);

            CreateLanWatermark(frame, "Top Right Logo Watermark", logoTexture, new Vector2(190f, 124f), new Vector2(520f, 275f), 0.055f);
            CreateLanLine(frame, "Title Separator", new Vector2(1230f, 1.5f), new Vector2(0f, 230f), new Color(0.76f, 0.79f, 0.82f, 0.28f));
            CreateLanAccentBar(frame, new Vector2(-585f, 287f), new Vector2(6f, 64f), accent);

            Text titleText = CreateText(
                "Settings Title",
                frame,
                "\u5c40\u57df\u7f51\u8054\u673a",
                font,
                58,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                ink,
                new Vector2(520f, 90f),
                new Vector2(-300f, 296f));
            titleText.raycastTarget = false;

            RectTransform clientSection = CreateLanSection(frame, font, "Client Panel", "\u4f5c\u4e3a\u5ba2\u6237\u7aef", new Vector2(-302f, -20f), accent, border);
            RectTransform serverSection = CreateLanSection(frame, font, "Server Panel", "\u4f5c\u4e3a\u670d\u52a1\u7aef", new Vector2(302f, -20f), accent, border);
            Text clientStatusText = CreateText("Client Status Text", clientSection, "\u672a\u8fde\u63a5", font, 17, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.18f, 0.2f, 0.23f, 0.78f), new Vector2(170f, 34f), new Vector2(180f, 196f));
            Text serverStatusText = CreateText("Server Status Text", serverSection, "\u672a\u5f00\u542f", font, 17, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.18f, 0.2f, 0.23f, 0.78f), new Vector2(170f, 34f), new Vector2(180f, 196f));
            clientStatusText.raycastTarget = false;
            serverStatusText.raycastTarget = false;

            CreateLanWatermark(clientSection, "Client Watermark", logoTexture, new Vector2(180f, 118f), new Vector2(-176f, -145f), 0.045f);
            CreateLanIconWatermark(clientSection, "Client Icon Watermark", iconTexture, new Rect(0.5f, 0f, 0.5f, 0.5f), new Vector2(156f, 138f), new Vector2(122f, -120f), 0.06f);
            CreateLanIconWatermark(serverSection, "Server Icon Watermark", iconTexture, new Rect(0f, 0f, 0.5f, 0.5f), new Vector2(156f, 138f), new Vector2(136f, -120f), 0.055f);

            CreateText("Target IP Label", clientSection, "\u76ee\u6807 IP", font, 22, FontStyle.Bold, TextAnchor.MiddleLeft, mutedInk, new Vector2(130f, 42f), new Vector2(-185f, 120.3f)).raycastTarget = false;
            InputField targetIpInput = CreateLanInputField(
                clientSection,
                "Target IP Field",
                font,
                "127.0.0.1",
                lanBootstrap != null ? lanBootstrap.ClientAddress : "127.0.0.1",
                new Vector2(362f, 48f),
                new Vector2(75f, 118f),
                border,
                64,
                InputField.ContentType.Standard);
            CreateLanLine(clientSection, "Client Divider", new Vector2(520f, 1f), new Vector2(0f, 72f), new Color(0.82f, 0.85f, 0.88f, 0.22f));

            CreateText("Port Label", clientSection, "\u7aef\u53e3", font, 22, FontStyle.Bold, TextAnchor.MiddleLeft, mutedInk, new Vector2(130f, 42f), new Vector2(-185f, 33.6f)).raycastTarget = false;
            InputField clientPortInput = CreateLanInputField(
                clientSection,
                "Port Field",
                font,
                "7777",
                (lanBootstrap != null ? lanBootstrap.Port : (ushort)7777).ToString(),
                new Vector2(362f, 48f),
                new Vector2(75f, 28f),
                border,
                5,
                InputField.ContentType.IntegerNumber);

            RectTransform hint = CreateRect("Client Hint", clientSection, new Vector2(518f, 44f), new Vector2(0f, -60f));
            Image hintImage = hint.gameObject.AddComponent<Image>();
            hintImage.color = new Color(0.86f, 0.88f, 0.9f, 0.22f);
            Text hintIcon = CreateText("Hint Icon", hint, "i", font, 22, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.4f, 0.43f, 0.46f, 1f), new Vector2(28f, 32f), new Vector2(-236f, 0f));
            hintIcon.raycastTarget = false;
            Text hintText = CreateText("Hint Text", hint, "\u8f93\u5165\u670d\u52a1\u7aef\u5730\u5740\u540e\u52a0\u5165\u623f\u95f4", font, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.38f, 0.4f, 0.43f, 0.78f), new Vector2(410f, 32f), new Vector2(18f, 0f));
            hintText.raycastTarget = false;

            Button clearButton = CreateLanButton(clientSection, font, "\u6e05\u7a7a", new Vector2(-154f, -185f), new Vector2(205f, 56f), Color.white, ink, border, 24);
            Button connectButton = CreateLanButton(clientSection, font, "\u8fde\u63a5", new Vector2(111f, -185f), new Vector2(294f, 56f), accent, Color.white, new Color(1f, 0.31f, 0.04f, 0.85f), 24);

            CreateText("Listen Port Label", serverSection, "\u76d1\u542c\u7aef\u53e3", font, 22, FontStyle.Bold, TextAnchor.MiddleLeft, mutedInk, new Vector2(130f, 42f), new Vector2(-192.1f, 141.3f)).raycastTarget = false;
            RectTransform listenField = CreateLanField(serverSection, "Listen Port Field", new Vector2(388f, 40f), new Vector2(55f, 138f), border);
            InputField listenPortInput = listenField.gameObject.AddComponent<InputField>();
            listenField.GetComponent<Image>().raycastTarget = true;
            Text listenPortText = CreateText("Text", listenField, (lanBootstrap != null ? lanBootstrap.Port : (ushort)7777).ToString(), font, 18, FontStyle.Bold, TextAnchor.MiddleLeft, ink, new Vector2(320f, 34f), new Vector2(-16f, 0f));
            Text listenPortPlaceholder = CreateText("Placeholder", listenField, "7777", font, 18, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.42f, 0.44f, 0.47f, 0.46f), new Vector2(320f, 34f), new Vector2(-16f, 0f));
            listenPortText.raycastTarget = false;
            listenPortPlaceholder.raycastTarget = false;
            listenPortInput.textComponent = listenPortText;
            listenPortInput.placeholder = listenPortPlaceholder;
            listenPortInput.targetGraphic = listenField.GetComponent<Image>();
            listenPortInput.contentType = InputField.ContentType.IntegerNumber;
            listenPortInput.characterLimit = 5;
            listenPortInput.lineType = InputField.LineType.SingleLine;
            listenPortInput.text = (lanBootstrap != null ? lanBootstrap.Port : (ushort)7777).ToString();
            CreateText("Listen Arrows", listenField, "\u25b2\n\u25bc", font, 15, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.42f, 0.44f, 0.47f, 0.8f), new Vector2(26f, 34f), new Vector2(174f, 0f)).raycastTarget = false;

            CreateText("Local IP Label", serverSection, "\u672c\u5730 IP", font, 22, FontStyle.Bold, TextAnchor.MiddleLeft, mutedInk, new Vector2(130f, 42f), new Vector2(-194.6f, 74.9f)).raycastTarget = false;
            RectTransform ipList = CreateLanField(serverSection, "Local IP List", new Vector2(388f, 242f), new Vector2(55f, -28f), border);
            Text localIpListText = CreateText("Local IP List Text", ipList, string.Empty, font, 18, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.12f, 0.14f, 0.16f, 0.82f), new Vector2(310f, 210f), new Vector2(-14f, -10f));
            localIpListText.raycastTarget = false;
            for (int i = 0; i < 5; i++)
            {
                CreateLanLine(ipList, "IP Row Line " + i, new Vector2(336f, 1f), new Vector2(-12f, 80f - i * 42f), new Color(0.76f, 0.79f, 0.82f, 0.2f));
            }

            CreateLanLine(ipList, "IP Scroll Track", new Vector2(6f, 206f), new Vector2(178f, 0f), new Color(0.72f, 0.74f, 0.77f, 0.28f));
            CreateLanLine(ipList, "IP Scroll Thumb", new Vector2(8f, 108f), new Vector2(178f, 34f), new Color(0.55f, 0.57f, 0.6f, 0.48f));
            CreateText("IP Scroll Arrows", ipList, "\u25b2\n\n\n\n\u25bc", font, 13, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.42f, 0.44f, 0.47f, 0.8f), new Vector2(26f, 226f), new Vector2(178f, 0f)).raycastTarget = false;
            localIpListText.rectTransform.SetAsLastSibling();

            Button startServerButton = CreateLanButton(serverSection, font, "\u5f00\u542f\u4e3b\u673a", new Vector2(-115f, -185f), new Vector2(310f, 56f), accent, Color.white, new Color(1f, 0.31f, 0.04f, 0.85f), 23);
            Button copyIpButton = CreateLanButton(serverSection, font, "\u590d\u5236 IP", new Vector2(158f, -185f), new Vector2(188f, 56f), Color.white, ink, border, 23);

            CreateLanLine(frame, "Bottom Separator", new Vector2(1230f, 1.5f), new Vector2(0f, -262f), new Color(0.76f, 0.79f, 0.82f, 0.28f));
            Button refreshButton = CreateLanButton(frame, font, "\u5237\u65b0\u7f51\u7edc", new Vector2(-500f, -304f), new Vector2(182f, 48f), Color.white, ink, border, 22);
            Button back = CreateLanButton(frame, font, "\u8fd4\u56de", new Vector2(280f, -304f), new Vector2(158f, 48f), Color.white, ink, border, 22);
            Button confirm = CreateLanButton(frame, font, "\u786e\u5b9a", new Vector2(485f, -304f), new Vector2(200f, 48f), accent, Color.white, new Color(1f, 0.31f, 0.04f, 0.85f), 22, 4f);
            lanPanelController.Configure(lanBootstrap, targetIpInput, clientPortInput, listenPortInput, localIpListText, clientStatusText, serverStatusText, connectButton.GetComponentInChildren<Text>(), startServerButton.GetComponentInChildren<Text>(), hintText);
            EditorUtility.SetDirty(lanPanelController);
            UnityEventTools.AddPersistentListener(clearButton.onClick, lanPanelController.ClearClientFields);
            UnityEventTools.AddPersistentListener(connectButton.onClick, lanPanelController.StartClient);
            UnityEventTools.AddPersistentListener(startServerButton.onClick, lanPanelController.StartServer);
            UnityEventTools.AddPersistentListener(copyIpButton.onClick, lanPanelController.CopyPrimaryLocalIp);
            UnityEventTools.AddPersistentListener(refreshButton.onClick, lanPanelController.RefreshNetworkInfo);
            UnityEventTools.AddPersistentListener(back.onClick, controller.CloseSettings);
            UnityEventTools.AddPersistentListener(confirm.onClick, controller.CloseSettings);

            panel.gameObject.SetActive(false);
            return new SettingsPanelReferences
            {
                GameObject = panel.gameObject,
                PanelRect = panel,
                TitleText = titleText,
                RowTexts = Array.Empty<Text>(),
                CloseButtonText = back.GetComponentInChildren<Text>(),
            };
        }

        private static RectTransform CreateLanSection(
            RectTransform parent,
            Font font,
            string name,
            string title,
            Vector2 position,
            Color accent,
            Color border)
        {
            RectTransform section = CreateRect(name, parent, new Vector2(570f, 460f), position);
            Image background = section.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.58f);

            Outline outline = section.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            CreateLanAccentBar(section, new Vector2(-260f, 184f), new Vector2(5f, 32f), accent);
            CreateText("Section Title", section, title, font, 27, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.04f, 0.045f, 0.05f, 1f), new Vector2(245f, 42f), new Vector2(-112f, 190f)).raycastTarget = false;
            CreateLanLine(section, "Section Title Rule", new Vector2(356f, 1f), new Vector2(126f, 184f), new Color(0.76f, 0.79f, 0.82f, 0.25f));
            return section;
        }

        private static RectTransform CreateLanField(RectTransform parent, string name, Vector2 size, Vector2 position, Color border)
        {
            RectTransform field = CreateRect(name, parent, size, position);
            Image image = field.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.72f);
            image.raycastTarget = false;

            Outline outline = field.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1f, -1f);
            return field;
        }

        private static InputField CreateLanInputField(
            RectTransform parent,
            string name,
            Font font,
            string placeholder,
            string initialText,
            Vector2 size,
            Vector2 position,
            Color border,
            int characterLimit,
            InputField.ContentType contentType)
        {
            RectTransform field = CreateLanField(parent, name, size, position, border);
            Image background = field.GetComponent<Image>();
            background.raycastTarget = true;

            InputField input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.contentType = contentType;
            input.characterLimit = characterLimit;
            input.lineType = InputField.LineType.SingleLine;
            input.selectionColor = new Color(1f, 0.31f, 0.04f, 0.28f);

            Text text = CreateText(
                "Text",
                field,
                initialText,
                font,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.08f, 0.09f, 0.1f, 0.92f),
                size - new Vector2(30f, 0f),
                new Vector2(10f, 0f));
            text.raycastTarget = false;
            text.supportRichText = false;

            Text placeholderText = CreateText(
                "Placeholder",
                field,
                placeholder,
                font,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.42f, 0.44f, 0.47f, 0.46f),
                size - new Vector2(30f, 0f),
                new Vector2(10f, 0f));
            placeholderText.raycastTarget = false;
            placeholderText.supportRichText = false;

            input.textComponent = text;
            input.placeholder = placeholderText;
            input.text = initialText ?? string.Empty;
            return input;
        }

        private static Button CreateLanButton(
            RectTransform parent,
            Font font,
            string label,
            Vector2 position,
            Vector2 size,
            Color backgroundColor,
            Color textColor,
            Color border,
            int fontSize,
            float labelOffsetY = 3f)
        {
            RectTransform rect = CreateRect(label + " Button", parent, size, position);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = backgroundColor;

            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1f, -1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = BuildButtonColors();

            Text text = CreateText("Label", rect, label, font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor, size, new Vector2(0f, labelOffsetY));
            text.raycastTarget = false;
            return button;
        }

        private static void CreateLanAccentBar(RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect("Orange Accent", parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateLanLine(RectTransform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateLanWatermark(
            RectTransform parent,
            string name,
            Texture2D texture,
            Vector2 size,
            Vector2 position,
            float alpha)
        {
            if (texture == null)
            {
                return;
            }

            RectTransform rect = CreateRect(name, parent, size, position);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(0.7f, 0.74f, 0.78f, alpha);
            image.raycastTarget = false;
        }

        private static void CreateLanIconWatermark(
            RectTransform parent,
            string name,
            Texture2D texture,
            Rect uv,
            Vector2 size,
            Vector2 position,
            float alpha)
        {
            if (texture == null)
            {
                return;
            }

            RectTransform rect = CreateRect(name, parent, size, position);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.color = new Color(0.7f, 0.74f, 0.78f, alpha);
            image.raycastTarget = false;
        }

        private static Text CreateSliderRow(RectTransform parent, Font font, string label, Vector2 position, float value)
        {
            Text labelText = CreateText(
                label + " Label",
                parent,
                label,
                font,
                34,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.06f, 0.07f, 0.08f, 0.92f),
                new Vector2(230f, 52f),
                position + new Vector2(-192f, 0f));

            RectTransform sliderRect = CreateRect(label + " Slider", parent, new Vector2(330f, 36f), position + new Vector2(122f, 0f));
            Slider slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;

            RectTransform backgroundRect = CreateStretchRect("Background", sliderRect, Vector2.zero, Vector2.zero);
            Image background = backgroundRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.83f, 0.86f, 0.89f, 0.78f);
            slider.targetGraphic = background;

            RectTransform fillArea = CreateStretchRect("Fill Area", sliderRect, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            RectTransform fill = CreateStretchRect("Fill", fillArea, Vector2.zero, Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.31f, 0.04f, 1f);
            slider.fillRect = fill;

            RectTransform handleArea = CreateStretchRect("Handle Slide Area", sliderRect, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            RectTransform handle = CreateRect("Handle", handleArea, new Vector2(20f, 42f), Vector2.zero);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.08f, 0.1f, 0.12f, 1f);
            slider.handleRect = handle;
            return labelText;
        }

        private static Button CreateSmallButton(RectTransform parent, Font font, string label, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(label + " Button", parent, size, position);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.12f, 0.94f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.colors = BuildDarkButtonColors();

            Text text = CreateText(
                "Label",
                rect,
                label,
                font,
                30,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                Color.white,
                size,
                Vector2.zero);
            text.raycastTarget = false;
            return button;
        }

        private static ColorBlock BuildButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.965f, 0.99f, 1f);
            colors.pressedColor = new Color(0.88f, 0.91f, 0.94f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.72f, 0.74f, 0.76f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static ColorBlock BuildDarkButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.86f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.6f, 0.62f, 0.64f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            string content,
            Font font,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            Color color,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            RectTransform rect = CreateRect(name, parent, size, anchoredPosition);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            text.resizeTextMinSize = Mathf.Max(18, Mathf.RoundToInt(fontSize * 0.55f));
            text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static RectTransform CreateStretchRect(string name, RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);

            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private sealed class SettingsPanelReferences
        {
            public GameObject GameObject;
            public RectTransform PanelRect;
            public Text TitleText;
            public Text[] RowTexts;
            public Text CloseButtonText;
        }

        private static Font CreateMenuFont()
        {
            string[] preferredFonts =
            {
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "SimHei",
                "Arial",
            };

            Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, 96);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();

            Type inputSystemModuleType = FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (inputSystemModuleType != null)
            {
                eventSystemObject.AddComponent(inputSystemModuleType);
                return;
            }

            eventSystemObject.AddComponent<StandaloneInputModule>();
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

        private static void CreateRevisionMarker(Transform parent)
        {
            GameObject marker = new GameObject(SceneRevisionMarker);
            marker.transform.SetParent(parent);
        }

        private static void SetBuildScenes()
        {
            List<string> scenePaths = new List<string>();
            AddBuildScene(scenePaths, ScenePath);
            AddBuildScene(scenePaths, OnlineCombatScenePath);
            AddBuildScene(scenePaths, TrainingScenePath);

            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[scenePaths.Count];
            for (int i = 0; i < scenePaths.Count; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static void AddBuildScene(List<string> scenePaths, string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath) || scenePaths.Contains(scenePath) || !File.Exists(scenePath))
            {
                return;
            }

            scenePaths.Add(scenePath);
        }

    }
}
