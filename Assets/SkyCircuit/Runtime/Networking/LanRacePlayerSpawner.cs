using System.Collections.Generic;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using Unity.Netcode;
using UnityEngine;

namespace SkyCircuit.Networking
{
    internal static class LanRacePlayerSpawner
    {
        public static void SpawnMissingPlayerObjects(
            NetworkManager networkManager,
            GameObject playerPrefab,
            Transform fallbackSpawn,
            Transform[] spawnPoints,
            int expectedPlayers)
        {
            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer
                || playerPrefab == null
                || networkManager.ConnectedClientsIds == null)
            {
                return;
            }

            List<ulong> clientIds = new List<ulong>(networkManager.ConnectedClientsIds);
            clientIds.Sort();

            int playerCount = Mathf.Max(0, expectedPlayers);
            for (int slot = 0; slot < clientIds.Count && slot < playerCount; slot++)
            {
                ulong clientId = clientIds[slot];
                if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
                {
                    continue;
                }

                if (client.PlayerObject != null)
                {
                    if (IsRacePlayerObject(client.PlayerObject))
                    {
                        continue;
                    }

                    ReplaceNonRacePlayerObject(client.PlayerObject);
                    continue;
                }

                SpawnRacePlayerObject(clientId, slot, playerPrefab, fallbackSpawn, spawnPoints);
            }
        }

        private static void SpawnRacePlayerObject(
            ulong clientId,
            int slot,
            GameObject playerPrefab,
            Transform fallbackSpawn,
            Transform[] spawnPoints)
        {
            Transform spawnPoint = ResolveSpawnPoint(spawnPoints, slot);
            Vector3 spawnPosition = spawnPoint != null
                ? spawnPoint.position
                : fallbackSpawn != null ? fallbackSpawn.position : Vector3.zero;
            Quaternion spawnRotation = spawnPoint != null
                ? spawnPoint.rotation
                : fallbackSpawn != null ? fallbackSpawn.rotation : Quaternion.identity;
            GameObject playerObject = Object.Instantiate(playerPrefab, spawnPosition, spawnRotation);
            if (!playerObject.TryGetComponent(out NetworkObject networkObject))
            {
                Object.Destroy(playerObject);
                Debug.LogError("LAN race player prefab is missing a NetworkObject.");
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId, true);
        }

        private static Transform ResolveSpawnPoint(Transform[] spawnPoints, int slot)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            return spawnPoints[Mathf.Clamp(slot, 0, spawnPoints.Length - 1)];
        }

        private static bool IsRacePlayerObject(NetworkObject playerObject)
        {
            return playerObject != null
                && playerObject.TryGetComponent(out NetworkFlightInputBridge _)
                && playerObject.TryGetComponent(out SkyCircuitFlightController _)
                && playerObject.TryGetComponent(out Competitor _);
        }

        private static void ReplaceNonRacePlayerObject(NetworkObject playerObject)
        {
            if (playerObject == null)
            {
                return;
            }

            Debug.LogWarning($"Replacing non-race player object '{playerObject.name}' with LAN race player prefab.");
            if (playerObject.IsSpawned)
            {
                playerObject.Despawn(true);
                return;
            }

            Object.Destroy(playerObject.gameObject);
        }
    }
}
