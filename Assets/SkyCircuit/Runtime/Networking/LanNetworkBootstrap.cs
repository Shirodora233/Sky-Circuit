using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SkyCircuit.Networking
{
    [DisallowMultipleComponent]
    public sealed class LanNetworkBootstrap : MonoBehaviour
    {
        private const string AnyIpv4Address = "0.0.0.0";
        private const string LocalhostAddress = "127.0.0.1";
        private const ushort FallbackPort = 7777;
        private const float FallbackConnectTimeoutSeconds = 3f;
        private static readonly Vector3[] DefaultSpawnPositions =
        {
            new Vector3(-8f, 18f, -35f),
            new Vector3(8f, 18f, -35f),
        };
        private static readonly Vector3[] DefaultSpawnEulerAngles =
        {
            new Vector3(0f, 18f, 0f),
            new Vector3(0f, -18f, 0f),
        };

        [SerializeField] private LanConnectionSettings settings = null;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private string clientAddressOverride;
        [SerializeField] private int portOverride;
        [SerializeField] private bool findSceneNetworkManager = true;
        [SerializeField] private bool createPlayerObjectWhenPrefabExists = true;
        [SerializeField] private Vector3[] playerSpawnPositions = DefaultSpawnPositions;
        [SerializeField] private Vector3[] playerSpawnEulerAngles = DefaultSpawnEulerAngles;

        private string statusText = "Offline";
        private bool callbacksRegistered;
        private bool approvalCallbackRegistered;

        public LanConnectionSettings Settings => settings;
        public string ClientAddress => ResolveClientAddress();
        public string StatusText => statusText;
        public ushort Port => ResolvePort();
        public bool IsListening => networkManager != null && networkManager.IsListening;
        public bool IsHost => networkManager != null && networkManager.IsHost;
        public bool IsClient => networkManager != null && networkManager.IsClient;
        public bool IsServer => networkManager != null && networkManager.IsServer;
        public ulong LocalClientId => networkManager != null ? networkManager.LocalClientId : 0UL;
        public NetworkManager NetworkManager => networkManager;

        public int ConnectedClientCount
        {
            get
            {
                if (networkManager == null || networkManager.ConnectedClientsIds == null)
                {
                    return 0;
                }

                return networkManager.ConnectedClientsIds.Count;
            }
        }

        private void Awake()
        {
            ResolveNetworkObjects();
            EnsurePreferredNetworkManager();
            RegisterCallbacks();
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
        }

        public void SetClientAddress(string address)
        {
            clientAddressOverride = address != null ? address.Trim() : string.Empty;
        }

        public void SetPort(int port)
        {
            portOverride = Mathf.Clamp(port, 1, ushort.MaxValue);
        }

        public bool StartHost()
        {
            return StartHost(ResolveClientAddress());
        }

        public bool StartHost(string hostAddress)
        {
            if (!CanStart("host"))
            {
                return false;
            }

            if (!ConfigureTransport(hostAddress, true))
            {
                return false;
            }

            bool started = networkManager.StartHost();
            statusText = started ? $"Hosting on port {Port}" : "Failed to start host";
            return started;
        }

        public bool StartServer()
        {
            if (!CanStart("server"))
            {
                return false;
            }

            if (!ConfigureTransport(LocalhostAddress, true))
            {
                return false;
            }

            bool started = networkManager.StartServer();
            statusText = started ? $"Serving on port {Port}" : "Failed to start server";
            return started;
        }

        public bool StartClient()
        {
            return StartClient(ResolveClientAddress());
        }

        public bool StartClient(string hostAddress)
        {
            if (!CanStart("client"))
            {
                return false;
            }

            if (!ConfigureTransport(hostAddress, false))
            {
                return false;
            }

            bool started = networkManager.StartClient();
            statusText = started ? $"Connecting to {ResolveClientAddress()}:{Port}" : "Failed to start client";
            return started;
        }

        public void Shutdown()
        {
            if (networkManager == null)
            {
                statusText = "Missing NetworkManager";
                return;
            }

            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }

            statusText = "Offline";
        }

        private bool CanStart(string mode)
        {
            ResolveNetworkObjects();
            RegisterCallbacks();

            if (networkManager == null)
            {
                statusText = $"Cannot start {mode}: missing NetworkManager";
                return false;
            }

            if (transport == null)
            {
                statusText = $"Cannot start {mode}: missing UnityTransport";
                return false;
            }

            if (networkManager.IsListening)
            {
                statusText = $"Already running as {DescribeMode()}";
                return false;
            }

            return true;
        }

        private bool ConfigureTransport(string hostAddress, bool hostMode)
        {
            string endpointAddress = NormalizeAddress(hostAddress);
            string listenAddress = hostMode ? AnyIpv4Address : null;

            transport.SetConnectionData(endpointAddress, Port, listenAddress);
            transport.ConnectTimeoutMS = Mathf.RoundToInt(ResolveConnectTimeoutSeconds() * 1000f);
            SetClientAddress(endpointAddress);
            return true;
        }

        private void ResolveNetworkObjects()
        {
            if (networkManager == null && NetworkManager.Singleton != null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager == null && findSceneNetworkManager)
            {
                networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }

            if (transport == null && networkManager != null)
            {
                EnsureNetworkConfig();
                transport = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
                if (transport == null)
                {
                    transport = networkManager.GetComponent<UnityTransport>();
                }
            }
        }

        private void EnsurePreferredNetworkManager()
        {
            if (networkManager == null)
            {
                return;
            }

            NetworkManager singleton = NetworkManager.Singleton;
            if (singleton == null)
            {
                networkManager.SetSingleton();
                return;
            }

            if (singleton == networkManager)
            {
                return;
            }

            if (singleton.IsListening)
            {
                if (!networkManager.IsListening)
                {
                    networkManager = singleton;
                    transport = networkManager.NetworkConfig != null
                        ? networkManager.NetworkConfig.NetworkTransport as UnityTransport
                        : null;
                    if (transport == null)
                    {
                        transport = networkManager.GetComponent<UnityTransport>();
                    }
                }

                return;
            }

            Destroy(singleton.gameObject);
            networkManager.SetSingleton();
        }

        private void RegisterCallbacks()
        {
            if (callbacksRegistered || networkManager == null)
            {
                return;
            }

            EnsureNetworkConfig();
            networkManager.NetworkConfig.ConnectionApproval = true;
            if (transport != null)
            {
                networkManager.NetworkConfig.NetworkTransport = transport;
            }

            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
            approvalCallbackRegistered = true;
            networkManager.OnClientConnectedCallback += HandleClientConnected;
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!callbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            if (approvalCallbackRegistered)
            {
                networkManager.ConnectionApprovalCallback = null;
                approvalCallbackRegistered = false;
            }

            callbacksRegistered = false;
        }

        private void EnsureNetworkConfig()
        {
            if (networkManager != null && networkManager.NetworkConfig == null)
            {
                networkManager.NetworkConfig = new NetworkConfig();
            }
        }

        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            int slotIndex = ConnectedClientCount;
            bool hasRoom = slotIndex < MaxPlayers;
            bool hasPlayerPrefab = networkManager != null
                && networkManager.NetworkConfig != null
                && networkManager.NetworkConfig.PlayerPrefab != null;
            bool shouldCreatePlayer = hasRoom && createPlayerObjectWhenPrefabExists && hasPlayerPrefab;

            response.Approved = hasRoom;
            response.CreatePlayerObject = shouldCreatePlayer;
            response.Position = shouldCreatePlayer ? ResolveSpawnPosition(slotIndex) : null;
            response.Rotation = shouldCreatePlayer ? ResolveSpawnRotation(slotIndex) : null;
            response.Pending = false;
            response.Reason = hasRoom ? string.Empty : $"Session is full ({MaxPlayers} players)";
        }

        private void HandleClientConnected(ulong clientId)
        {
            statusText = $"{DescribeMode()} connected client {clientId} ({ConnectedClientCount} connected)";
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            statusText = $"{DescribeMode()} disconnected client {clientId} ({ConnectedClientCount} connected)";
        }

        private string ResolveClientAddress()
        {
            if (!string.IsNullOrWhiteSpace(clientAddressOverride))
            {
                return clientAddressOverride.Trim();
            }

            return settings != null ? settings.DefaultHostAddress : LocalhostAddress;
        }

        private float ResolveConnectTimeoutSeconds()
        {
            return settings != null ? settings.ConnectTimeoutSeconds : FallbackConnectTimeoutSeconds;
        }

        private int MaxPlayers => settings != null ? settings.MaxPlayers : 2;

        private ushort ResolvePort()
        {
            if (portOverride > 0)
            {
                return (ushort)Mathf.Clamp(portOverride, 1, ushort.MaxValue);
            }

            return settings != null ? settings.Port : FallbackPort;
        }

        private Vector3 ResolveSpawnPosition(int slotIndex)
        {
            if (playerSpawnPositions == null || playerSpawnPositions.Length == 0)
            {
                return DefaultSpawnPositions[Mathf.Clamp(slotIndex, 0, DefaultSpawnPositions.Length - 1)];
            }

            return playerSpawnPositions[Mathf.Clamp(slotIndex, 0, playerSpawnPositions.Length - 1)];
        }

        private Quaternion ResolveSpawnRotation(int slotIndex)
        {
            if (playerSpawnEulerAngles == null || playerSpawnEulerAngles.Length == 0)
            {
                return Quaternion.Euler(DefaultSpawnEulerAngles[Mathf.Clamp(slotIndex, 0, DefaultSpawnEulerAngles.Length - 1)]);
            }

            return Quaternion.Euler(playerSpawnEulerAngles[Mathf.Clamp(slotIndex, 0, playerSpawnEulerAngles.Length - 1)]);
        }

        private static string NormalizeAddress(string address)
        {
            return string.IsNullOrWhiteSpace(address) ? LocalhostAddress : address.Trim();
        }

        private string DescribeMode()
        {
            if (networkManager == null)
            {
                return "Offline";
            }

            if (networkManager.IsHost)
            {
                return "Host";
            }

            if (networkManager.IsServer)
            {
                return "Server";
            }

            if (networkManager.IsClient)
            {
                return "Client";
            }

            return "Offline";
        }
    }
}
