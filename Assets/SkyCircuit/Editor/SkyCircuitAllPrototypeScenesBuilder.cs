using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitAllPrototypeScenesBuilder
    {
        private static readonly string[] PrototypeScenes =
        {
            "Assets/Scenes/V0_1_FlightPrototype.unity",
            "Assets/Scenes/V0_2_MatchPrototype.unity",
            "Assets/Scenes/V0_3_DogfightPrototype.unity",
            "Assets/Scenes/V0_4_ProfilePrototype.unity",
            "Assets/Scenes/V0_6_PresentationSlice.unity",
        };

        [MenuItem("Sky Circuit/Build All Prototype Scenes")]
        public static void BuildAllPrototypeScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot build prototype scenes while Unity is in Play Mode.");
                return;
            }

            SkyCircuitV01SceneBuilder.BuildPrototypeScene();
            SkyCircuitV02SceneBuilder.BuildMatchPrototypeScene();
            SkyCircuitV03SceneBuilder.BuildDogfightPrototypeScene();
            SkyCircuitV04SceneBuilder.BuildProfilePrototypeScene();
            SkyCircuitV06PresentationSliceBuilder.BuildCharacterFlightDemoScene();

            EditorBuildSettings.scenes = CreateBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene("Assets/Scenes/V0_6_PresentationSlice.unity");
        }

        private static EditorBuildSettingsScene[] CreateBuildScenes()
        {
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[PrototypeScenes.Length];
            for (int i = 0; i < PrototypeScenes.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(PrototypeScenes[i], true);
            }

            return scenes;
        }
    }
}
