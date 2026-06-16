using UnityEngine;

namespace SkyCircuit.Networking
{
    [DisallowMultipleComponent]
    public sealed class LanNetworkStatusHud : MonoBehaviour
    {
        [SerializeField] private LanNetworkBootstrap bootstrap;
        [SerializeField] private LanConnectionSettings settings;
        [SerializeField] private bool showHud = true;
        [SerializeField] private Rect area = new Rect(18f, 402f, 390f, 210f);

        private string addressText;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;

        private void Awake()
        {
            ResolveBootstrap();
            addressText = bootstrap != null
                ? bootstrap.ClientAddress
                : settings != null ? settings.DefaultHostAddress : "127.0.0.1";

            labelStyle = new GUIStyle
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            if (labelStyle == null || titleStyle == null)
            {
                Awake();
            }

            ResolveBootstrap();

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("LAN Multiplayer", titleStyle);

            if (bootstrap == null)
            {
                GUILayout.Label("Missing LanNetworkBootstrap", labelStyle);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Status: {bootstrap.StatusText}", labelStyle);
            GUILayout.Label($"Mode: {ModeText()}    Clients: {bootstrap.ConnectedClientCount}", labelStyle);
            GUILayout.Label($"Port: {bootstrap.Port}    Local Client: {bootstrap.LocalClientId}", labelStyle);

            GUILayout.Space(6f);
            GUI.enabled = !bootstrap.IsListening;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Host IP", GUILayout.Width(64f));
            addressText = GUILayout.TextField(addressText ?? string.Empty, GUILayout.Width(160f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Host", GUILayout.Height(30f)))
            {
                bootstrap.SetClientAddress(addressText);
                bootstrap.StartHost();
            }

            if (GUILayout.Button("Start Client", GUILayout.Height(30f)))
            {
                bootstrap.SetClientAddress(addressText);
                bootstrap.StartClient();
            }
            GUILayout.EndHorizontal();

            GUI.enabled = bootstrap.IsListening;
            if (GUILayout.Button("Shutdown", GUILayout.Height(28f)))
            {
                bootstrap.Shutdown();
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void ResolveBootstrap()
        {
            if (bootstrap == null)
            {
                bootstrap = FindFirstObjectByType<LanNetworkBootstrap>(FindObjectsInactive.Include);
            }

            if (settings == null && bootstrap != null)
            {
                settings = bootstrap.Settings;
            }
        }

        private string ModeText()
        {
            if (bootstrap == null || !bootstrap.IsListening)
            {
                return "Offline";
            }

            if (bootstrap.IsHost)
            {
                return "Host";
            }

            if (bootstrap.IsServer)
            {
                return "Server";
            }

            return bootstrap.IsClient ? "Client" : "Offline";
        }
    }
}
