using UnityEngine;

namespace SkyCircuit.Match
{
    public sealed class MatchDebugHud : MonoBehaviour
    {
        [SerializeField] private MatchController match;

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        public void Configure(MatchController matchController)
        {
            match = matchController;
        }

        private void Awake()
        {
            labelStyle = new GUIStyle
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (labelStyle == null)
            {
                Awake();
            }

            GUILayout.BeginArea(new Rect(18f, 18f, 460f, 220f), GUI.skin.box);
            GUILayout.Label("Sky Circuit V0.2 Match Prototype", titleStyle);
            GUILayout.Space(4f);

            if (match == null)
            {
                GUILayout.Label("Match: Missing", labelStyle);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Phase: {match.Phase}", labelStyle);
            if (match.Phase == MatchPhase.Countdown)
            {
                GUILayout.Label($"Start In: {Mathf.CeilToInt(match.CountdownRemaining)}", labelStyle);
            }
            else
            {
                GUILayout.Label($"Time: {FormatTime(match.RemainingTime)}", labelStyle);
            }

            Competitor player = match.Player;
            Competitor opponent = match.Opponent;
            GUILayout.Space(6f);
            GUILayout.Label($"{NameOf(player)}: {ScoreOf(player)}    {NameOf(opponent)}: {ScoreOf(opponent)}", labelStyle);

            if (player != null)
            {
                GUILayout.Label($"Target Buoy: {player.TargetIndex + 1}", labelStyle);
            }

            if (match.Phase == MatchPhase.Finished)
            {
                GUILayout.Space(6f);
                GUILayout.Label(match.ResultText, titleStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label("W/S speed  Mouse steer  Space/Ctrl altitude", labelStyle);
            GUILayout.EndArea();
        }

        private static string NameOf(Competitor competitor)
        {
            return competitor != null ? competitor.DisplayName : "None";
        }

        private static int ScoreOf(Competitor competitor)
        {
            return competitor != null ? competitor.Score : 0;
        }

        private static string FormatTime(float seconds)
        {
            int clamped = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{clamped / 60:0}:{clamped % 60:00}";
        }
    }
}
