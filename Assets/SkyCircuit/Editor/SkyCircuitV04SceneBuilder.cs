using System.IO;
using SkyCircuit.AI;
using SkyCircuit.Match;
using SkyCircuit.Profiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitV04SceneBuilder
    {
        private const string SourceScenePath = "Assets/Scenes/V0_3_DogfightPrototype.unity";
        private const string ScenePath = "Assets/Scenes/V0_4_ProfilePrototype.unity";

        [MenuItem("Sky Circuit/Build V0.4 Profile Prototype Scene")]
        public static void BuildProfilePrototypeScene()
        {
            CreateFolder("Assets", "Scenes");
            SkyCircuitDefaultProfiles.ProfileSet profiles = SkyCircuitDefaultProfiles.EnsureDefaultProfiles(false);

            if (!File.Exists(SourceScenePath))
            {
                Debug.LogError($"Sky Circuit V0.4 build needs {SourceScenePath}. Build V0.3 first.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            scene.name = "V0_4_ProfilePrototype";

            Competitor player = FindComponentOnNamedObject<Competitor>("Player Competitor");
            Competitor opponent = FindComponentOnNamedObject<Competitor>("AI Competitor");
            MatchDebugHud hud = FindComponentOnNamedObject<MatchDebugHud>("Match HUD");
            RouteAIPilotController aiPilot = FindComponentOnNamedObject<RouteAIPilotController>("AI Competitor");

            if (player == null || opponent == null)
            {
                Debug.LogError("Sky Circuit V0.4 build could not find the V0.3 competitors.");
                return;
            }

            player.SetProfile(profiles.AllRounder, true);
            opponent.SetProfile(profiles.Speeder, true);

            PlayerProfileSwitcher switcher = player.GetComponent<PlayerProfileSwitcher>();
            if (switcher == null)
            {
                switcher = player.gameObject.AddComponent<PlayerProfileSwitcher>();
            }

            switcher.Configure(player, profiles.Speeder, profiles.Fighter, profiles.AllRounder);

            if (aiPilot != null)
            {
                aiPilot.ApplyProfile(profiles.Speeder);
            }

            if (hud != null)
            {
                hud.SetTitle("Sky Circuit V0.4 Profile Prototype");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetBuildScene(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Sky Circuit V0.4 profile prototype scene built at {ScenePath}");
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

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
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
