using SkyCircuit.Flight;
using SkyCircuit.CameraRigging;
using SkyCircuit.Presentation;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SkyCircuit.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SkyCircuitFlightController))]
    public sealed class NetworkFlightInputBridge : NetworkBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Rigidbody body;
        [SerializeField] private bool lockCursorForOwner = true;
        [SerializeField] private float gamepadLookScale = 16f;

        [Header("Owner Camera")]
        [SerializeField] private bool bindOwnerCameraTargetRig = true;
        [SerializeField] private FlightCameraTargetRig ownerCameraTargetRig;

        [Header("Contrails")]
        [SerializeField] private Color hostTrailColor = new Color(0.85f, 0.38f, 1f, 0.95f);
        [SerializeField] private Color clientTrailColor = new Color(0f, 0.98f, 0.6f, 1f);
        [SerializeField] private Color boostTrailColor = new Color(1f, 0.95f, 0.55f, 1f);

        [Header("Debug")]
        [SerializeField] private bool logContrailDebug = false;

        private readonly NetworkVariable<int> syncedVisualSlot = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> syncedNormalizedSpeed = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> syncedContrailEmitting = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> syncedContrailBoosting = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private FlightContrailFeedback[] contrailFeedbacks = System.Array.Empty<FlightContrailFeedback>();
        private int localVisualSlot = -1;
        private int lastBroadcastVisualSlot = -1;
        private bool ownerCameraTargetRigBound;
        private bool ownerInputActive = false;

        public bool HasControlAuthority => IsSpawned && IsOwner;
        public int DebugLocalVisualSlot => localVisualSlot;
        public int DebugSyncedVisualSlot => IsSpawned ? syncedVisualSlot.Value : -1;
        public int DebugResolvedVisualSlot => ResolveVisualSlot();
        public int DebugContrailFeedbackCount => contrailFeedbacks != null ? contrailFeedbacks.Length : 0;
        public float DebugSyncedNormalizedSpeed => IsSpawned ? syncedNormalizedSpeed.Value : 0f;
        public bool DebugSyncedContrailEmitting => IsSpawned && syncedContrailEmitting.Value;
        public bool DebugSyncedContrailBoosting => IsSpawned && syncedContrailBoosting.Value;
        public Color DebugResolvedCruiseColor => ResolveVisualSlot() == 1 ? clientTrailColor : hostTrailColor;
        public string DebugFirstTrailSummary => BuildFirstTrailSummary();

        private void Awake()
        {
            controller = controller != null ? controller : GetComponent<SkyCircuitFlightController>();
            body = body != null ? body : GetComponent<Rigidbody>();
            RefreshContrailFeedbacks();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            syncedVisualSlot.OnValueChanged += HandleSyncedVisualSlotChanged;
            ConfigureAuthority();
            controller?.SyncControlRotationFromTransform();
            localVisualSlot = syncedVisualSlot.Value >= 0
                ? Mathf.Clamp(syncedVisualSlot.Value, 0, 1)
                : InferVisualSlotFromOwner();
            if (IsServer)
            {
                syncedVisualSlot.Value = localVisualSlot;
            }

            ConfigureNetworkTransformAuthority();

            if (IsOwner && lockCursorForOwner)
            {
                LockCursor();
            }

            RefreshContrailFeedbacks();
            LogContrailDebug("spawn");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            syncedVisualSlot.OnValueChanged -= HandleSyncedVisualSlotChanged;
            ownerCameraTargetRigBound = false;
            lastBroadcastVisualSlot = -1;
        }

        private void Update()
        {
            if (IsOwner)
            {
                HandleCursorLock();
                if (ownerInputActive)
                {
                    FlightInputState input = ReadInput();
                    controller?.SetInput(input);
                    PublishOwnerContrailState();
                }
                else
                {
                    controller?.SyncControlRotationFromTransform();
                }
            }

            ApplyContrailVisuals();
        }

        public void ConfigureContrailPalette(Color hostCruise, Color clientCruise, Color boost)
        {
            hostTrailColor = hostCruise;
            clientTrailColor = clientCruise;
            boostTrailColor = boost;
            ApplyContrailVisuals();
        }

        public void SetInputActive(bool active)
        {
            if (ownerInputActive == active)
            {
                return;
            }

            ownerInputActive = active;
            if (!ownerInputActive)
            {
                controller?.SetInput(FlightInputState.Neutral);
            }

            controller?.SyncControlRotationFromTransform();
            ConfigureAuthority();
        }

        public void SetVisualSlot(int slot)
        {
            int clampedSlot = Mathf.Clamp(slot, 0, 1);
            localVisualSlot = clampedSlot;
            if (IsServer && IsSpawned)
            {
                if (syncedVisualSlot.Value != clampedSlot)
                {
                    syncedVisualSlot.Value = clampedSlot;
                }

                if (lastBroadcastVisualSlot != clampedSlot)
                {
                    lastBroadcastVisualSlot = clampedSlot;
                    ApplyVisualSlotClientRpc(clampedSlot);
                    LogContrailDebug($"broadcast slot {clampedSlot}");
                }
            }

            ApplyContrailVisuals();
        }

        public void ResetFlightForRace(Vector3 position, Quaternion rotation)
        {
            ApplyRaceReset(position, rotation);
            if (IsServer && IsSpawned)
            {
                ResetFlightForRaceClientRpc(position, rotation);
            }
        }

        public void ApplyRaceImpulse(Vector3 velocityChange)
        {
            if (IsServer && IsSpawned)
            {
                ApplyRaceImpulseClientRpc(velocityChange);
                return;
            }

            if (IsOwner)
            {
                ApplyRaceImpulseLocally(velocityChange);
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner || !bindOwnerCameraTargetRig || ownerCameraTargetRigBound)
            {
                return;
            }

            ownerCameraTargetRig = ownerCameraTargetRig != null
                ? ownerCameraTargetRig
                : FindAnyObjectByType<FlightCameraTargetRig>(FindObjectsInactive.Include);
            if (ownerCameraTargetRig == null)
            {
                return;
            }

            ownerCameraTargetRig.Configure(transform, controller, body, ownerCameraTargetRig.AimTarget);
            ownerCameraTargetRig.gameObject.SetActive(true);
            ownerCameraTargetRigBound = true;
        }

        [ClientRpc]
        private void ApplyVisualSlotClientRpc(int slot)
        {
            localVisualSlot = Mathf.Clamp(slot, 0, 1);
            ApplyContrailVisuals();
            LogContrailDebug($"client rpc slot {localVisualSlot}");
        }

        [ClientRpc]
        private void ResetFlightForRaceClientRpc(Vector3 position, Quaternion rotation)
        {
            ApplyRaceReset(position, rotation);
        }

        [ClientRpc]
        private void ApplyRaceImpulseClientRpc(Vector3 velocityChange)
        {
            if (IsOwner)
            {
                ApplyRaceImpulseLocally(velocityChange);
            }
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SubmitContrailStateServerRpc(float normalizedSpeed, bool emitting, bool boosting)
        {
            SetSyncedContrailState(normalizedSpeed, emitting, boosting);
        }

        private void ConfigureAuthority()
        {
            if (controller != null)
            {
                controller.enabled = ownerInputActive && HasControlAuthority;
                controller.SetInput(FlightInputState.Neutral);
            }

            if (body == null)
            {
                return;
            }

            body.isKinematic = !HasControlAuthority;
            body.useGravity = false;
            if (!HasControlAuthority)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void ConfigureNetworkTransformAuthority()
        {
            NetworkTransform networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            }
        }

        private void PublishOwnerContrailState()
        {
            if (controller == null)
            {
                return;
            }

            float normalizedSpeed = controller.NormalizedSpeed;
            bool emitting = controller.CurrentSpeed > 1f;
            bool boosting = controller.IsBoosting || controller.IsDashing;

            if (IsServer)
            {
                SetSyncedContrailState(normalizedSpeed, emitting, boosting);
                return;
            }

            SubmitContrailStateServerRpc(normalizedSpeed, emitting, boosting);
        }

        private void SetSyncedContrailState(float normalizedSpeed, bool emitting, bool boosting)
        {
            if (!IsServer)
            {
                return;
            }

            syncedNormalizedSpeed.Value = Mathf.Clamp01(normalizedSpeed);
            syncedContrailEmitting.Value = emitting;
            syncedContrailBoosting.Value = boosting;
        }

        private void HandleSyncedVisualSlotChanged(int previousValue, int currentValue)
        {
            if (currentValue < 0)
            {
                return;
            }

            localVisualSlot = Mathf.Clamp(currentValue, 0, 1);
            ApplyContrailVisuals();
            LogContrailDebug($"network slot {previousValue}->{currentValue}");
        }

        private void ApplyRaceImpulseLocally(Vector3 velocityChange)
        {
            controller?.ApplyExternalImpulse(velocityChange);
        }

        private void ApplyRaceReset(Vector3 position, Quaternion rotation)
        {
            if (controller != null)
            {
                controller.ResetFlight(position, rotation);
            }
            else
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyContrailVisuals()
        {
            RefreshContrailFeedbacks();

            int slot = ResolveVisualSlot();
            Color cruise = slot == 1 ? clientTrailColor : hostTrailColor;
            bool useLocalControllerState = IsOwner && controller != null;
            float normalizedSpeed = useLocalControllerState ? controller.NormalizedSpeed : syncedNormalizedSpeed.Value;
            bool emitting = useLocalControllerState ? controller.CurrentSpeed > 1f : syncedContrailEmitting.Value;
            bool boosting = useLocalControllerState
                ? controller.IsBoosting || controller.IsDashing
                : syncedContrailBoosting.Value;

            for (int i = 0; i < contrailFeedbacks.Length; i++)
            {
                FlightContrailFeedback feedback = contrailFeedbacks[i];
                if (feedback == null)
                {
                    continue;
                }

                feedback.ConfigureColors(cruise, boostTrailColor);
                feedback.SetExternalVisualState(normalizedSpeed, emitting, boosting);
            }
        }

        private int ResolveVisualSlot()
        {
            if (IsSpawned && syncedVisualSlot.Value >= 0)
            {
                return Mathf.Clamp(syncedVisualSlot.Value, 0, 1);
            }

            if (localVisualSlot >= 0)
            {
                return Mathf.Clamp(localVisualSlot, 0, 1);
            }

            if (IsSpawned)
            {
                return InferVisualSlotFromOwner();
            }

            return 0;
        }

        private int InferVisualSlotFromOwner()
        {
            return OwnerClientId == 0UL ? 0 : 1;
        }

        private void RefreshContrailFeedbacks()
        {
            if (contrailFeedbacks != null && contrailFeedbacks.Length > 0)
            {
                return;
            }

            contrailFeedbacks = GetComponentsInChildren<FlightContrailFeedback>(true);
        }

        private string BuildFirstTrailSummary()
        {
            RefreshContrailFeedbacks();
            if (contrailFeedbacks == null || contrailFeedbacks.Length == 0)
            {
                return "feedback=0";
            }

            FlightContrailFeedback feedback = contrailFeedbacks[0];
            if (feedback == null)
            {
                return $"feedback={contrailFeedbacks.Length} first=null";
            }

            return $"feedback={contrailFeedbacks.Length} trails={feedback.DebugTrailCount} material={feedback.DebugFirstTrailMaterial} external={feedback.DebugUseExternalVisualState} emit={feedback.DebugFirstTrailEmitting} stateEmit={feedback.DebugExternalEmitting} stateBoost={feedback.DebugExternalBoosting} stateSpeed={feedback.DebugExternalNormalizedSpeed:0.00} trailColor=#{ColorUtility.ToHtmlStringRGBA(feedback.DebugFirstTrailStartColor)} cruise=#{ColorUtility.ToHtmlStringRGBA(feedback.DebugCruiseColor)}";
        }

        private void LogContrailDebug(string reason)
        {
            if (!logContrailDebug)
            {
                return;
            }

            ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue;
            Debug.Log(
                $"[LAN Contrail] {reason} object={name} netId={NetworkObjectId} owner={OwnerClientId} localClient={localClientId} " +
                $"isServer={IsServer} isOwner={IsOwner} localSlot={localVisualSlot} syncedSlot={DebugSyncedVisualSlot} resolvedSlot={ResolveVisualSlot()} " +
                $"syncedSpeed={DebugSyncedNormalizedSpeed:0.00} syncedEmit={DebugSyncedContrailEmitting} syncedBoost={DebugSyncedContrailBoosting} " +
                $"resolvedColor=#{ColorUtility.ToHtmlStringRGBA(DebugResolvedCruiseColor)} {BuildFirstTrailSummary()}");
        }

        private FlightInputState ReadInput()
        {
            float throttle = 0f;
            float turn = 0f;
            float vertical = 0f;
            bool boost = false;
            Vector2 lookDelta = Vector2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                throttle += ReadKey(keyboard.wKey) - ReadKey(keyboard.sKey);
                turn += ReadKey(keyboard.dKey) - ReadKey(keyboard.aKey);
                vertical += ReadKey(keyboard.spaceKey) - Mathf.Max(ReadKey(keyboard.leftCtrlKey), ReadKey(keyboard.rightCtrlKey), ReadKey(keyboard.cKey));
                boost |= keyboard.qKey.isPressed;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                lookDelta += mouse.delta.ReadValue();
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 leftStick = gamepad.leftStick.ReadValue();
                Vector2 rightStick = gamepad.rightStick.ReadValue();

                throttle += leftStick.y;
                turn += leftStick.x;
                vertical += ReadButton(gamepad.rightShoulder) - ReadButton(gamepad.leftShoulder);
                boost |= gamepad.rightTrigger.ReadValue() > 0.5f;
                lookDelta += rightStick * (gamepadLookScale * Time.deltaTime);
            }

            return new FlightInputState(throttle, turn, vertical, lookDelta, boost);
        }

        private static float ReadKey(KeyControl key)
        {
            return key != null && key.isPressed ? 1f : 0f;
        }

        private static float ReadButton(ButtonControl button)
        {
            return button != null && button.isPressed ? 1f : 0f;
        }

        private static void HandleCursorLock()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
