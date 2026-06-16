using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    [InitializeOnLoad]
    public static class SkyCircuitVolumetricCloudsURPSetup
    {
        private const string ScenePath = "Assets/Scenes/V0_7_CloudSea.unity";
        private const string SceneRevisionMarker = "Cloud Sea Scene Revision 16";
        private const string VolumeProfilePath = "Assets/SkyCircuit/Art/Volumes/SC_URPVolumetricCloudsProfile.asset";
        private const string CloudsMaterialPath = "Assets/VolumetricClouds/VolumetricClouds.mat";
        private const string VolumeObjectName = "URP Volumetric Clouds Volume";
        private const string AutoSetupSessionKey = "SkyCircuit.V07.URPVolumetricClouds.AutoSetupQueued.v9";

        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        static SkyCircuitVolumetricCloudsURPSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(AutoSetupSessionKey, false))
            {
                return;
            }

            EditorApplication.delayCall += TryAutoSetup;
        }

        [MenuItem("Sky Circuit/Setup URP Volumetric Clouds")]
        public static void SetupSceneAndRenderer()
        {
            EnsureCloudSeaScene();

            foreach (string rendererPath in RendererPaths)
            {
                EnsureRendererFeature(rendererPath);
            }

            VolumeProfile profile = EnsureVolumeProfile();
            AssetDatabase.SaveAssets();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                Debug.Log("Sky Circuit URP volumetric cloud assets configured. Scene update deferred until edit mode.");
                return;
            }

            EnsureSceneVolume(profile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Sky Circuit URP volumetric clouds configured.");
        }

        private static void TryAutoSetup()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            if (SessionState.GetBool(AutoSetupSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoSetupSessionKey, true);
            try
            {
                SetupSceneAndRenderer();
            }
            catch
            {
                SessionState.SetBool(AutoSetupSessionKey, false);
                throw;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SessionState.SetBool(AutoSetupSessionKey, false);
            EditorApplication.delayCall += TryAutoSetup;
        }

        private static void EnsureCloudSeaScene()
        {
            if (File.Exists(ScenePath) && File.ReadAllText(ScenePath).Contains(SceneRevisionMarker))
            {
                return;
            }

            SkyCircuitCloudSeaSceneBuilder.BuildCloudSeaScene();
        }

        private static void EnsureRendererFeature(string rendererPath)
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"Could not find URP renderer data at {rendererPath}.");
                return;
            }

            VolumetricCloudsURP feature = FindRendererFeature(rendererData);
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<VolumetricCloudsURP>();
                feature.name = "Volumetric Clouds URP";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AppendRendererFeature(rendererData, feature);
            }

            ConfigureRendererFeature(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static VolumetricCloudsURP FindRendererFeature(ScriptableRendererData rendererData)
        {
            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            if (features == null)
            {
                return null;
            }

            for (int i = 0; i < features.arraySize; i++)
            {
                if (features.GetArrayElementAtIndex(i).objectReferenceValue is VolumetricCloudsURP feature)
                {
                    return feature;
                }
            }

            return null;
        }

        private static void AppendRendererFeature(ScriptableRendererData rendererData, VolumetricCloudsURP feature)
        {
            SerializedObject serializedRenderer = new SerializedObject(rendererData);
            SerializedProperty features = serializedRenderer.FindProperty("m_RendererFeatures");
            SerializedProperty featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");

            if (features == null || featureMap == null)
            {
                Debug.LogWarning($"Could not append Volumetric Clouds URP to {rendererData.name}; renderer serialization changed.");
                return;
            }

            int index = features.arraySize;
            features.InsertArrayElementAtIndex(index);
            features.GetArrayElementAtIndex(index).objectReferenceValue = feature;

            featureMap.InsertArrayElementAtIndex(featureMap.arraySize);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
            {
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            }

            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRendererFeature(VolumetricCloudsURP feature)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CloudsMaterialPath);
            if (material != null)
            {
                material.SetFloat("_CloudMinimumLight", 0.72f);
                material.SetColor("_CloudMinimumLightColor", new Color(0.86f, 0.9f, 0.94f, 1f));
                EditorUtility.SetDirty(material);
                feature.CloudsMaterial = material;
            }

            feature.ResolutionScale = 0.5f;
            feature.UpscaleMode = VolumetricCloudsURP.CloudsUpscaleMode.Bilateral;
            feature.PreferredRenderMode = VolumetricCloudsURP.CloudsRenderMode.CopyTexture;
            feature.AmbientUpdateMode = VolumetricCloudsURP.CloudsAmbientMode.Dynamic;
            feature.RenderingDebugger = false;
            feature.ResetWindOnStart = true;
            feature.SunAttenuation = false;
            feature.OutputCloudsDepth = true;
            feature.OutputToSceneDepth = false;
        }

        private static VolumeProfile EnsureVolumeProfile()
        {
            CreateFolder("Assets/SkyCircuit/Art", "Volumes");

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "SC_URPVolumetricCloudsProfile";
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            profile.components.RemoveAll(component => component == null);

            if (!profile.TryGet(out VolumetricClouds clouds))
            {
                clouds = ScriptableObject.CreateInstance<VolumetricClouds>();
                clouds.name = "VolumetricClouds";
                profile.components.Add(clouds);
                AssetDatabase.AddObjectToAsset(clouds, profile);
            }
            else if (!AssetDatabase.Contains(clouds))
            {
                AssetDatabase.AddObjectToAsset(clouds, profile);
            }

            ConfigureClouds(clouds);
            EditorUtility.SetDirty(clouds);
            EditorUtility.SetDirty(profile);

            return profile;
        }

        private static void ConfigureClouds(VolumetricClouds clouds)
        {
            clouds.active = true;
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Cloudy;
            SetCloudPresetOverride(clouds, VolumetricClouds.CloudPresets.Cloudy);

            SetOverride(clouds.state, true);
            SetOverride(clouds.localClouds, false);

            SetOverride(clouds.densityMultiplier, 0.14f);
            SetOverride(clouds.densityCurve, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.1f, 0.68f),
                new Keyframe(0.62f, 0.42f),
                new Keyframe(1f, 0f)));

            SetOverride(clouds.shapeFactor, 0.94f);
            SetOverride(clouds.shapeScale, 6.8f);
            SetOverride(clouds.erosionFactor, 0.88f);
            SetOverride(clouds.erosionScale, 132f);
            SetOverride(clouds.erosionCurve, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.18f, 0.98f),
                new Keyframe(0.72f, 1f),
                new Keyframe(1f, 1f)));
            SetOverride(clouds.ambientOcclusionCurve, new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.35f, 0f),
                new Keyframe(1f, 0f)));

            SetOverride(clouds.microErosion, false);
            SetOverride(clouds.bottomAltitude, 5200f);
            SetOverride(clouds.altitudeRange, 1800f);
            SetOverride(clouds.earthCurvature, 0.04f);

            SetOverride(clouds.globalSpeed, 6f);
            SetOverride(clouds.globalOrientation, 230f);
            SetOverride(clouds.shapeSpeedMultiplier, 0.22f);
            SetOverride(clouds.erosionSpeedMultiplier, 0.1f);
            SetOverride(clouds.altitudeDistortion, 0.05f);

            SetOverride(clouds.ambientLightProbeDimmer, 2f);
            SetOverride(clouds.sunLightDimmer, 2f);
            SetOverride(clouds.scatteringTint, new Color(0.9f, 0.9f, 0.88f, 1f));
            SetOverride(clouds.powderEffectIntensity, 0f);
            SetOverride(clouds.multiScattering, 1f);

            SetOverride(clouds.shadows, false);
            SetOverride(clouds.temporalAccumulationFactor, 0.93f);
            SetOverride(clouds.perceptualBlending, 0.82f);
            SetOverride(clouds.numPrimarySteps, 56);
            SetOverride(clouds.numLightSteps, 4);
            SetOverride(clouds.fadeInMode, VolumetricClouds.CloudFadeInMode.Automatic);
            SetOverride(clouds.fadeInStart, 0f);
            SetOverride(clouds.fadeInDistance, 3200f);
        }

        private static void SetCloudPresetOverride(VolumetricClouds clouds, VolumetricClouds.CloudPresets preset)
        {
            SerializedObject serializedClouds = new SerializedObject(clouds);
            SerializedProperty presetProperty = serializedClouds.FindProperty("m_CloudPreset");
            if (presetProperty == null)
            {
                return;
            }

            SerializedProperty overrideState = presetProperty.FindPropertyRelative("m_OverrideState");
            SerializedProperty value = presetProperty.FindPropertyRelative("m_Value");
            if (overrideState != null)
            {
                overrideState.boolValue = true;
            }

            if (value != null)
            {
                value.enumValueIndex = (int)preset;
            }

            serializedClouds.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetOverride<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.overrideState = true;
            parameter.value = value;
        }

        private static void EnsureSceneVolume(VolumeProfile profile)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            GameObject environmentRoot = GameObject.Find("Environment") ?? new GameObject("Environment");
            GameObject volumeObject = GameObject.Find(VolumeObjectName) ?? new GameObject(VolumeObjectName);
            volumeObject.transform.SetParent(environmentRoot.transform, false);

            Volume volume = volumeObject.GetComponent<Volume>() ?? volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            volume.sharedProfile = profile;

            GameObject sunObject = GameObject.Find("Sun");
            if (sunObject != null && sunObject.TryGetComponent(out Light sunLight))
            {
                sunObject.transform.rotation = Quaternion.Euler(72f, -28f, 0f);
                sunLight.intensity = 2f;
                sunLight.color = new Color(1f, 0.98f, 0.93f);
                RenderSettings.sun = sunLight;
            }

            GameObject cameraObject = GameObject.FindWithTag("MainCamera") ?? GameObject.Find("Main Camera");
            if (cameraObject != null && cameraObject.TryGetComponent(out Camera previewCamera))
            {
                previewCamera.farClipPlane = 20000f;
            }

            GameObject skyCloudDome = GameObject.Find("Sky Cloud Dome");
            if (skyCloudDome != null)
            {
                skyCloudDome.SetActive(false);
            }

            EditorUtility.SetDirty(volumeObject);
            EditorUtility.SetDirty(environmentRoot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
