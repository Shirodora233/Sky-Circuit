using SkyCircuit.Profiles;
using UnityEditor;
using UnityEngine;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitDefaultProfiles
    {
        public const string ProfilesFolder = "Assets/SkyCircuit/Configs/CompetitorProfiles";
        public const string SpeederPath = ProfilesFolder + "/SC_Speeder.asset";
        public const string FighterPath = ProfilesFolder + "/SC_Fighter.asset";
        public const string AllRounderPath = ProfilesFolder + "/SC_AllRounder.asset";

        [MenuItem("Sky Circuit/Profiles/Create Missing Default Profiles")]
        public static void CreateMissingDefaultProfiles()
        {
            EnsureDefaultProfiles(false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Sky Circuit/Profiles/Reset Default Profiles To Recommended Values")]
        public static void ResetDefaultProfiles()
        {
            EnsureDefaultProfiles(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static ProfileSet EnsureDefaultProfiles(bool overwriteExisting)
        {
            EnsureFolders();

            CompetitorProfile speeder = EnsureProfile(
                SpeederPath,
                "Speeder",
                CompetitorArchetype.Speeder,
                FlightSpeedSettings.Speeder(),
                FlightSteeringSettings.Speeder(),
                RouteAIPilotSettings.Speeder(),
                overwriteExisting);

            CompetitorProfile fighter = EnsureProfile(
                FighterPath,
                "Fighter",
                CompetitorArchetype.Fighter,
                FlightSpeedSettings.Fighter(),
                FlightSteeringSettings.Fighter(),
                RouteAIPilotSettings.Fighter(),
                overwriteExisting);

            CompetitorProfile allRounder = EnsureProfile(
                AllRounderPath,
                "All-Rounder",
                CompetitorArchetype.AllRounder,
                FlightSpeedSettings.AllRounder(),
                FlightSteeringSettings.AllRounder(),
                RouteAIPilotSettings.AllRounder(),
                overwriteExisting);

            return new ProfileSet(speeder, fighter, allRounder);
        }

        private static CompetitorProfile EnsureProfile(
            string path,
            string displayName,
            CompetitorArchetype archetype,
            FlightSpeedSettings speed,
            FlightSteeringSettings steering,
            RouteAIPilotSettings aiPilot,
            bool overwriteExisting)
        {
            CompetitorProfile profile = AssetDatabase.LoadAssetAtPath<CompetitorProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CompetitorProfile>();
                AssetDatabase.CreateAsset(profile, path);
                overwriteExisting = true;
            }

            if (overwriteExisting)
            {
                profile.Configure(displayName, archetype, speed, steering, aiPilot);
                EditorUtility.SetDirty(profile);
            }

            return profile;
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "SkyCircuit");
            CreateFolder("Assets/SkyCircuit", "Configs");
            CreateFolder("Assets/SkyCircuit/Configs", "CompetitorProfiles");
        }

        private static void CreateFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        public readonly struct ProfileSet
        {
            public readonly CompetitorProfile Speeder;
            public readonly CompetitorProfile Fighter;
            public readonly CompetitorProfile AllRounder;

            public ProfileSet(CompetitorProfile speeder, CompetitorProfile fighter, CompetitorProfile allRounder)
            {
                Speeder = speeder;
                Fighter = fighter;
                AllRounder = allRounder;
            }
        }
    }
}
