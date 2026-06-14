using SkyCircuit.Combat;
using UnityEngine;

namespace SkyCircuit.Match
{
    public sealed class MatchDebugHud : MonoBehaviour
    {
        [SerializeField] private MatchController match;
        [SerializeField] private DogfightController dogfight;
        [SerializeField] private string title = "Sky Circuit V0.2 Match Prototype";

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        public void Configure(MatchController matchController)
        {
            match = matchController;
        }

        public void ConfigureDogfight(DogfightController dogfightController)
        {
            dogfight = dogfightController;
            title = "Sky Circuit V0.3 Dogfight Prototype";
        }

        public void SetTitle(string hudTitle)
        {
            title = hudTitle;
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

            GUILayout.BeginArea(new Rect(18f, 18f, 560f, 365f), GUI.skin.box);
            GUILayout.Label(title, titleStyle);
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
            GUILayout.Label($"Shoes: {ProfileNameOf(player)}    AI: {ProfileNameOf(opponent)}", labelStyle);
            GUILayout.Label($"Buoys: {BuoyScoreOf(player)} / {BuoyScoreOf(opponent)}    Back Hits: {BackHitScoreOf(player)} / {BackHitScoreOf(opponent)}", labelStyle);
            GUILayout.Label($"Speed: {SpeedText(player)}    AI: {SpeedText(opponent)}", labelStyle);
            GUILayout.Label($"Dash: {DashText(player)}    AI: {DashText(opponent)}", labelStyle);

            if (player != null)
            {
                GUILayout.Label($"Target Buoy: {player.TargetIndex + 1}", labelStyle);
            }

            if (dogfight != null)
            {
                string dogfightState = dogfight.IsUnlocked ? "Unlocked" : "Locked";
                GUILayout.Label($"Dogfight: {dogfightState}    Cooldown: {dogfight.CooldownRemaining:0.0}", labelStyle);
                GUILayout.Label(dogfight.LastHitText, labelStyle);
            }

            if (match.Phase == MatchPhase.Finished)
            {
                GUILayout.Space(6f);
                GUILayout.Label(match.ResultText, titleStyle);
            }

            GUILayout.Space(6f);
            GUILayout.Label("1 Speeder  2 Fighter  3 All-Rounder", labelStyle);
            GUILayout.Label("W/S speed  Mouse steer  Space/Ctrl altitude  Q dash", labelStyle);
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

        private static int BuoyScoreOf(Competitor competitor)
        {
            return competitor != null ? competitor.BuoyScoreCount : 0;
        }

        private static int BackHitScoreOf(Competitor competitor)
        {
            return competitor != null ? competitor.BackHitScoreCount : 0;
        }

        private static string ProfileNameOf(Competitor competitor)
        {
            return competitor != null ? competitor.ProfileName : "--";
        }

        private static string SpeedText(Competitor competitor)
        {
            if (competitor == null || competitor.Controller == null)
            {
                return "--";
            }

            float flightSpeed = competitor.Controller.CurrentSpeed;
            float bodySpeed = BodySpeedOf(competitor);
            return $"{flightSpeed:0.0} (Vel {bodySpeed:0.0})";
        }

        private static string DashText(Competitor competitor)
        {
            if (competitor == null || competitor.Controller == null)
            {
                return "--";
            }

            string state = string.Empty;
            if (competitor.Controller.IsDashing)
            {
                state = " Dashing";
            }
            else if (competitor.Controller.IsDashCoolingDown)
            {
                state = $" CD {competitor.Controller.DashCooldownRemaining:0.0}";
            }
            else if (competitor.Controller.RequiresDashRelease)
            {
                state = " Release";
            }

            return $"{competitor.Controller.DashCharge:0}/{competitor.Controller.DashMaxCharge:0}{state}";
        }

        private static float BodySpeedOf(Competitor competitor)
        {
            Rigidbody body = competitor.Body != null ? competitor.Body.GetComponent<Rigidbody>() : null;
            return body != null ? body.linearVelocity.magnitude : 0f;
        }

        private static string FormatTime(float seconds)
        {
            int clamped = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{clamped / 60:0}:{clamped % 60:00}";
        }
    }
}
