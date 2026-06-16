using SkyCircuit.Flight;
using Unity.Netcode;
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
        [SerializeField] private bool followOwnerWithMainCamera = true;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 5.2f, -14f);
        [SerializeField] private Vector3 cameraLookOffset = new Vector3(0f, 1.5f, 8f);
        [SerializeField] private float cameraFollowSharpness = 18f;

        private FlightInputState latestServerInput = FlightInputState.Neutral;
        private Camera ownerCamera;

        private void Awake()
        {
            controller = controller != null ? controller : GetComponent<SkyCircuitFlightController>();
            body = body != null ? body : GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ConfigureAuthority();

            if (IsOwner && lockCursorForOwner)
            {
                LockCursor();
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsOwner && ownerCamera != null)
            {
                ownerCamera = null;
            }
        }

        private void Update()
        {
            if (IsOwner)
            {
                HandleCursorLock();
                FlightInputState input = ReadInput();
                SubmitInputServerRpc(input.Throttle, input.Turn, input.Vertical, input.LookDelta, input.Boost);
            }

            if (IsServer && controller != null)
            {
                controller.SetInput(latestServerInput);
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner || !followOwnerWithMainCamera)
            {
                return;
            }

            ownerCamera = ownerCamera != null ? ownerCamera : Camera.main;
            if (ownerCamera == null)
            {
                return;
            }

            Transform cameraTransform = ownerCamera.transform;
            Vector3 targetPosition = transform.TransformPoint(cameraOffset);
            Vector3 lookTarget = transform.TransformPoint(cameraLookOffset);
            float blend = 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);

            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, blend);
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - cameraTransform.position, Vector3.up);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, blend);
        }

        [ServerRpc]
        private void SubmitInputServerRpc(float throttle, float turn, float vertical, Vector2 lookDelta, bool boost)
        {
            latestServerInput = new FlightInputState(throttle, turn, vertical, lookDelta, boost);
        }

        private void ConfigureAuthority()
        {
            if (controller != null)
            {
                controller.enabled = IsServer;
                controller.SetInput(FlightInputState.Neutral);
            }

            if (body == null)
            {
                return;
            }

            body.isKinematic = !IsServer;
            body.useGravity = false;
            if (!IsServer)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
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
