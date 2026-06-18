using SkyCircuit.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SkyCircuit.Flight
{
    [RequireComponent(typeof(SkyCircuitFlightController))]
    public sealed class PlayerFlightInput : MonoBehaviour
    {
        [SerializeField] private bool lockCursorOnStart = true;
        [SerializeField] private float gamepadLookScale = 16f;

        private SkyCircuitFlightController controller;
        private bool inputEnabled = true;

        private void Awake()
        {
            controller = GetComponent<SkyCircuitFlightController>();
        }

        private void OnEnable()
        {
            if (lockCursorOnStart)
            {
                LockCursor();
            }
        }

        private void Update()
        {
            HandleCursorLock();
            controller.SetInput(inputEnabled ? ReadInput() : FlightInputState.Neutral);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!inputEnabled && controller != null)
            {
                controller.SetInput(FlightInputState.Neutral);
            }
        }

        private FlightInputState ReadInput()
        {
            float throttle = 0f;
            float turn = 0f;
            float vertical = 0f;
            bool boost = false;
            Vector2 lookDelta = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                throttle += ReadKey(keyboard.wKey) - ReadKey(keyboard.sKey);
                turn += ReadKey(keyboard.dKey) - ReadKey(keyboard.aKey);
                vertical += ReadKey(keyboard.spaceKey) - Mathf.Max(ReadKey(keyboard.leftCtrlKey), ReadKey(keyboard.rightCtrlKey), ReadKey(keyboard.cKey));
                boost |= keyboard.qKey.isPressed;
            }

            var mouse = Mouse.current;
            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                lookDelta += mouse.delta.ReadValue();
            }

            var gamepad = Gamepad.current;
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
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            var mouse = Mouse.current;
            if (mouse != null
                && mouse.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked
                && !ShouldIgnoreCursorLockClick())
            {
                LockCursor();
            }
        }

        private static bool ShouldIgnoreCursorLockClick()
        {
            return LanRaceRoomControlHud.IsPointerCaptured
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
