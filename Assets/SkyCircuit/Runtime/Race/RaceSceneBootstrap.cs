using SkyCircuit.AI;
using SkyCircuit.CameraRigging;
using SkyCircuit.Combat;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Networking;
using SkyCircuit.Presentation;
using SkyCircuit.Profiles;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.Race
{
    [DisallowMultipleComponent]
    public sealed class RaceSceneBootstrap : MonoBehaviour
    {
        private static bool registered;

        [SerializeField] private LanRaceSessionController lanSession;

        private bool initialized;

        public static bool TryStartOfflineRace(
            LanRaceSessionController session,
            out Competitor localPlayer,
            out Competitor aiOpponent)
        {
            localPlayer = null;
            aiOpponent = null;
            if (session == null || session.IsOfflineTutorialActive)
            {
                return false;
            }

            GameObject prefab = session.RacePlayerPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("Offline race could not find the LAN race player prefab.");
                return false;
            }

            GameObject hostPilot = InstantiatePilot(prefab, session.GetSpawnPoint(0), "AI Training Host Pilot");
            GameObject clientPilot = InstantiatePilot(prefab, session.GetSpawnPoint(1), "AI Training Client Pilot");
            if (hostPilot == null || clientPilot == null)
            {
                return false;
            }

            localPlayer = PreparePilot(session, hostPilot, 0, true);
            aiOpponent = PreparePilot(session, clientPilot, 1, false);
            ApplyOfflineProfiles(localPlayer, aiOpponent);
            ConfigureCamera(hostPilot);
            session.StartOfflineRace(localPlayer, aiOpponent);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            if (!registered)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                registered = true;
            }

            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            LanRaceSessionController session = Object.FindAnyObjectByType<LanRaceSessionController>(FindObjectsInactive.Include);
            if (session == null || session.GetComponent<RaceSceneBootstrap>() != null)
            {
                return;
            }

            session.gameObject.AddComponent<RaceSceneBootstrap>();
        }

        private void Awake()
        {
            lanSession = lanSession != null
                ? lanSession
                : GetComponent<LanRaceSessionController>();
        }

        private void Start()
        {
            TryInitialize();
        }

        private void Update()
        {
            if (!initialized)
            {
                TryInitialize();
            }
        }

        private void TryInitialize()
        {
            lanSession = lanSession != null
                ? lanSession
                : Object.FindAnyObjectByType<LanRaceSessionController>(FindObjectsInactive.Include);
            if (lanSession == null)
            {
                return;
            }

            RaceMode mode = RaceLaunchRequest.Resolve();
            if (HasListeningNetwork())
            {
                initialized = true;
                RaceLaunchRequest.Clear();
                return;
            }

            if (mode == RaceMode.LanMultiplayer)
            {
                initialized = true;
                RaceLaunchRequest.Clear();
                return;
            }

            if (lanSession.IsOfflineTutorialActive)
            {
                initialized = true;
                RaceLaunchRequest.Clear();
                return;
            }

            if (mode == RaceMode.AiTraining || mode == RaceMode.Tutorial || mode == RaceMode.None)
            {
                TryStartOfflineRace(lanSession, out _, out _);
                initialized = true;
                RaceLaunchRequest.Clear();
            }
        }

        private static bool HasListeningNetwork()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }

        private static GameObject InstantiatePilot(GameObject prefab, Transform spawn, string objectName)
        {
            Vector3 position = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rotation = spawn != null ? spawn.rotation : Quaternion.identity;
            GameObject pilot = Object.Instantiate(prefab, position, rotation);
            pilot.name = objectName;
            return pilot;
        }

        private static Competitor PreparePilot(
            LanRaceSessionController session,
            GameObject pilot,
            int slot,
            bool isLocalPlayer)
        {
            NetworkFlightInputBridge bridge = pilot.GetComponent<NetworkFlightInputBridge>();
            if (bridge != null)
            {
                bridge.enabled = false;
            }

            NetworkTransform networkTransform = pilot.GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.enabled = false;
            }

            NetworkObject networkObject = pilot.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.enabled = false;
            }

            SkyCircuitFlightController controller = pilot.GetComponent<SkyCircuitFlightController>();
            Rigidbody body = pilot.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = false;
            }

            if (controller != null)
            {
                controller.enabled = true;
                controller.SyncControlRotationFromTransform();
            }

            Competitor competitor = pilot.GetComponent<Competitor>();
            if (competitor == null)
            {
                competitor = pilot.AddComponent<Competitor>();
            }

            ConfigureContrails(session, pilot, slot);
            if (isLocalPlayer)
            {
                ConfigureLocalInput(pilot);
            }
            else
            {
                ConfigureAiInput(session, pilot, controller, competitor);
            }

            return competitor;
        }

        private static void ConfigureLocalInput(GameObject pilot)
        {
            PlayerFlightInput input = pilot.GetComponent<PlayerFlightInput>();
            if (input == null)
            {
                input = pilot.AddComponent<PlayerFlightInput>();
            }

            input.enabled = true;
        }

        private static void ConfigureAiInput(
            LanRaceSessionController session,
            GameObject pilot,
            SkyCircuitFlightController controller,
            Competitor competitor)
        {
            PlayerFlightInput input = pilot.GetComponent<PlayerFlightInput>();
            if (input != null)
            {
                input.enabled = false;
            }

            RouteAIPilotController aiPilot = pilot.GetComponent<RouteAIPilotController>();
            if (aiPilot == null)
            {
                aiPilot = pilot.AddComponent<RouteAIPilotController>();
            }

            aiPilot.enabled = true;
            aiPilot.Configure(controller, competitor, session.Route, session);
        }

        private static void ApplyOfflineProfiles(Competitor localPlayer, Competitor aiOpponent)
        {
            CompetitorProfile playerProfile = RaceProfileCatalog.ResolveDefault(RaceLaunchRequest.ResolvePlayerArchetype());
            CompetitorProfile aiProfile = RaceProfileCatalog.ResolveDefault(RaceLaunchRequest.ResolveAiArchetype());
            localPlayer?.SetProfile(playerProfile, true);
            aiOpponent?.SetProfile(aiProfile, true);
        }

        private static void ConfigureContrails(LanRaceSessionController session, GameObject pilot, int slot)
        {
            Color cruise = slot == 1 ? session.ClientTrailColor : session.HostTrailColor;
            foreach (FlightContrailFeedback feedback in pilot.GetComponentsInChildren<FlightContrailFeedback>(true))
            {
                feedback.ConfigureColors(cruise, session.BoostTrailColor);
                feedback.ClearExternalVisualState();
            }
        }

        private static void ConfigureCamera(GameObject hostPilot)
        {
            if (hostPilot == null)
            {
                return;
            }

            SkyCircuitFlightController controller = hostPilot.GetComponent<SkyCircuitFlightController>();
            Rigidbody body = hostPilot.GetComponent<Rigidbody>();
            FlightCameraTargetRig cameraTargetRig = Object.FindAnyObjectByType<FlightCameraTargetRig>(FindObjectsInactive.Include);
            if (cameraTargetRig == null)
            {
                return;
            }

            cameraTargetRig.Configure(hostPilot.transform, controller, body, cameraTargetRig.AimTarget);
            cameraTargetRig.gameObject.SetActive(true);
        }
    }
}
