using SkyCircuit.Profiles;
using UnityEngine;

namespace SkyCircuit.Race
{
    [CreateAssetMenu(fileName = "SC_RaceProfileCatalog", menuName = "Sky Circuit/Race Profile Catalog")]
    public sealed class RaceProfileCatalog : ScriptableObject
    {
        private const string ResourcePath = "SkyCircuit/RaceProfileCatalog";

        [SerializeField] private CompetitorProfile speederProfile = null;
        [SerializeField] private CompetitorProfile fighterProfile = null;
        [SerializeField] private CompetitorProfile allRounderProfile = null;

        private static RaceProfileCatalog cachedCatalog;
        private static CompetitorProfile fallbackSpeeder;
        private static CompetitorProfile fallbackFighter;
        private static CompetitorProfile fallbackAllRounder;

        public CompetitorProfile Resolve(CompetitorArchetype archetype)
        {
            switch (archetype)
            {
                case CompetitorArchetype.Speeder:
                    return speederProfile != null ? speederProfile : Fallback(archetype);
                case CompetitorArchetype.Fighter:
                    return fighterProfile != null ? fighterProfile : Fallback(archetype);
                default:
                    return allRounderProfile != null ? allRounderProfile : Fallback(CompetitorArchetype.AllRounder);
            }
        }

        public static CompetitorProfile ResolveDefault(CompetitorArchetype archetype)
        {
            RaceProfileCatalog catalog = cachedCatalog != null
                ? cachedCatalog
                : Resources.Load<RaceProfileCatalog>(ResourcePath);
            cachedCatalog = catalog;
            return catalog != null ? catalog.Resolve(archetype) : Fallback(archetype);
        }

        private static CompetitorProfile Fallback(CompetitorArchetype archetype)
        {
            switch (archetype)
            {
                case CompetitorArchetype.Speeder:
                    fallbackSpeeder = fallbackSpeeder != null
                        ? fallbackSpeeder
                        : CreateProfile("Speeder", CompetitorArchetype.Speeder);
                    return fallbackSpeeder;
                case CompetitorArchetype.Fighter:
                    fallbackFighter = fallbackFighter != null
                        ? fallbackFighter
                        : CreateProfile("Fighter", CompetitorArchetype.Fighter);
                    return fallbackFighter;
                default:
                    fallbackAllRounder = fallbackAllRounder != null
                        ? fallbackAllRounder
                        : CreateProfile("All-Rounder", CompetitorArchetype.AllRounder);
                    return fallbackAllRounder;
            }
        }

        private static CompetitorProfile CreateProfile(string displayName, CompetitorArchetype archetype)
        {
            CompetitorProfile profile = CreateInstance<CompetitorProfile>();
            switch (archetype)
            {
                case CompetitorArchetype.Speeder:
                    profile.Configure(
                        displayName,
                        archetype,
                        FlightSpeedSettings.Speeder(),
                        FlightSteeringSettings.Speeder(),
                        DashSkillSettings.Speeder(),
                        RouteAIPilotSettings.Speeder());
                    break;
                case CompetitorArchetype.Fighter:
                    profile.Configure(
                        displayName,
                        archetype,
                        FlightSpeedSettings.Fighter(),
                        FlightSteeringSettings.Fighter(),
                        DashSkillSettings.Fighter(),
                        RouteAIPilotSettings.Fighter());
                    break;
                default:
                    profile.Configure(
                        displayName,
                        CompetitorArchetype.AllRounder,
                        FlightSpeedSettings.AllRounder(),
                        FlightSteeringSettings.AllRounder(),
                        DashSkillSettings.AllRounder(),
                        RouteAIPilotSettings.AllRounder());
                    break;
            }

            return profile;
        }
    }
}
