using UnityEngine;

namespace SkyCircuit.Networking
{
    [CreateAssetMenu(fileName = "SC_LanConnectionSettings", menuName = "Sky Circuit/Networking/LAN Connection Settings")]
    public sealed class LanConnectionSettings : ScriptableObject
    {
        private const int MinPort = 1;
        private const int MaxPort = 65535;
        private const int RequiredPlayerCount = 2;

        [Header("LAN Endpoint")]
        [SerializeField] private string defaultHostAddress = "127.0.0.1";
        [SerializeField, Range(MinPort, MaxPort)] private int port = 7777;

        [Header("Session")]
        [SerializeField, Min(RequiredPlayerCount)] private int maxPlayers = RequiredPlayerCount;
        [SerializeField, Min(1f)] private float connectTimeoutSeconds = 3f;

        [Header("Replication Rates")]
        [SerializeField, Range(10f, 60f)] private float inputSendRate = 30f;
        [SerializeField, Range(10f, 60f)] private float stateSendRate = 20f;
        [SerializeField, Min(0f)] private float remoteInterpolationDelay = 0.1f;

        public string DefaultHostAddress => string.IsNullOrWhiteSpace(defaultHostAddress) ? "127.0.0.1" : defaultHostAddress;
        public ushort Port => (ushort)Mathf.Clamp(port, MinPort, MaxPort);
        public int MaxPlayers => Mathf.Max(RequiredPlayerCount, maxPlayers);
        public float ConnectTimeoutSeconds => Mathf.Max(1f, connectTimeoutSeconds);
        public float InputSendRate => Mathf.Clamp(inputSendRate, 10f, 60f);
        public float StateSendRate => Mathf.Clamp(stateSendRate, 10f, 60f);
        public float RemoteInterpolationDelay => Mathf.Max(0f, remoteInterpolationDelay);

        private void OnValidate()
        {
            port = Mathf.Clamp(port, MinPort, MaxPort);
            maxPlayers = Mathf.Max(RequiredPlayerCount, maxPlayers);
            connectTimeoutSeconds = Mathf.Max(1f, connectTimeoutSeconds);
            inputSendRate = Mathf.Clamp(inputSendRate, 10f, 60f);
            stateSendRate = Mathf.Clamp(stateSendRate, 10f, 60f);
            remoteInterpolationDelay = Mathf.Max(0f, remoteInterpolationDelay);
        }
    }
}
