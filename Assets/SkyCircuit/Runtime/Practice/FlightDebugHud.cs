using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Practice
{
    public sealed class FlightDebugHud : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private FlightPracticeRoute route;

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        public void Configure(SkyCircuitFlightController flightController, FlightPracticeRoute practiceRoute)
        {
            controller = flightController;
            route = practiceRoute;
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

            GUILayout.BeginArea(new Rect(18f, 18f, 430f, 170f), GUI.skin.box);
            GUILayout.Label("Sky Circuit V0.1 Flight Prototype", titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("W/S speed  A/D turn  Mouse look  Space/Ctrl altitude  Shift boost", labelStyle);
            GUILayout.Label("Esc unlocks cursor. Click the game view to lock it again.", labelStyle);

            if (controller != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Speed: {controller.CurrentSpeed:0.0}", labelStyle);
            }

            if (route != null && route.BuoyCount > 0)
            {
                GUILayout.Label($"Target Buoy: {route.TargetIndex + 1}/{route.BuoyCount}    Gates Touched: {route.GatesTouched}", labelStyle);
            }

            GUILayout.EndArea();
        }
    }
}
