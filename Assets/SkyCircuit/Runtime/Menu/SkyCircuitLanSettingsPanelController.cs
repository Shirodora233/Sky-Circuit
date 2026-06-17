using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SkyCircuit.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCircuit.Menu
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SkyCircuitLanSettingsPanelController : MonoBehaviour
    {
        private const string DefaultAddress = "127.0.0.1";
        private const int DefaultPort = 7777;

        [SerializeField] private LanNetworkBootstrap bootstrap;
        [SerializeField] private InputField targetAddressInput;
        [SerializeField] private InputField clientPortInput;
        [SerializeField] private InputField listenPortInput;
        [SerializeField] private Text localIpListText;
        [SerializeField] private Text clientStatusText;
        [SerializeField] private Text serverStatusText;
        [SerializeField] private Text clientButtonText;
        [SerializeField] private Text serverButtonText;
        [SerializeField] private Text hintText;

        private readonly List<string> localIpAddresses = new List<string>();
        private float nextStatusRefreshTime;

        public void Configure(
            LanNetworkBootstrap networkBootstrap,
            InputField targetAddress,
            InputField clientPort,
            InputField listenPort,
            Text localIpList,
            Text clientStatus,
            Text serverStatus,
            Text clientActionLabel,
            Text serverActionLabel,
            Text hint)
        {
            bootstrap = networkBootstrap;
            targetAddressInput = targetAddress;
            clientPortInput = clientPort;
            listenPortInput = listenPort;
            localIpListText = localIpList;
            clientStatusText = clientStatus;
            serverStatusText = serverStatus;
            clientButtonText = clientActionLabel;
            serverButtonText = serverActionLabel;
            hintText = hint;
        }

        private void Awake()
        {
            ResolveBootstrap();
            InitializeInputs();
            RefreshNetworkInfo(false);
        }

        private void OnEnable()
        {
            ResolveBootstrap();
            InitializeInputs();
            RefreshNetworkInfo(false);
            RefreshStatusText();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime < nextStatusRefreshTime)
            {
                return;
            }

            nextStatusRefreshTime = Time.unscaledTime + 0.35f;
            RefreshStatusText();
        }

        public void ClearClientFields()
        {
            SetInputText(targetAddressInput, string.Empty);
            SetInputText(clientPortInput, ResolvePort().ToString());
            ShowClientStatus("\u5ba2\u6237\u7aef\u8f93\u5165\u5df2\u6e05\u7a7a");
        }

        public void RefreshNetworkInfo()
        {
            RefreshNetworkInfo(true);
        }

        private void RefreshNetworkInfo(bool showMessage)
        {
            localIpAddresses.Clear();
            CollectLocalIpv4Addresses(localIpAddresses);
            if (localIpAddresses.Count == 0)
            {
                localIpAddresses.Add(DefaultAddress);
            }

            if (localIpListText != null)
            {
                localIpListText.text = string.Join("\n", localIpAddresses);
            }

            if (string.IsNullOrWhiteSpace(GetInputText(clientPortInput)))
            {
                SetInputText(clientPortInput, ResolvePort().ToString());
            }

            if (string.IsNullOrWhiteSpace(GetInputText(listenPortInput)))
            {
                SetInputText(listenPortInput, ResolvePort().ToString());
            }

            if (showMessage)
            {
                ShowHint("\u7f51\u7edc\u4fe1\u606f\u5df2\u5237\u65b0");
            }
        }

        public void StartClient()
        {
            ResolveBootstrap();
            if (bootstrap == null)
            {
                ShowClientStatus("\u7f3a\u5c11 LAN \u8054\u673a\u6a21\u5757");
                return;
            }

            if (bootstrap.IsListening)
            {
                if (bootstrap.IsClient && !bootstrap.IsServer)
                {
                    bootstrap.Shutdown();
                    ShowClientStatus("\u5df2\u53d6\u6d88\u8fde\u63a5");
                    SetClientButtonText(false, false);
                    return;
                }

                ShowClientStatus("\u5f53\u524d\u6b63\u5728\u4f5c\u4e3a\u670d\u52a1\u7aef\u8fd0\u884c");
                return;
            }

            string address = NormalizeAddress(GetInputText(targetAddressInput));
            int port = ParsePort(GetInputText(clientPortInput));
            SetInputText(targetAddressInput, address);
            SetInputText(clientPortInput, port.ToString());

            bootstrap.SetClientAddress(address);
            bootstrap.SetPort(port);
            bool started = bootstrap.StartClient(address);
            ShowClientStatus(started ? $"\u6b63\u5728\u8fde\u63a5 {address}:{port}" : bootstrap.StatusText);
            SetClientButtonText(started, false);
            RefreshStatusText();
        }

        public void StartServer()
        {
            ResolveBootstrap();
            if (bootstrap == null)
            {
                ShowServerStatus("\u7f3a\u5c11 LAN \u8054\u673a\u6a21\u5757");
                return;
            }

            if (bootstrap.IsListening && bootstrap.IsServer)
            {
                bootstrap.Shutdown();
                ShowServerStatus("\u670d\u52a1\u7aef\u5df2\u5173\u95ed");
                SetServerButtonText(false);
                return;
            }

            int port = ParsePort(GetInputText(listenPortInput));
            SetInputText(listenPortInput, port.ToString());
            bootstrap.SetPort(port);
            bool started = bootstrap.StartServer();
            ShowServerStatus(started ? $"\u670d\u52a1\u7aef\u5df2\u5f00\u542f\uff0c\u7aef\u53e3 {port}" : bootstrap.StatusText);
            RefreshStatusText();
        }

        public void CopyPrimaryLocalIp()
        {
            if (localIpAddresses.Count == 0)
            {
                RefreshNetworkInfo();
            }

            string address = localIpAddresses.Count > 0 ? localIpAddresses[0] : DefaultAddress;
            GUIUtility.systemCopyBuffer = address;
            ShowServerStatus($"\u5df2\u590d\u5236 IP\uff1a{address}");
        }

        private void InitializeInputs()
        {
            int port = ResolvePort();
            if (string.IsNullOrWhiteSpace(GetInputText(targetAddressInput)))
            {
                SetInputText(targetAddressInput, bootstrap != null ? bootstrap.ClientAddress : DefaultAddress);
            }

            if (string.IsNullOrWhiteSpace(GetInputText(clientPortInput)))
            {
                SetInputText(clientPortInput, port.ToString());
            }

            if (string.IsNullOrWhiteSpace(GetInputText(listenPortInput)))
            {
                SetInputText(listenPortInput, port.ToString());
            }
        }

        private void RefreshStatusText()
        {
            if (bootstrap == null)
            {
                ResolveBootstrap();
            }

            if (bootstrap == null)
            {
                SetClientStatusText("\u672a\u8fde\u63a5");
                SetServerStatusText("\u7f3a\u5c11\u6a21\u5757");
                SetClientButtonText(false, false);
                SetServerButtonText(false);
                return;
            }

            bool serverRunning = bootstrap.IsListening && bootstrap.IsServer;
            bool clientRunning = bootstrap.IsListening && bootstrap.IsClient && !bootstrap.IsServer;
            bool clientConnected = clientRunning && bootstrap.ConnectedClientCount > 0;
            SetClientButtonText(clientRunning, clientConnected);
            SetServerButtonText(serverRunning);

            if (clientRunning)
            {
                string clientState = clientConnected ? "\u5df2\u8fde\u63a5" : "\u8fde\u63a5\u4e2d";
                SetClientStatusText($"{clientState} | {bootstrap.ConnectedClientCount}");
            }
            else if (bootstrap.IsHost)
            {
                SetClientStatusText("\u672c\u673a\u4e3b\u673a");
            }
            else
            {
                SetClientStatusText("\u672a\u8fde\u63a5");
            }

            if (serverRunning)
            {
                SetServerStatusText($"\u5df2\u5f00\u542f | {bootstrap.ConnectedClientCount}");
            }
            else
            {
                SetServerStatusText("\u672a\u5f00\u542f");
            }
        }

        private void ShowClientStatus(string message)
        {
            SetClientStatusText(message);
            ShowHint(message);
        }

        private void ShowServerStatus(string message)
        {
            SetServerStatusText(message);
            ShowHint(message);
        }

        private void ShowHint(string message)
        {
            if (hintText != null)
            {
                hintText.text = message;
            }
        }

        private void SetClientStatusText(string message)
        {
            if (clientStatusText != null)
            {
                clientStatusText.text = message;
            }
        }

        private void SetServerStatusText(string message)
        {
            if (serverStatusText != null)
            {
                serverStatusText.text = message;
            }
        }

        private void SetClientButtonText(bool clientRunning, bool clientConnected)
        {
            if (clientButtonText != null)
            {
                clientButtonText.text = clientRunning
                    ? (clientConnected ? "\u65ad\u5f00\u8fde\u63a5" : "\u53d6\u6d88\u8fde\u63a5")
                    : "\u8fde\u63a5";
            }
        }

        private void SetServerButtonText(bool serverRunning)
        {
            if (serverButtonText != null)
            {
                serverButtonText.text = serverRunning ? "\u5173\u95ed\u670d\u52a1\u7aef" : "\u5f00\u542f\u670d\u52a1\u7aef";
            }
        }

        private void ResolveBootstrap()
        {
            if (bootstrap == null)
            {
                bootstrap = FindAnyObjectByType<LanNetworkBootstrap>(FindObjectsInactive.Include);
            }
        }

        private int ResolvePort()
        {
            return bootstrap != null ? bootstrap.Port : DefaultPort;
        }

        private static int ParsePort(string value)
        {
            if (!int.TryParse(value, out int port))
            {
                return DefaultPort;
            }

            return Mathf.Clamp(port, 1, ushort.MaxValue);
        }

        private static string NormalizeAddress(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultAddress : value.Trim();
        }

        private static string GetInputText(InputField input)
        {
            return input != null ? input.text : string.Empty;
        }

        private static void SetInputText(InputField input, string value)
        {
            if (input != null)
            {
                input.text = value ?? string.Empty;
            }
        }

        private static void CollectLocalIpv4Addresses(List<string> results)
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
                {
                    IPAddress address = addressInfo.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    {
                        continue;
                    }

                    string text = address.ToString();
                    if (!results.Contains(text))
                    {
                        results.Add(text);
                    }
                }
            }

            results.Sort(ComparePreferredIpv4Address);
        }

        private static int ComparePreferredIpv4Address(string left, string right)
        {
            return GetIpv4Preference(left).CompareTo(GetIpv4Preference(right));
        }

        private static int GetIpv4Preference(string address)
        {
            if (!IPAddress.TryParse(address, out IPAddress parsed))
            {
                return 100;
            }

            byte[] bytes = parsed.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return 100;
            }

            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return 0;
            }

            if (bytes[0] == 10)
            {
                return 1;
            }

            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return 2;
            }

            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return 90;
            }

            if (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19))
            {
                return 95;
            }

            return 50;
        }
    }
}
