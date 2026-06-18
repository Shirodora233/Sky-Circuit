using UnityEngine;
using System.Collections.Generic;
using SkyCircuit.Profiles;

namespace SkyCircuit.Race
{
    public static class RaceLaunchRequest
    {
        private const string ModeKey = "SkyCircuit.Race.Mode";
        private const string PlayerArchetypeKey = "SkyCircuit.Race.PlayerArchetype";
        private const string AiArchetypeKey = "SkyCircuit.Race.AiArchetype";

        private static readonly Dictionary<ulong, CompetitorArchetype> lanSelections =
            new Dictionary<ulong, CompetitorArchetype>();
        private static RaceMode pendingMode = RaceMode.None;
        private static CompetitorArchetype pendingPlayerArchetype = CompetitorArchetype.AllRounder;
        private static CompetitorArchetype pendingAiArchetype = CompetitorArchetype.AllRounder;

        public static void Request(RaceMode mode)
        {
            Request(mode, CompetitorArchetype.AllRounder, CompetitorArchetype.AllRounder);
        }

        public static void Request(
            RaceMode mode,
            CompetitorArchetype playerArchetype,
            CompetitorArchetype aiArchetype)
        {
            pendingMode = mode;
            pendingPlayerArchetype = playerArchetype;
            pendingAiArchetype = aiArchetype;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.SetInt(PlayerArchetypeKey, (int)playerArchetype);
            PlayerPrefs.SetInt(AiArchetypeKey, (int)aiArchetype);
            PlayerPrefs.Save();
        }

        public static RaceMode Resolve()
        {
            if (pendingMode != RaceMode.None)
            {
                return pendingMode;
            }

            int storedMode = PlayerPrefs.GetInt(ModeKey, (int)RaceMode.None);
            return System.Enum.IsDefined(typeof(RaceMode), storedMode)
                ? (RaceMode)storedMode
                : RaceMode.None;
        }

        public static CompetitorArchetype ResolvePlayerArchetype()
        {
            if (pendingMode != RaceMode.None)
            {
                return pendingPlayerArchetype;
            }

            return ResolveArchetype(PlayerArchetypeKey, CompetitorArchetype.AllRounder);
        }

        public static CompetitorArchetype ResolveAiArchetype()
        {
            if (pendingMode != RaceMode.None)
            {
                return pendingAiArchetype;
            }

            return ResolveArchetype(AiArchetypeKey, CompetitorArchetype.AllRounder);
        }

        public static void SetLanSelection(ulong clientId, CompetitorArchetype archetype)
        {
            lanSelections[clientId] = archetype;
        }

        public static bool TryGetLanSelection(ulong clientId, out CompetitorArchetype archetype)
        {
            return lanSelections.TryGetValue(clientId, out archetype);
        }

        public static void ClearLanSelections()
        {
            lanSelections.Clear();
        }

        public static void Clear()
        {
            pendingMode = RaceMode.None;
            PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.DeleteKey(PlayerArchetypeKey);
            PlayerPrefs.DeleteKey(AiArchetypeKey);
            PlayerPrefs.Save();
        }

        private static CompetitorArchetype ResolveArchetype(string key, CompetitorArchetype fallback)
        {
            int stored = PlayerPrefs.GetInt(key, (int)fallback);
            return System.Enum.IsDefined(typeof(CompetitorArchetype), stored)
                ? (CompetitorArchetype)stored
                : fallback;
        }
    }
}
