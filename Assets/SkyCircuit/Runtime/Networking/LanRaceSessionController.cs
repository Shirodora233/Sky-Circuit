using System;
using System.Collections.Generic;
using SkyCircuit.Combat;
using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Profiles;
using SkyCircuit.Race;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkyCircuit.Networking
{
    public enum LanRacePhase
    {
        Offline,
        WaitingForPlayers,
        Countdown,
        Running,
        Finished
    }

    public enum LanRaceRoomCloseReason
    {
        None,
        LocalExit,
        PeerExit,
        PeerDisconnected,
        ServerDisconnected
    }

    [DisallowMultipleComponent]
    public sealed class LanRaceSessionController : NetworkBehaviour
    {
        private const int MaxSupportedPlayers = 2;
        private const ulong UnassignedClientId = ulong.MaxValue;

        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private GameObject playerPrefab = null;
        [SerializeField] private BuoyRoute route = null;
        [SerializeField] private Transform[] spawnPoints = null;
        [SerializeField] private int expectedPlayers = MaxSupportedPlayers;
        [SerializeField] private float countdownDuration = 3f;
        [SerializeField] private float matchDuration = 180f;
        [SerializeField] private bool autoStartWhenReady = true;
        [SerializeField] private Color hostTrailColor = new Color(0.85f, 0.38f, 1f, 0.95f);
        [SerializeField] private Color clientTrailColor = new Color(0f, 0.98f, 0.6f, 1f);
        [SerializeField] private Color boostTrailColor = new Color(1f, 0.95f, 0.55f, 1f);

        [Header("Back Hit")]
        [SerializeField] private bool enableBackHitScoring = true;
        [SerializeField] private float hitDistance = 4.5f;
        [SerializeField] private float behindDotThreshold = -0.55f;
        [SerializeField] private float attackerFacingDotThreshold = 0.35f;
        [SerializeField] private int backHitScore = 2;
        [SerializeField] private float hitCooldown = 1.2f;
        [SerializeField] private float repulsionVelocityChange = 50f;
        [SerializeField] private float repulsionUpBias = 0.45f;

        [Header("HUD")]
        [SerializeField] private bool showHud = true;
        [SerializeField] private Rect hudArea = new Rect(18f, 18f, 520f, 245f);
        [SerializeField] private bool showContrailDebug = false;
        [SerializeField] private Rect contrailDebugArea = new Rect(18f, 272f, 850f, 190f);

        [Header("Room Control")]
        [SerializeField] private string mainMenuSceneName = "V0_10_MainMenu";
        [SerializeField] private float roomCloseDelay = 0.85f;

        private readonly NetworkVariable<int> syncedPhase = new NetworkVariable<int>(
            (int)LanRacePhase.Offline,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> syncedRemainingTime = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> syncedCountdownRemaining = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedConnectedPlayers = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedLeftScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedRightScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedLeftTarget = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedRightTarget = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedLeftBuoyScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedRightBuoyScore = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> syncedBackHitCooldown = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> syncedResult = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly Competitor[] competitors = new Competitor[MaxSupportedPlayers];
        private readonly ulong[] ownerClientIds = new ulong[MaxSupportedPlayers];
        private readonly List<NetworkFlightInputBridge> playerBuffer = new List<NetworkFlightInputBridge>(MaxSupportedPlayers);

        private LanRacePhase localPhase = LanRacePhase.Offline;
        private float localRemainingTime;
        private float localCountdownRemaining;
        private float localBackHitCooldown;
        private int localResult;
        private bool playerPrefabRegistered;
        private bool offlineTutorialActive;
        private bool roomLifecycleCallbacksRegistered;
        private bool roomWasReady;
        private bool roomClosing;
        private bool roomCloseCompleted;
        private float roomCloseTimer;
        private LanRaceRoomCloseReason roomCloseReason;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        public GameObject RacePlayerPrefab => playerPrefab;
        public BuoyRoute Route => route;
        public Color HostTrailColor => hostTrailColor;
        public Color ClientTrailColor => clientTrailColor;
        public Color BoostTrailColor => boostTrailColor;
        public LanRacePhase Phase => DisplayPhase;
        public float RemainingTime => DisplayRemainingTime;
        public float CountdownRemaining => DisplayCountdownRemaining;
        public Competitor Player => competitors[0];
        public Competitor Opponent => competitors[1];
        public Competitor LeftCompetitor => competitors[0];
        public Competitor RightCompetitor => competitors[1];
        public Competitor LocalPlayer => GetCompetitor(LocalSlot);
        public Competitor RemoteOpponent => GetCompetitor(LocalSlot == 0 ? 1 : 0);
        public int LeftScore => DisplayLeftScore;
        public int RightScore => DisplayRightScore;
        public int LeftTargetIndex => DisplayLeftTarget;
        public int RightTargetIndex => DisplayRightTarget;
        public int LocalTargetIndex => LocalSlot == 0 ? DisplayLeftTarget : DisplayRightTarget;
        public bool DogfightUnlocked => DisplayLeftBuoyScore > 0 || DisplayRightBuoyScore > 0;
        public string LeftDisplayName => DisplayNameOf(competitors[0], "Host Pilot");
        public string RightDisplayName => DisplayNameOf(competitors[1], "Client Pilot");
        public bool IsOfflineTutorialActive => offlineTutorialActive;
        public bool IsRoomControlAvailable => !roomCloseCompleted
            && (offlineTutorialActive || roomClosing || (networkManager != null && networkManager.IsListening));
        public bool IsRoomClosing => roomClosing;
        public string RoomCloseReasonText => RoomCloseReasonTextOf(roomCloseReason);
        public string RoomStatusText => roomClosing
            ? RoomCloseReasonText
            : $"{DescribeMode()}  {DisplayConnectedPlayers}/{ExpectedPlayerCount}  {DisplayPhaseText}";
        public bool IsOfflineRoomControl => offlineTutorialActive && (networkManager == null || !networkManager.IsListening);

        private void Awake()
        {
            ResetOwnerClientIds();
            ResolveNetworkManager();
            RegisterPlayerPrefab();
            RegisterRoomLifecycleCallbacks();
            localRemainingTime = matchDuration;
            localCountdownRemaining = countdownDuration;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResolveNetworkManager();
            RegisterPlayerPrefab();
            RegisterRoomLifecycleCallbacks();
        }

        public override void OnDestroy()
        {
            UnregisterRoomLifecycleCallbacks();
            base.OnDestroy();
        }

        private void Update()
        {
            ResolveNetworkManager();
            RegisterPlayerPrefab();
            RegisterRoomLifecycleCallbacks();

            if (UpdateRoomClosure())
            {
                return;
            }

            if (offlineTutorialActive && (networkManager == null || !networkManager.IsListening))
            {
                UpdateOfflineTutorial();
                return;
            }

            if (offlineTutorialActive)
            {
                offlineTutorialActive = false;
            }

            if (!IsServerActive)
            {
                BindNetworkPlayers();
                if (networkManager == null || !networkManager.IsListening)
                {
                    SetLocalPhase(LanRacePhase.Offline);
                }

                RefreshLocalPresentationState();
                UpdateRoomReadiness();
                return;
            }

            SpawnMissingPlayerObjects();
            BindNetworkPlayers();

            if (ConnectedCompetitorCount < ExpectedPlayerCount)
            {
                if (localPhase != LanRacePhase.WaitingForPlayers)
                {
                    EnterWaitingForPlayers(true);
                }

                PublishState();
                RefreshLocalPresentationState();
                UpdateRoomReadiness();
                return;
            }

            switch (localPhase)
            {
                case LanRacePhase.Offline:
                case LanRacePhase.WaitingForPlayers:
                    if (autoStartWhenReady)
                    {
                        BeginCountdown();
                    }
                    break;
                case LanRacePhase.Countdown:
                    UpdateCountdown();
                    break;
                case LanRacePhase.Running:
                    UpdateRunning();
                    break;
            }

            PublishState();
            RefreshLocalPresentationState();
            UpdateRoomReadiness();
        }

        public Transform GetSpawnPoint(int slot)
        {
            return ResolveSpawnPoint(slot);
        }

        public void StartOfflineTutorial(Competitor localPlayer, Competitor remoteOpponent)
        {
            offlineTutorialActive = true;
            ResetOwnerClientIds();

            ConfigureOfflineTutorialSlot(0, localPlayer, true, "Host Pilot");
            ConfigureOfflineTutorialSlot(1, remoteOpponent, false, "Client Pilot");

            localRemainingTime = matchDuration;
            localCountdownRemaining = 0f;
            localBackHitCooldown = 0f;
            localResult = 0;
            SetLocalPhase(LanRacePhase.Running);

            for (int i = 0; i < MaxSupportedPlayers; i++)
            {
                ResetCompetitorForMatch(competitors[i]);
            }

            route?.RefreshPlayerTargetVisual(competitors[0]);
            RefreshOfflineTutorialPresentationState();
        }

        public void StartOfflineRace(Competitor localPlayer, Competitor remoteOpponent)
        {
            StartOfflineTutorial(localPlayer, remoteOpponent);
        }

        public void StopOfflineTutorial()
        {
            offlineTutorialActive = false;
            SetLocalPhase(LanRacePhase.Offline);
        }

        public void RequestLeaveRoom()
        {
            if (roomClosing || roomCloseCompleted)
            {
                return;
            }

            if (offlineTutorialActive)
            {
                StopOfflineTutorial();
                BeginLocalRoomClosure(LanRaceRoomCloseReason.LocalExit, 0.05f);
                return;
            }

            ResolveNetworkManager();
            if (networkManager == null || !networkManager.IsListening)
            {
                BeginLocalRoomClosure(LanRaceRoomCloseReason.LocalExit, 0.05f);
                return;
            }

            if (IsServerActive)
            {
                BeginNetworkRoomClosure(
                    LanRaceRoomCloseReason.LocalExit,
                    LanRaceRoomCloseReason.PeerExit);
                return;
            }

            if (IsSpawned && IsClient)
            {
                RequestLeaveRoomServerRpc();
            }

            BeginLocalRoomClosure(LanRaceRoomCloseReason.LocalExit, roomCloseDelay);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestLeaveRoomServerRpc(ServerRpcParams rpcParams = default)
        {
            BeginNetworkRoomClosure(
                LanRaceRoomCloseReason.PeerExit,
                LanRaceRoomCloseReason.PeerExit);
        }

        [ClientRpc]
        private void NotifyRoomClosingClientRpc(int reasonCode)
        {
            BeginLocalRoomClosure(ToRoomCloseReason(reasonCode), roomCloseDelay);
        }

        private void BeginNetworkRoomClosure(
            LanRaceRoomCloseReason localReason,
            LanRaceRoomCloseReason remoteReason)
        {
            BeginLocalRoomClosure(localReason, roomCloseDelay);
            if (IsSpawned && IsServer)
            {
                NotifyRoomClosingClientRpc((int)remoteReason);
            }
        }

        private void BeginLocalRoomClosure(LanRaceRoomCloseReason reason, float delay)
        {
            if (roomCloseCompleted)
            {
                return;
            }

            roomClosing = true;
            if (roomCloseReason == LanRaceRoomCloseReason.None || reason == LanRaceRoomCloseReason.LocalExit)
            {
                roomCloseReason = reason;
            }

            roomCloseTimer = Mathf.Max(roomCloseTimer, Mathf.Max(0.01f, delay));
        }

        private bool UpdateRoomClosure()
        {
            if (!roomClosing || roomCloseCompleted)
            {
                return false;
            }

            roomCloseTimer -= Time.unscaledDeltaTime;
            if (roomCloseTimer <= 0f)
            {
                CompleteRoomClosure();
            }

            return true;
        }

        private void CompleteRoomClosure()
        {
            if (roomCloseCompleted)
            {
                return;
            }

            roomCloseCompleted = true;
            roomClosing = false;
            NetworkManager closingNetworkManager = networkManager != null
                ? networkManager
                : NetworkManager.Singleton;
            if (closingNetworkManager != null && closingNetworkManager.IsListening)
            {
                closingNetworkManager.Shutdown();
            }

            if (closingNetworkManager != null)
            {
                Destroy(closingNetworkManager.gameObject);
                networkManager = null;
            }

            if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }

        private void ConfigureOfflineTutorialSlot(int slot, Competitor competitor, bool playerControlled, string displayName)
        {
            if (slot < 0 || slot >= MaxSupportedPlayers)
            {
                return;
            }

            competitors[slot] = competitor;
            ownerClientIds[slot] = (ulong)slot;
            if (competitor == null)
            {
                return;
            }

            SkyCircuitFlightController controller = competitor.GetComponent<SkyCircuitFlightController>();
            competitor.Configure(
                displayName,
                playerControlled,
                controller,
                ResolveSpawnPoint(slot),
                controller != null ? controller.Profile : null);
        }

        private void UpdateOfflineTutorial()
        {
            if (competitors[0] == null)
            {
                StopOfflineTutorial();
                return;
            }

            if (localPhase != LanRacePhase.Running)
            {
                SetLocalPhase(LanRacePhase.Running);
            }

            localRemainingTime = matchDuration;
            localCountdownRemaining = 0f;
            localResult = 0;

            route?.TryScore(competitors[0]);
            route?.TryScore(competitors[1]);
            UpdateBackHitScoring();
            RefreshOfflineTutorialPresentationState();
        }

        private void RefreshOfflineTutorialPresentationState()
        {
            RefreshLocalRouteVisual();
            RefreshBackHitFeedbacks();
        }

        private void OnGUI()
        {
            if (!showHud && !showContrailDebug)
            {
                return;
            }

            EnsureHudStyles();

            if (showHud)
            {
                GUILayout.BeginArea(hudArea, GUI.skin.box);
                GUILayout.Label("LAN Cloud Sea Race", titleStyle);
                GUILayout.Label($"Mode: {DescribeMode()}    Connected Racers: {DisplayConnectedPlayers}/{ExpectedPlayerCount}", labelStyle);
                GUILayout.Label($"Phase: {DisplayPhaseText}", labelStyle);

                if (DisplayPhase == LanRacePhase.Countdown)
                {
                    GUILayout.Label($"Start In: {Mathf.CeilToInt(DisplayCountdownRemaining)}", labelStyle);
                }
                else
                {
                    GUILayout.Label($"Time: {FormatTime(DisplayRemainingTime)}", labelStyle);
                }

                GUILayout.Space(6f);
                GUILayout.Label($"Host Pilot: {DisplayLeftScore}    Client Pilot: {DisplayRightScore}", labelStyle);
                GUILayout.Label($"Target: {DisplayLeftTarget + 1} / {DisplayRightTarget + 1}", labelStyle);

                if (DisplayPhase == LanRacePhase.Finished)
                {
                    GUILayout.Space(6f);
                    GUILayout.Label(ResultText(DisplayResult), titleStyle);
                }

                GUILayout.EndArea();
            }

            if (showContrailDebug)
            {
                DrawContrailDebugHud();
            }
        }

        private void BeginCountdown()
        {
            SetLocalPhase(LanRacePhase.Countdown);
            localCountdownRemaining = countdownDuration;
            localRemainingTime = matchDuration;
            localBackHitCooldown = 0f;
            localResult = 0;

            for (int i = 0; i < MaxSupportedPlayers; i++)
            {
                Competitor competitor = competitors[i];
                if (competitor == null)
                {
                    continue;
                }

                ResetCompetitorForMatch(competitor);
                SetCompetitorFlightActive(competitor, false);
            }

            route?.RefreshPlayerTargetVisual(competitors[0]);
        }

        private void UpdateCountdown()
        {
            localCountdownRemaining -= Time.deltaTime;
            if (localCountdownRemaining > 0f)
            {
                return;
            }

            StartRace();
        }

        private void StartRace()
        {
            localCountdownRemaining = 0f;
            localRemainingTime = matchDuration;
            localBackHitCooldown = 0f;
            localResult = 0;
            SetLocalPhase(LanRacePhase.Running);

            for (int i = 0; i < MaxSupportedPlayers; i++)
            {
                Competitor competitor = competitors[i];
                if (competitor == null)
                {
                    continue;
                }

                ResetCompetitorForMatch(competitor);
                SetCompetitorFlightActive(competitor, true);
            }

            route?.RefreshPlayerTargetVisual(competitors[0]);
        }

        private void UpdateRunning()
        {
            route?.TryScore(competitors[0]);
            route?.TryScore(competitors[1]);
            UpdateBackHitScoring();

            localRemainingTime -= Time.deltaTime;
            if (localRemainingTime <= 0f)
            {
                FinishRace();
            }
        }

        private void FinishRace()
        {
            localRemainingTime = 0f;
            SetLocalPhase(LanRacePhase.Finished);

            for (int i = 0; i < MaxSupportedPlayers; i++)
            {
                SetCompetitorFlightActive(competitors[i], false);
            }

            int leftScore = ScoreOf(competitors[0]);
            int rightScore = ScoreOf(competitors[1]);
            if (leftScore > rightScore)
            {
                localResult = 1;
            }
            else if (rightScore > leftScore)
            {
                localResult = 2;
            }
            else
            {
                localResult = 3;
            }
        }

        private void EnterWaitingForPlayers(bool resetCompetitors)
        {
            SetLocalPhase(LanRacePhase.WaitingForPlayers);
            localCountdownRemaining = countdownDuration;
            localRemainingTime = matchDuration;
            localBackHitCooldown = 0f;
            localResult = 0;

            if (!resetCompetitors)
            {
                return;
            }

            for (int i = 0; i < MaxSupportedPlayers; i++)
            {
                Competitor competitor = competitors[i];
                if (competitor == null)
                {
                    continue;
                }

                ResetCompetitorForMatch(competitor);
                SetCompetitorFlightActive(competitor, false);
            }

            route?.RefreshPlayerTargetVisual(competitors[0]);
        }

        private void BindNetworkPlayers()
        {
            playerBuffer.Clear();
            foreach (NetworkFlightInputBridge bridge in FindObjectsByType<NetworkFlightInputBridge>(FindObjectsInactive.Include))
            {
                if (bridge == null || !bridge.IsSpawned || bridge.NetworkObject == null)
                {
                    continue;
                }

                playerBuffer.Add(bridge);
            }

            playerBuffer.Sort(CompareByOwnerClientId);

            for (int slot = 0; slot < MaxSupportedPlayers; slot++)
            {
                if (slot >= playerBuffer.Count || slot >= ExpectedPlayerCount)
                {
                    ClearSlot(slot);
                    continue;
                }

                ConfigureSlot(slot, playerBuffer[slot]);
            }
        }

        private void SpawnMissingPlayerObjects()
        {
            LanRacePlayerSpawner.SpawnMissingPlayerObjects(
                networkManager,
                playerPrefab,
                transform,
                spawnPoints,
                ExpectedPlayerCount);
        }

        private void ConfigureSlot(int slot, NetworkFlightInputBridge bridge)
        {
            ulong ownerClientId = bridge.OwnerClientId;
            SkyCircuitFlightController controller = bridge.GetComponent<SkyCircuitFlightController>();
            Competitor competitor = bridge.GetComponent<Competitor>();
            if (competitor == null)
            {
                competitor = bridge.gameObject.AddComponent<Competitor>();
            }

            bool changed = ownerClientIds[slot] != ownerClientId || competitors[slot] != competitor;
            competitors[slot] = competitor;
            ownerClientIds[slot] = ownerClientId;
            ApplySlotVisuals(bridge, slot);
            bridge.SetInputActive(IsRaceInputActive);

            if (!changed)
            {
                return;
            }

            Transform spawnPoint = ResolveSpawnPoint(slot);
            competitor.Configure(
                slot == 0 ? "Host Pilot" : "Client Pilot",
                slot == 0,
                controller,
                spawnPoint,
                ResolveProfileForOwner(ownerClientId, controller != null ? controller.Profile : null));
            ResetCompetitorForMatch(competitor);
            SetCompetitorFlightActive(competitor, IsRaceInputActive);
        }

        private static CompetitorProfile ResolveProfileForOwner(ulong ownerClientId, CompetitorProfile fallback)
        {
            if (RaceLaunchRequest.TryGetLanSelection(ownerClientId, out CompetitorArchetype archetype))
            {
                return RaceProfileCatalog.ResolveDefault(archetype);
            }

            return fallback != null ? fallback : RaceProfileCatalog.ResolveDefault(CompetitorArchetype.AllRounder);
        }

        private void ApplySlotVisuals(NetworkFlightInputBridge bridge, int slot)
        {
            if (bridge == null)
            {
                return;
            }

            bridge.ConfigureContrailPalette(hostTrailColor, clientTrailColor, boostTrailColor);
            bridge.SetVisualSlot(slot);
        }

        private void ClearSlot(int slot)
        {
            if (slot < 0 || slot >= MaxSupportedPlayers)
            {
                return;
            }

            if (competitors[slot] != null)
            {
                SetCompetitorFlightActive(competitors[slot], false);
            }

            competitors[slot] = null;
            ownerClientIds[slot] = UnassignedClientId;
        }

        private Transform ResolveSpawnPoint(int slot)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return null;
            }

            return spawnPoints[Mathf.Clamp(slot, 0, spawnPoints.Length - 1)];
        }

        private void RefreshLocalPresentationState()
        {
            RefreshLocalRouteVisual();
            RefreshBackHitFeedbacks();

            bool inputActive = IsRaceInputActive;
            for (int i = 0; i < competitors.Length; i++)
            {
                NetworkFlightInputBridge bridge = competitors[i] != null
                    ? competitors[i].GetComponent<NetworkFlightInputBridge>()
                    : null;
                bridge?.SetInputActive(inputActive);
            }
        }

        private void RefreshLocalRouteVisual()
        {
            if (route == null)
            {
                return;
            }

            bool localIsRight = LocalSlot == 1;
            int targetIndex = localIsRight ? DisplayRightTarget : DisplayLeftTarget;
            bool hasCompletedAny = localIsRight ? DisplayRightBuoyScore > 0 : DisplayLeftBuoyScore > 0;
            route.RefreshTargetVisual(targetIndex, hasCompletedAny);
        }

        private void RefreshBackHitFeedbacks()
        {
            bool available = DisplayPhase == LanRacePhase.Running
                && DogfightUnlocked
                && DisplayBackHitCooldown <= 0f;

            for (int i = 0; i < competitors.Length; i++)
            {
                BackHitFeedback feedback = competitors[i] != null
                    ? competitors[i].GetComponentInChildren<BackHitFeedback>(true)
                    : null;
                feedback?.SetAvailable(available);
            }
        }

        private void UpdateBackHitScoring()
        {
            if (!enableBackHitScoring || !DogfightUnlocked)
            {
                localBackHitCooldown = 0f;
                return;
            }

            localBackHitCooldown = Mathf.Max(0f, localBackHitCooldown - Time.deltaTime);
            if (localBackHitCooldown > 0f)
            {
                return;
            }

            HitCandidate leftHit = EvaluateHit(0, 1);
            HitCandidate rightHit = EvaluateHit(1, 0);
            if (!leftHit.IsValid && !rightHit.IsValid)
            {
                return;
            }

            if (leftHit.IsValid && (!rightHit.IsValid || leftHit.Distance <= rightHit.Distance))
            {
                ResolveBackHit(leftHit);
            }
            else
            {
                ResolveBackHit(rightHit);
            }
        }

        private HitCandidate EvaluateHit(int attackerSlot, int targetSlot)
        {
            Competitor attacker = GetCompetitor(attackerSlot);
            Competitor target = GetCompetitor(targetSlot);
            if (attacker == null || target == null || attacker.Body == null || target.Body == null)
            {
                return HitCandidate.Invalid;
            }

            Vector3 targetToAttacker = attacker.Body.position - target.Body.position;
            float distance = targetToAttacker.magnitude;
            if (distance <= Mathf.Epsilon || distance > hitDistance)
            {
                return HitCandidate.Invalid;
            }

            Vector3 targetToAttackerDirection = targetToAttacker / distance;
            Vector3 attackerToTargetDirection = -targetToAttackerDirection;
            float behindDot = Vector3.Dot(target.Body.forward, targetToAttackerDirection);
            if (behindDot > behindDotThreshold)
            {
                return HitCandidate.Invalid;
            }

            float attackerFacingDot = Vector3.Dot(attacker.Body.forward, attackerToTargetDirection);
            if (attackerFacingDot < attackerFacingDotThreshold)
            {
                return HitCandidate.Invalid;
            }

            return new HitCandidate(attackerSlot, targetSlot, attacker, target, distance, targetToAttackerDirection);
        }

        private void ResolveBackHit(HitCandidate hit)
        {
            hit.Attacker.AddBackHitScore(backHitScore);
            localBackHitCooldown = hitCooldown;
            ApplyBackHitRepulsion(hit);
            if (IsSpawned && IsServer)
            {
                TriggerBackHitFeedbackClientRpc(hit.TargetSlot);
                return;
            }

            TriggerBackHitFeedbackLocally(hit.TargetSlot);
        }

        private void ApplyBackHitRepulsion(HitCandidate hit)
        {
            Vector3 separation = hit.TargetToAttackerDirection;
            Vector3 attackerVelocity = (separation + Vector3.up * repulsionUpBias).normalized * repulsionVelocityChange;
            Vector3 targetVelocity = (-separation + Vector3.up * repulsionUpBias).normalized * repulsionVelocityChange;
            ApplyRaceImpulse(hit.Attacker, attackerVelocity);
            ApplyRaceImpulse(hit.Target, targetVelocity);
        }

        [ClientRpc]
        private void TriggerBackHitFeedbackClientRpc(int targetSlot)
        {
            TriggerBackHitFeedbackLocally(targetSlot);
        }

        private void TriggerBackHitFeedbackLocally(int targetSlot)
        {
            BackHitFeedback feedback = GetCompetitor(targetSlot) != null
                ? GetCompetitor(targetSlot).GetComponentInChildren<BackHitFeedback>(true)
                : null;
            feedback?.TriggerHit();
        }

        private void PublishState()
        {
            if (!CanWriteNetworkState)
            {
                return;
            }

            syncedPhase.Value = (int)localPhase;
            syncedRemainingTime.Value = Mathf.Max(0f, localRemainingTime);
            syncedCountdownRemaining.Value = Mathf.Max(0f, localCountdownRemaining);
            syncedConnectedPlayers.Value = ConnectedCompetitorCount;
            syncedLeftScore.Value = ScoreOf(competitors[0]);
            syncedRightScore.Value = ScoreOf(competitors[1]);
            syncedLeftTarget.Value = TargetIndexOf(competitors[0]);
            syncedRightTarget.Value = TargetIndexOf(competitors[1]);
            syncedLeftBuoyScore.Value = BuoyScoreOf(competitors[0]);
            syncedRightBuoyScore.Value = BuoyScoreOf(competitors[1]);
            syncedBackHitCooldown.Value = Mathf.Max(0f, localBackHitCooldown);
            syncedResult.Value = localResult;
        }

        private void SetLocalPhase(LanRacePhase phase)
        {
            localPhase = phase;
        }

        private void ResolveNetworkManager()
        {
            if (networkManager == null)
            {
                networkManager = NetworkManager.Singleton;
            }

            if (networkManager == null)
            {
                networkManager = FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            }
        }

        private void RegisterRoomLifecycleCallbacks()
        {
            if (roomLifecycleCallbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnClientDisconnectCallback += HandleRoomClientDisconnected;
            roomLifecycleCallbacksRegistered = true;
        }

        private void UnregisterRoomLifecycleCallbacks()
        {
            if (!roomLifecycleCallbacksRegistered || networkManager == null)
            {
                return;
            }

            networkManager.OnClientDisconnectCallback -= HandleRoomClientDisconnected;
            roomLifecycleCallbacksRegistered = false;
        }

        private void HandleRoomClientDisconnected(ulong clientId)
        {
            if (roomClosing || roomCloseCompleted || offlineTutorialActive || networkManager == null)
            {
                return;
            }

            if (networkManager.IsServer)
            {
                if (clientId == networkManager.LocalClientId || !roomWasReady)
                {
                    return;
                }

                BeginNetworkRoomClosure(
                    LanRaceRoomCloseReason.PeerDisconnected,
                    LanRaceRoomCloseReason.PeerDisconnected);
                return;
            }

            if (networkManager.IsClient && clientId == NetworkManager.ServerClientId)
            {
                BeginLocalRoomClosure(LanRaceRoomCloseReason.ServerDisconnected, roomCloseDelay);
            }
        }

        private void UpdateRoomReadiness()
        {
            if (offlineTutorialActive || roomWasReady || networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            if (ConnectedCompetitorCount >= ExpectedPlayerCount
                || ConnectedNetworkClientCount >= ExpectedPlayerCount
                || DisplayConnectedPlayers >= ExpectedPlayerCount
                || DisplayPhase == LanRacePhase.Countdown
                || DisplayPhase == LanRacePhase.Running
                || DisplayPhase == LanRacePhase.Finished)
            {
                roomWasReady = true;
            }
        }

        private void RegisterPlayerPrefab()
        {
            if (playerPrefabRegistered || networkManager == null || playerPrefab == null)
            {
                return;
            }

            try
            {
                if (networkManager.NetworkConfig != null)
                {
                    networkManager.NetworkConfig.PlayerPrefab = null;
                    networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
                }

                networkManager.AddNetworkPrefab(playerPrefab);
                playerPrefabRegistered = true;
            }
            catch (Exception exception)
            {
                if (exception.Message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                    || exception.Message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    playerPrefabRegistered = true;
                    return;
                }

                Debug.LogWarning($"Could not register LAN race player prefab: {exception.Message}");
            }
        }

        private void ResetOwnerClientIds()
        {
            for (int i = 0; i < ownerClientIds.Length; i++)
            {
                ownerClientIds[i] = UnassignedClientId;
            }
        }

        private void EnsureHudStyles()
        {
            if (labelStyle != null && titleStyle != null)
            {
                return;
            }

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

        private void DrawContrailDebugHud()
        {
            GUILayout.BeginArea(contrailDebugArea, GUI.skin.box);
            GUILayout.Label("LAN Contrail Debug", titleStyle);

            ulong localClientId = networkManager != null ? networkManager.LocalClientId : ulong.MaxValue;
            GUILayout.Label(
                $"Mode: {DescribeMode()}    LocalClient: {localClientId}    IsServer: {IsServer}    IsClient: {IsClient}    Phase: {DisplayPhaseText}",
                labelStyle);

            for (int slot = 0; slot < MaxSupportedPlayers; slot++)
            {
                Competitor competitor = GetCompetitor(slot);
                NetworkFlightInputBridge bridge = competitor != null
                    ? competitor.GetComponent<NetworkFlightInputBridge>()
                    : null;

                if (bridge == null)
                {
                    GUILayout.Label($"Slot {slot}: empty    owner={OwnerText(slot)}", labelStyle);
                    continue;
                }

                GUILayout.Label(BuildContrailDebugLine(slot, bridge), labelStyle);
                GUILayout.Label($"    {bridge.DebugFirstTrailSummary}", labelStyle);
            }

            GUILayout.EndArea();
        }

        private string BuildContrailDebugLine(int slot, NetworkFlightInputBridge bridge)
        {
            string role = slot == 0 ? "Host" : "Client";
            return
                $"Slot {slot} {role}: owner={bridge.OwnerClientId} isOwner={bridge.IsOwner} authority={bridge.HasControlAuthority} " +
                $"localSlot={bridge.DebugLocalVisualSlot} syncedSlot={bridge.DebugSyncedVisualSlot} resolved={bridge.DebugResolvedVisualSlot} " +
                $"syncedSpeed={bridge.DebugSyncedNormalizedSpeed:0.00} emit={bridge.DebugSyncedContrailEmitting} boost={bridge.DebugSyncedContrailBoosting} " +
                $"color=#{ColorUtility.ToHtmlStringRGBA(bridge.DebugResolvedCruiseColor)}";
        }

        private string OwnerText(int slot)
        {
            return slot >= 0 && slot < ownerClientIds.Length && ownerClientIds[slot] != UnassignedClientId
                ? ownerClientIds[slot].ToString()
                : "none";
        }

        private bool IsServerActive => networkManager != null && networkManager.IsListening && networkManager.IsServer;
        private bool CanWriteNetworkState => IsSpawned && IsServer;
        private int ExpectedPlayerCount => Mathf.Clamp(expectedPlayers, 1, MaxSupportedPlayers);
        private int ConnectedCompetitorCount => CountCompetitors();
        private int ConnectedNetworkClientCount => networkManager != null && networkManager.ConnectedClientsIds != null
            ? networkManager.ConnectedClientsIds.Count
            : 0;

        private LanRacePhase DisplayPhase => IsSpawned
            ? (LanRacePhase)Mathf.Clamp(syncedPhase.Value, (int)LanRacePhase.Offline, (int)LanRacePhase.Finished)
            : localPhase;

        private float DisplayRemainingTime => IsSpawned ? syncedRemainingTime.Value : localRemainingTime;
        private float DisplayCountdownRemaining => IsSpawned ? syncedCountdownRemaining.Value : localCountdownRemaining;
        private int DisplayConnectedPlayers => IsSpawned ? syncedConnectedPlayers.Value : ConnectedCompetitorCount;
        private int DisplayLeftScore => IsSpawned ? syncedLeftScore.Value : ScoreOf(competitors[0]);
        private int DisplayRightScore => IsSpawned ? syncedRightScore.Value : ScoreOf(competitors[1]);
        private int DisplayLeftTarget => IsSpawned ? syncedLeftTarget.Value : TargetIndexOf(competitors[0]);
        private int DisplayRightTarget => IsSpawned ? syncedRightTarget.Value : TargetIndexOf(competitors[1]);
        private int DisplayLeftBuoyScore => IsSpawned ? syncedLeftBuoyScore.Value : BuoyScoreOf(competitors[0]);
        private int DisplayRightBuoyScore => IsSpawned ? syncedRightBuoyScore.Value : BuoyScoreOf(competitors[1]);
        private float DisplayBackHitCooldown => IsSpawned ? syncedBackHitCooldown.Value : localBackHitCooldown;
        private int DisplayResult => IsSpawned ? syncedResult.Value : localResult;
        private string DisplayPhaseText => DisplayPhase.ToString();
        private bool IsRaceInputActive => DisplayPhase == LanRacePhase.Running;

        private int CountCompetitors()
        {
            int count = 0;
            for (int i = 0; i < MaxSupportedPlayers; i++)
            {
                if (competitors[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private string DescribeMode()
        {
            if (IsOfflineRoomControl)
            {
                return "Training";
            }

            if (networkManager == null || !networkManager.IsListening)
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

            return networkManager.IsClient ? "Client" : "Offline";
        }

        private int LocalSlot
        {
            get
            {
                ulong localClientId = networkManager != null ? networkManager.LocalClientId : 0UL;
                for (int i = 0; i < ownerClientIds.Length; i++)
                {
                    if (ownerClientIds[i] == localClientId)
                    {
                        return i;
                    }
                }

                return 0;
            }
        }

        private Competitor GetCompetitor(int slot)
        {
            return slot >= 0 && slot < competitors.Length ? competitors[slot] : null;
        }

        private static void SetCompetitorFlightActive(Competitor competitor, bool active)
        {
            NetworkFlightInputBridge bridge = competitor != null
                ? competitor.GetComponent<NetworkFlightInputBridge>()
                : null;
            bridge?.SetInputActive(active);

            SkyCircuitFlightController controller = competitor != null ? competitor.Controller : null;
            if (controller == null)
            {
                return;
            }

            controller.SetInput(FlightInputState.Neutral);
            controller.SyncControlRotationFromTransform();
            controller.enabled = active && (bridge == null || bridge.HasControlAuthority);

            if (active)
            {
                return;
            }

            Rigidbody body = controller.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private static void ResetCompetitorForMatch(Competitor competitor)
        {
            if (competitor == null)
            {
                return;
            }

            competitor.ResetForMatch();
            NetworkFlightInputBridge bridge = competitor.GetComponent<NetworkFlightInputBridge>();
            SkyCircuitFlightController controller = competitor.Controller;
            if (bridge == null || controller == null)
            {
                return;
            }

            Transform controllerTransform = controller.transform;
            bridge.ResetFlightForRace(controllerTransform.position, controllerTransform.rotation);
        }

        private static void ApplyRaceImpulse(Competitor competitor, Vector3 velocityChange)
        {
            if (competitor == null)
            {
                return;
            }

            NetworkFlightInputBridge bridge = competitor.GetComponent<NetworkFlightInputBridge>();
            if (bridge != null && bridge.IsSpawned)
            {
                bridge.ApplyRaceImpulse(velocityChange);
                return;
            }

            competitor.Controller?.ApplyExternalImpulse(velocityChange);
        }

        private static int CompareByOwnerClientId(NetworkFlightInputBridge left, NetworkFlightInputBridge right)
        {
            return left.OwnerClientId.CompareTo(right.OwnerClientId);
        }

        private static int ScoreOf(Competitor competitor)
        {
            return competitor != null ? competitor.Score : 0;
        }

        private static int BuoyScoreOf(Competitor competitor)
        {
            return competitor != null ? competitor.BuoyScoreCount : 0;
        }

        private static int TargetIndexOf(Competitor competitor)
        {
            return competitor != null ? competitor.TargetIndex : 0;
        }

        private static string ResultText(int result)
        {
            return result switch
            {
                1 => "Host Pilot Wins",
                2 => "Client Pilot Wins",
                3 => "Draw",
                _ => "Race Finished",
            };
        }

        private static LanRaceRoomCloseReason ToRoomCloseReason(int reasonCode)
        {
            if (Enum.IsDefined(typeof(LanRaceRoomCloseReason), reasonCode))
            {
                return (LanRaceRoomCloseReason)reasonCode;
            }

            return LanRaceRoomCloseReason.PeerDisconnected;
        }

        private static string RoomCloseReasonTextOf(LanRaceRoomCloseReason reason)
        {
            return reason switch
            {
                LanRaceRoomCloseReason.LocalExit => "\u6b63\u5728\u9000\u51fa\u623f\u95f4",
                LanRaceRoomCloseReason.PeerExit => "\u5bf9\u65b9\u5df2\u9000\u51fa\u623f\u95f4",
                LanRaceRoomCloseReason.PeerDisconnected => "\u5bf9\u65b9\u8fde\u63a5\u5df2\u65ad\u5f00",
                LanRaceRoomCloseReason.ServerDisconnected => "\u4e3b\u673a\u8fde\u63a5\u5df2\u65ad\u5f00",
                _ => "\u623f\u95f4\u6b63\u5728\u5173\u95ed",
            };
        }

        private static string DisplayNameOf(Competitor competitor, string fallback)
        {
            return competitor != null && !string.IsNullOrWhiteSpace(competitor.DisplayName)
                ? competitor.DisplayName
                : fallback;
        }

        private static string FormatTime(float seconds)
        {
            int clamped = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{clamped / 60:0}:{clamped % 60:00}";
        }

        private readonly struct HitCandidate
        {
            public static readonly HitCandidate Invalid = new HitCandidate(-1, -1, null, null, 0f, Vector3.zero);

            public readonly int AttackerSlot;
            public readonly int TargetSlot;
            public readonly Competitor Attacker;
            public readonly Competitor Target;
            public readonly float Distance;
            public readonly Vector3 TargetToAttackerDirection;

            public bool IsValid => Attacker != null && Target != null;

            public HitCandidate(
                int attackerSlot,
                int targetSlot,
                Competitor attacker,
                Competitor target,
                float distance,
                Vector3 targetToAttackerDirection)
            {
                AttackerSlot = attackerSlot;
                TargetSlot = targetSlot;
                Attacker = attacker;
                Target = target;
                Distance = distance;
                TargetToAttackerDirection = targetToAttackerDirection;
            }
        }
    }
}
