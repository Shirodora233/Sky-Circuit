using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SkyCircuit.Networking
{
    [DisallowMultipleComponent]
    public sealed class LanRaceRoomControlHud : MonoBehaviour
    {
        [SerializeField] private LanRaceSessionController session;
        [SerializeField] private Key toggleKey = Key.Escape;
        [SerializeField] private Vector2 panelSize = new Vector2(470f, 252f);

        private static bool registered;

        private bool panelOpen;
        private bool confirmExit;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle mutedStyle;
        private GUIStyle buttonStyle;
        private GUIStyle dangerButtonStyle;

        public static bool IsPointerCaptured { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            if (!registered)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                registered = true;
            }

            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForActiveScene();
        }

        private static void EnsureForActiveScene()
        {
            LanRaceSessionController sessionController = FindAnyObjectByType<LanRaceSessionController>(FindObjectsInactive.Include);
            if (sessionController == null || sessionController.GetComponent<LanRaceRoomControlHud>() != null)
            {
                return;
            }

            LanRaceRoomControlHud hud = sessionController.gameObject.AddComponent<LanRaceRoomControlHud>();
            hud.session = sessionController;
        }

        private void Awake()
        {
            ResolveSession();
        }

        private void Update()
        {
            ResolveSession();
            if (session == null || !session.IsRoomControlAvailable)
            {
                panelOpen = false;
                confirmExit = false;
                SetPointerCaptured(false);
                return;
            }

            if (session.IsRoomClosing)
            {
                panelOpen = true;
                confirmExit = false;
                SetPointerCaptured(true);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            {
                panelOpen = !panelOpen;
                confirmExit = false;
                if (panelOpen)
                {
                    SetPointerCaptured(true);
                }
            }

            SetPointerCaptured(panelOpen);
        }

        private void OnDisable()
        {
            SetPointerCaptured(false);
        }

        private void OnDestroy()
        {
            SetPointerCaptured(false);
        }

        private void OnGUI()
        {
            ResolveSession();
            if (session == null || !session.IsRoomControlAvailable)
            {
                return;
            }

            EnsureStyles();
            DrawStatusButton();

            if (panelOpen || session.IsRoomClosing)
            {
                DrawRoomPanel();
            }
        }

        private void DrawStatusButton()
        {
            const float width = 278f;
            const float height = 42f;
            Rect rect = new Rect(Screen.width - width - 24f, 24f, width, height);

            Color previousColor = GUI.color;
            GUI.color = new Color(0.02f, 0.07f, 0.16f, 0.82f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = previousColor;

            Rect labelRect = new Rect(rect.x + 16f, rect.y + 8f, 172f, 26f);
            GUI.Label(labelRect, session.RoomStatusText, mutedStyle);

            Rect buttonRect = new Rect(rect.xMax - 74f, rect.y + 7f, 60f, 28f);
            GUI.enabled = !session.IsRoomClosing;
            if (GUI.Button(buttonRect, "\u623f\u95f4", buttonStyle))
            {
                panelOpen = true;
                confirmExit = false;
                SetPointerCaptured(true);
            }

            GUI.enabled = true;
        }

        private void DrawRoomPanel()
        {
            Rect rect = new Rect(
                (Screen.width - panelSize.x) * 0.5f,
                (Screen.height - panelSize.y) * 0.5f,
                panelSize.x,
                panelSize.y);

            Color previousColor = GUI.color;
            GUI.color = new Color(0.01f, 0.045f, 0.12f, 0.94f);
            GUI.Box(rect, GUIContent.none, panelStyle);
            GUI.color = previousColor;

            DrawAccent(rect);

            GUI.Label(new Rect(rect.x + 28f, rect.y + 22f, rect.width - 56f, 34f), "\u623f\u95f4\u72b6\u6001", titleStyle);
            GUI.Label(new Rect(rect.x + 30f, rect.y + 66f, rect.width - 60f, 34f), session.RoomStatusText, labelStyle);

            if (session.IsRoomClosing)
            {
                GUI.Label(
                    new Rect(rect.x + 30f, rect.y + 110f, rect.width - 60f, 44f),
                    "\u6b63\u5728\u5173\u95ed\u8054\u673a\u623f\u95f4\uff0c\u53cc\u65b9\u4f1a\u8fd4\u56de\u4e3b\u83dc\u5355\u3002",
                    mutedStyle);
                return;
            }

            string body = confirmExit
                ? "\u786e\u8ba4\u9000\u51fa\u623f\u95f4\uff1f\u5bf9\u65b9\u4e5f\u4f1a\u6536\u5230\u623f\u95f4\u5173\u95ed\u72b6\u6001\u5e76\u8fd4\u56de\u4e3b\u83dc\u5355\u3002"
                : "\u53ef\u4ee5\u7ee7\u7eed\u4f5c\u6218\uff0c\u6216\u4e3b\u52a8\u9000\u51fa\u5f53\u524d\u8054\u673a\u623f\u95f4\u3002";
            GUI.Label(new Rect(rect.x + 30f, rect.y + 110f, rect.width - 60f, 50f), body, mutedStyle);

            if (confirmExit)
            {
                if (GUI.Button(new Rect(rect.x + 30f, rect.yMax - 62f, 142f, 38f), "\u53d6\u6d88", buttonStyle))
                {
                    confirmExit = false;
                }

                if (GUI.Button(new Rect(rect.xMax - 178f, rect.yMax - 62f, 148f, 38f), "\u786e\u8ba4\u9000\u51fa", dangerButtonStyle))
                {
                    session.RequestLeaveRoom();
                }

                return;
            }

            if (GUI.Button(new Rect(rect.x + 30f, rect.yMax - 62f, 142f, 38f), "\u7ee7\u7eed\u4f5c\u6218", buttonStyle))
            {
                panelOpen = false;
            }

            if (GUI.Button(new Rect(rect.xMax - 178f, rect.yMax - 62f, 148f, 38f), "\u9000\u51fa\u623f\u95f4", dangerButtonStyle))
            {
                confirmExit = true;
            }
        }

        private void DrawAccent(Rect rect)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 0.96f, 0.02f, 0.92f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 3f, rect.y, 3f, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box);
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.96f, 0.02f, 1f) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 0.86f, 0.96f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            dangerButtonStyle = new GUIStyle(buttonStyle)
            {
                normal = { textColor = new Color(1f, 0.36f, 0.18f, 1f) },
                hover = { textColor = new Color(1f, 0.5f, 0.24f, 1f) }
            };
        }

        private void ResolveSession()
        {
            if (session == null)
            {
                session = GetComponent<LanRaceSessionController>();
            }

            if (session == null)
            {
                session = FindAnyObjectByType<LanRaceSessionController>(FindObjectsInactive.Include);
            }
        }

        private static void SetPointerCaptured(bool captured)
        {
            IsPointerCaptured = captured;
            if (captured)
            {
                UnlockCursor();
            }
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
