using System.IO;
using SkyCircuit.AI;
using SkyCircuit.Combat;
using SkyCircuit.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitV03SceneBuilder
    {
        private const string SourceScenePath = "Assets/Scenes/V0_2_MatchPrototype.unity";
        private const string ScenePath = "Assets/Scenes/V0_3_DogfightPrototype.unity";
        private const string MaterialsFolder = "Assets/SkyCircuit/Art/Materials";
        private const string BackHitTexturePath = "Assets/SkyCircuit/Art/Textures/SC_BackHitTriangle.png";

        [MenuItem("Sky Circuit/Legacy Scene Builders/Build V0.3 Dogfight Prototype Scene")]
        public static void BuildDogfightPrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot build the V0.3 dogfight prototype scene while Unity is in Play Mode.");
                return;
            }

            EnsureFolders();
            if (!File.Exists(SourceScenePath))
            {
                Debug.LogError($"Sky Circuit V0.3 build needs {SourceScenePath}. Build V0.2 first.");
                return;
            }

            Material pointFieldMaterial = CreateMaterial(
                "SC_PointField.mat",
                new Color(1f, 0.82f, 0.16f, 0.32f),
                new Color(1f, 0.45f, 0.08f),
                true);
            Texture2D backHitTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BackHitTexturePath);

            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

            Competitor player = FindComponentOnNamedObject<Competitor>("Player Competitor");
            Competitor opponent = FindComponentOnNamedObject<Competitor>("AI Competitor");
            MatchController match = FindComponentOnNamedObject<MatchController>("Match Controller");
            MatchDebugHud hud = FindComponentOnNamedObject<MatchDebugHud>("Match HUD");
            RouteAIPilotController aiPilot = FindComponentOnNamedObject<RouteAIPilotController>("AI Competitor");
            Transform matchRoot = FindNamedObject("Match")?.transform;

            if (player == null || opponent == null || match == null || matchRoot == null)
            {
                Debug.LogError("Sky Circuit V0.3 build could not find the V0.2 match objects.");
                return;
            }

            SkyCircuitCharacterVisualSceneUtility.EnsureCharacterVisual(player.gameObject, "Player Character Visual");
            SkyCircuitCharacterVisualSceneUtility.EnsureCharacterVisual(opponent.gameObject, "AI Character Visual");

            BackHitFeedback playerFeedback = CreateBackHitFeedback(player.Body, pointFieldMaterial, backHitTexture);
            BackHitFeedback opponentFeedback = CreateBackHitFeedback(opponent.Body, pointFieldMaterial, backHitTexture);

            GameObject dogfightObject = new GameObject("Dogfight Controller");
            dogfightObject.transform.SetParent(matchRoot);
            DogfightController dogfight = dogfightObject.AddComponent<DogfightController>();
            dogfight.Configure(match, player, opponent);
            dogfight.ConfigureFeedback(playerFeedback, opponentFeedback);

            if (hud != null)
            {
                hud.ConfigureDogfight(dogfight);
            }

            if (aiPilot != null)
            {
                aiPilot.ConfigureDogfight(player, dogfight);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.3 dogfight prototype scene built at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "Scenes");
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Art");
            CreateFolder("Assets/SkyCircuit/Art", "Materials");
            CreateFolder("Assets/SkyCircuit/Art", "Textures");
            CreateFolder("Assets/SkyCircuit/Runtime", "Combat");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Material CreateMaterial(string fileName, Color baseColor, Color emissionColor, bool transparent)
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
            material.EnableKeyword("_EMISSION");

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

        private static BackHitFeedback CreateBackHitFeedback(Transform competitor, Material material, Texture2D backHitTexture)
        {
            GameObject feedbackObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            feedbackObject.name = "Back Hit Feedback";
            feedbackObject.transform.SetParent(competitor);
            feedbackObject.transform.localPosition = new Vector3(0f, 0.35f, -0.95f);
            feedbackObject.transform.localRotation = Quaternion.identity;
            feedbackObject.transform.localScale = new Vector3(1.55f, 1.05f, 0.32f);

            var collider = feedbackObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = feedbackObject.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            Light light = feedbackObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 0.7f;
            light.color = new Color(1f, 0.82f, 0.16f);

            BackHitFeedback feedback = feedbackObject.AddComponent<BackHitFeedback>();
            feedback.ResetToDefaultBackAnchor();
            feedback.Configure(renderer, light, backHitTexture);
            return feedback;
        }

        private static GameObject FindNamedObject(string objectName)
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
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

        private static void SetBuildScene(string scenePath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true),
            };
        }
    }
}
