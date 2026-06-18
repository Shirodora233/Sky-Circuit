using System;
using System.Collections.Generic;
using SkyCircuit.Networking;
using SkyCircuit.Profiles;
using SkyCircuit.Race;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SkyCircuit.Menu
{
    [DisallowMultipleComponent]
    public sealed class LanRaceReadyCoordinator : MonoBehaviour
    {
        private const int MaxPlayers = 2;
        private const string ReadyRequestMessage = "SkyCircuit.Race.Ready.Request";
        private const string ReadyStateMessage = "SkyCircuit.Race.Ready.State";
        private const float PreSceneCountdownSeconds = 3f;
        private const float BroadcastInterval = 0.12f;

        [SerializeField] private LanNetworkBootstrap lanBootstrap;

        private readonly RaceReadyEntry[] entries = new RaceReadyEntry[MaxPlayers];
        private NetworkManager networkManager;
        private bool handlersRegistered;
        private bool countdownActive;
        private bool sceneLoadRequested;
        private float countdownRemaining;
        private float broadcastTimer;
        private CompetitorArchetype localArchetype = CompetitorArchetype.AllRounder;
        private bool localReady;

        public event Action CountdownCompleted;

        public bool IsListening => networkManager != null && networkManager.IsListening;
        public bool IsServer => networkManager != null && networkManager.IsListening && networkManager.IsServer;
        public bool LocalReady => FindEntry(LocalClientId, out RaceReadyEntry entry) ? entry.Ready : localReady;
        public bool CountdownActive => countdownActive;
        public float CountdownRemaining => countdownRemaining;
        public CompetitorArchetype LocalArchetype => localArchetype;
        public ulong LocalClientId => networkManager != null ? networkManager.LocalClientId : 0UL;

        public void Configure(LanNetworkBootstrap bootstrap)
        {
            lanBootstrap = bootstrap;
            ResolveNetworkManager();
        }

        public bool TryGetEntry(int slot, out ulong clientId, out CompetitorArchetype archetype, out bool ready)
        {
            if (slot < 0 || slot >= entries.Length || !entries[slot].Occupied)
            {
                clientId = 0UL;
                archetype = CompetitorArchetype.AllRounder;
                ready = false;
                return false;
            }

            clientId = entries[slot].ClientId;
            archetype = entries[slot].Archetype;
            ready = entries[slot].Ready;
            return true;
        }

        public void SetLocalReady(CompetitorArchetype archetype, bool ready)
        {
            localArchetype = archetype;
            localReady = ready;
            RaceLaunchRequest.Request(RaceMode.LanMultiplayer, archetype, CompetitorArchetype.AllRounder);
            RaceLaunchRequest.SetLanSelection(LocalClientId, archetype);
            ResolveNetworkManager();

            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            if (networkManager.IsServer)
            {
                ApplyReadyState(networkManager.LocalClientId, archetype, ready);
                BroadcastState(true);
                return;
            }

            SendReadyRequest(archetype, ready);
        }

        public void ResetReady()
        {
            localReady = false;
            countdownActive = false;
            sceneLoadRequested = false;
            RaceLaunchRequest.ClearLanSelections();
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = default;
            }
        }

        private void Update()
        {
            ResolveNetworkManager();
            if (networkManager == null || !networkManager.IsListening)
            {
                if (handlersRegistered)
                {
                    UnregisterHandlers();
                }

                ResetReady();
                return;
            }

            RegisterHandlers();
            if (!networkManager.IsServer)
            {
                return;
            }

            RefreshServerEntries();
            if (!AllConnectedPlayersReady())
            {
                if (countdownActive)
                {
                    countdownActive = false;
                    countdownRemaining = 0f;
                    sceneLoadRequested = false;
                    BroadcastState(true);
                }

                return;
            }

            UpdateServerCountdown();
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        private void ResolveNetworkManager()
        {
            if (networkManager == null && lanBootstrap != null)
            {
                networkManager = lanBootstrap.NetworkManager;
            }

            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }
        }

        private void RegisterHandlers()
        {
            if (handlersRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReadyStateMessage, HandleReadyStateMessage);
            if (networkManager.IsServer)
            {
                networkManager.CustomMessagingManager.RegisterNamedMessageHandler(ReadyRequestMessage, HandleReadyRequestMessage);
            }

            handlersRegistered = true;
        }

        private void UnregisterHandlers()
        {
            if (!handlersRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
            {
                handlersRegistered = false;
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReadyStateMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReadyRequestMessage);
            handlersRegistered = false;
        }

        private void SendReadyRequest(CompetitorArchetype archetype, bool ready)
        {
            if (networkManager == null || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe((int)archetype);
                writer.WriteValueSafe(ready);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    ReadyRequestMessage,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        private void HandleReadyRequestMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int archetypeValue);
            reader.ReadValueSafe(out bool ready);
            ApplyReadyState(senderClientId, ClampArchetype(archetypeValue), ready);
            BroadcastState(true);
        }

        private void HandleReadyStateMessage(ulong senderClientId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool incomingCountdownActive);
            reader.ReadValueSafe(out float incomingCountdownRemaining);
            countdownActive = incomingCountdownActive;
            countdownRemaining = incomingCountdownRemaining;

            for (int i = 0; i < entries.Length; i++)
            {
                reader.ReadValueSafe(out bool occupied);
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out int archetypeValue);
                reader.ReadValueSafe(out bool ready);
                entries[i] = new RaceReadyEntry
                {
                    Occupied = occupied,
                    ClientId = clientId,
                    Archetype = ClampArchetype(archetypeValue),
                    Ready = ready,
                };

                if (occupied)
                {
                    RaceLaunchRequest.SetLanSelection(clientId, entries[i].Archetype);
                    if (clientId == LocalClientId)
                    {
                        localReady = ready;
                        localArchetype = entries[i].Archetype;
                    }
                }
            }
        }

        private void ApplyReadyState(ulong clientId, CompetitorArchetype archetype, bool ready)
        {
            int slot = FindOrCreateSlot(clientId);
            if (slot < 0)
            {
                return;
            }

            entries[slot] = new RaceReadyEntry
            {
                Occupied = true,
                ClientId = clientId,
                Archetype = archetype,
                Ready = ready,
            };

            RaceLaunchRequest.SetLanSelection(clientId, archetype);
            if (!ready)
            {
                countdownActive = false;
                countdownRemaining = 0f;
                sceneLoadRequested = false;
            }
        }

        private void RefreshServerEntries()
        {
            if (networkManager == null || networkManager.ConnectedClientsIds == null)
            {
                return;
            }

            List<ulong> connectedIds = new List<ulong>(networkManager.ConnectedClientsIds);
            connectedIds.Sort();
            RaceReadyEntry[] previousEntries = new RaceReadyEntry[entries.Length];
            Array.Copy(entries, previousEntries, entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = default;
            }

            for (int i = 0; i < connectedIds.Count && i < entries.Length; i++)
            {
                ulong clientId = connectedIds[i];
                RaceReadyEntry previous = FindEntry(previousEntries, clientId, out RaceReadyEntry found)
                    ? found
                    : new RaceReadyEntry
                    {
                        Occupied = true,
                        ClientId = clientId,
                        Archetype = clientId == networkManager.LocalClientId ? localArchetype : CompetitorArchetype.AllRounder,
                        Ready = clientId == networkManager.LocalClientId && localReady,
                    };

                previous.Occupied = true;
                previous.ClientId = clientId;
                entries[i] = previous;
                RaceLaunchRequest.SetLanSelection(clientId, previous.Archetype);
            }
        }

        private bool AllConnectedPlayersReady()
        {
            if (networkManager == null || networkManager.ConnectedClientsIds == null || networkManager.ConnectedClientsIds.Count < MaxPlayers)
            {
                return false;
            }

            int readyCount = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Occupied && entries[i].Ready)
                {
                    readyCount++;
                }
            }

            return readyCount >= MaxPlayers;
        }

        private void UpdateServerCountdown()
        {
            if (!countdownActive)
            {
                countdownActive = true;
                countdownRemaining = PreSceneCountdownSeconds;
                sceneLoadRequested = false;
                BroadcastState(true);
                return;
            }

            countdownRemaining = Mathf.Max(0f, countdownRemaining - Time.unscaledDeltaTime);
            broadcastTimer -= Time.unscaledDeltaTime;
            if (broadcastTimer <= 0f)
            {
                BroadcastState(false);
            }

            if (countdownRemaining > 0f || sceneLoadRequested)
            {
                return;
            }

            sceneLoadRequested = true;
            BroadcastState(true);
            CountdownCompleted?.Invoke();
        }

        private void BroadcastState(bool force)
        {
            if (networkManager == null || !networkManager.IsServer || networkManager.CustomMessagingManager == null)
            {
                return;
            }

            if (!force && broadcastTimer > 0f)
            {
                return;
            }

            broadcastTimer = BroadcastInterval;
            using (FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteValueSafe(countdownActive);
                writer.WriteValueSafe(countdownRemaining);
                for (int i = 0; i < entries.Length; i++)
                {
                    writer.WriteValueSafe(entries[i].Occupied);
                    writer.WriteValueSafe(entries[i].ClientId);
                    writer.WriteValueSafe((int)entries[i].Archetype);
                    writer.WriteValueSafe(entries[i].Ready);
                }

                networkManager.CustomMessagingManager.SendNamedMessageToAll(
                    ReadyStateMessage,
                    writer,
                    NetworkDelivery.ReliableSequenced);
            }
        }

        private int FindOrCreateSlot(ulong clientId)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Occupied && entries[i].ClientId == clientId)
                {
                    return i;
                }
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].Occupied)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool FindEntry(ulong clientId, out RaceReadyEntry entry)
        {
            return FindEntry(entries, clientId, out entry);
        }

        private static bool FindEntry(RaceReadyEntry[] source, ulong clientId, out RaceReadyEntry entry)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].Occupied && source[i].ClientId == clientId)
                {
                    entry = source[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

        private static CompetitorArchetype ClampArchetype(int value)
        {
            return Enum.IsDefined(typeof(CompetitorArchetype), value)
                ? (CompetitorArchetype)value
                : CompetitorArchetype.AllRounder;
        }

        private struct RaceReadyEntry
        {
            public bool Occupied;
            public ulong ClientId;
            public CompetitorArchetype Archetype;
            public bool Ready;
        }
    }
}
