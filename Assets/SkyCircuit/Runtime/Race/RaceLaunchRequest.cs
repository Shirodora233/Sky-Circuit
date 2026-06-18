using UnityEngine;

namespace SkyCircuit.Race
{
    public static class RaceLaunchRequest
    {
        private const string ModeKey = "SkyCircuit.Race.Mode";

        private static RaceMode pendingMode = RaceMode.None;

        public static void Request(RaceMode mode)
        {
            pendingMode = mode;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
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

        public static void Clear()
        {
            pendingMode = RaceMode.None;
            PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.Save();
        }
    }
}
