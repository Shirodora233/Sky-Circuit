using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Presentation
{
    public sealed class PresentationFlightDemoHud : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private string title = "Sky Circuit V0.6 Character Flight Demo";

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        public void Configure(SkyCircuitFlightController flightController, string hudTitle)
        {
            controller = flightController;
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

            GUILayout.BeginArea(new Rect(18f, 18f, 470f, 175f), GUI.skin.box);
            GUILayout.Label(title, titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("W/S speed  A/D turn  Mouse look  Space/Ctrl altitude  Q dash", labelStyle);
            GUILayout.Label("Esc unlocks cursor. Click the game view to lock it again.", labelStyle);

            if (controller != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Speed: {controller.CurrentSpeed:0.0}", labelStyle);
                GUILayout.Label($"Dash: {controller.DashCharge:0}/{controller.DashMaxCharge:0}{DashStateText(controller)}", labelStyle);
            }

            GUILayout.EndArea();
        }

        private static string DashStateText(SkyCircuitFlightController controller)
        {
            if (controller.IsDashing)
            {
                return " Dashing";
            }

            if (controller.IsDashCoolingDown)
            {
                return $" CD {controller.DashCooldownRemaining:0.0}";
            }

            return controller.RequiresDashRelease ? " Release" : string.Empty;
        }
    }
}
