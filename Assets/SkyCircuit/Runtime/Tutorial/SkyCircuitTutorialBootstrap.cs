using SkyCircuit.Flight;
using SkyCircuit.Match;
using SkyCircuit.Networking;
using SkyCircuit.Practice;
using SkyCircuit.Race;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SkyCircuit.Tutorial
{
    public static class SkyCircuitTutorialBootstrap
    {
        private const string PendingTutorialKey = "SkyCircuit.Tutorial.Pending";

        private static bool registered;

        public static void RequestTutorial()
        {
            PlayerPrefs.SetInt(PendingTutorialKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            if (!registered)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                registered = true;
            }

            TryCreateTutorial(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateTutorial(scene);
        }

        private static void TryCreateTutorial(Scene scene)
        {
            if (!scene.IsValid() || PlayerPrefs.GetInt(PendingTutorialKey, 0) == 0)
            {
                return;
            }

            if (Object.FindAnyObjectByType<SkyCircuitTutorialController>() != null)
            {
                return;
            }

            bool hasCombatTutorialScene = Object.FindAnyObjectByType<LanRaceSessionController>() != null;
            bool hasPracticeTutorialScene = Object.FindAnyObjectByType<FlightPracticeRoute>() != null;
            if (!hasCombatTutorialScene && !hasPracticeTutorialScene)
            {
                return;
            }

            PlayerPrefs.DeleteKey(PendingTutorialKey);
            PlayerPrefs.Save();

            var tutorial = new GameObject("Sky Circuit Tutorial Controller");
            tutorial.AddComponent<SkyCircuitTutorialController>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class SkyCircuitTutorialController : MonoBehaviour
    {
        private enum TutorialStep
        {
            Intro,
            Controls,
            Buoys,
            BackHitRule,
            LanFlow,
            Complete
        }

        [SerializeField] private string mainMenuSceneName = "V0_10_MainMenu";
        [SerializeField] private int buoyGoal = 3;
        [SerializeField] private float movementGoalDistance = 12f;
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        private static readonly Color PanelColor = new Color(0.015f, 0.055f, 0.13f, 0.82f);
        private static readonly Color ModalColor = new Color(0.018f, 0.065f, 0.16f, 0.92f);
        private static readonly Color BackdropColor = new Color(0.01f, 0.025f, 0.06f, 0.34f);
        private static readonly Color BorderColor = new Color(1f, 0.96f, 0.02f, 0.95f);
        private static readonly Color CyanColor = new Color(0.26f, 0.88f, 1f, 1f);
        private static readonly Color OrangeColor = new Color(1f, 0.46f, 0.16f, 1f);
        private static readonly Color TextColor = new Color(0.88f, 0.94f, 1f, 1f);
        private static readonly Color MutedTextColor = new Color(0.62f, 0.78f, 0.9f, 1f);

        private FlightPracticeRoute practiceRoute;
        private BuoyRoute combatRoute;
        private LanRaceSessionController lanSession;
        private Competitor tutorialPlayer;
        private Competitor tutorialOpponent;
        private SkyCircuitFlightController flight;
        private Transform player;
        private Font font;
        private bool combatTutorialInitialized;

        private TutorialStep step;
        private Vector3 movementStart;
        private int buoyStartCount;
        private int displayedBuoys;
        private float stepClock;
        private float progressFlash;

        private RectTransform modalRoot;
        private RectTransform modalPanel;
        private RectTransform hudPanel;
        private RectTransform controlsPanel;
        private Text modalKicker;
        private Text modalTitle;
        private Text modalBody;
        private Text modalHint;
        private Text objectiveTitle;
        private Text objectiveBody;
        private Text objectiveProgress;
        private Text controlsText;
        private Image modalAccent;
        private Image hudAccent;
        private Image[] progressPips = new Image[0];

        private void Awake()
        {
            ResolveSceneObjects();
            EnsureCombatTutorialSession();
            ResolveSceneObjects();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
            SetStep(TutorialStep.Intro);
        }

        private void Update()
        {
            ResolveSceneObjects();
            stepClock += Time.deltaTime;
            progressFlash = Mathf.Max(0f, progressFlash - Time.deltaTime);

            switch (step)
            {
                case TutorialStep.Intro:
                    if (WasContinuePressed())
                    {
                        SetStep(TutorialStep.Controls);
                    }
                    break;
                case TutorialStep.Controls:
                    UpdateControlsStep();
                    break;
                case TutorialStep.Buoys:
                    UpdateBuoyStep();
                    break;
                case TutorialStep.BackHitRule:
                    if (WasContinuePressed())
                    {
                        SetStep(TutorialStep.LanFlow);
                    }
                    break;
                case TutorialStep.LanFlow:
                    if (WasContinuePressed())
                    {
                        SetStep(TutorialStep.Complete);
                    }
                    break;
                case TutorialStep.Complete:
                    if (WasMenuPressed())
                    {
                        ReturnToMainMenu();
                    }
                    else if (WasContinuePressed())
                    {
                        Destroy(gameObject);
                    }
                    break;
            }

            RefreshDynamicUi();
        }

        private void ResolveSceneObjects()
        {
            if (lanSession == null)
            {
                lanSession = Object.FindAnyObjectByType<LanRaceSessionController>();
            }

            if (combatRoute == null)
            {
                combatRoute = lanSession != null
                    ? lanSession.Route
                    : Object.FindAnyObjectByType<BuoyRoute>();
            }

            if (practiceRoute == null)
            {
                practiceRoute = Object.FindAnyObjectByType<FlightPracticeRoute>();
            }

            if (tutorialPlayer == null && lanSession != null)
            {
                tutorialPlayer = lanSession.LeftCompetitor;
                tutorialOpponent = lanSession.RightCompetitor;
            }

            if (flight == null)
            {
                PlayerFlightInput input = Object.FindAnyObjectByType<PlayerFlightInput>();
                flight = input != null
                    ? input.GetComponent<SkyCircuitFlightController>()
                    : tutorialPlayer != null ? tutorialPlayer.Controller : Object.FindAnyObjectByType<SkyCircuitFlightController>();
            }

            if (player == null && flight != null)
            {
                player = flight.transform;
            }
        }

        private void EnsureCombatTutorialSession()
        {
            if (combatTutorialInitialized || lanSession == null || lanSession.IsOfflineTutorialActive)
            {
                return;
            }

            combatTutorialInitialized = true;
            if (!RaceSceneBootstrap.TryStartOfflineRace(lanSession, out tutorialPlayer, out tutorialOpponent))
            {
                Debug.LogWarning("Tutorial could not start an offline race session.");
                return;
            }
        }

        private int CurrentBuoyScore
        {
            get
            {
                if (tutorialPlayer != null)
                {
                    return tutorialPlayer.BuoyScoreCount;
                }

                return practiceRoute != null ? practiceRoute.GatesTouched : 0;
            }
        }

        private void UpdateControlsStep()
        {
            float moved = player != null ? Vector3.Distance(player.position, movementStart) : 0f;
            bool hasStartedFlying = moved >= movementGoalDistance || (flight != null && flight.CurrentSpeed > 8f);
            if (hasStartedFlying || WasContinuePressed())
            {
                SetStep(TutorialStep.Buoys);
            }
        }

        private void UpdateBuoyStep()
        {
            int touched = Mathf.Max(0, CurrentBuoyScore - buoyStartCount);
            int clamped = Mathf.Clamp(touched, 0, buoyGoal);
            if (clamped != displayedBuoys)
            {
                displayedBuoys = clamped;
                progressFlash = 0.42f;
            }

            if (displayedBuoys >= buoyGoal)
            {
                SetStep(TutorialStep.BackHitRule);
            }
        }

        private void SetStep(TutorialStep nextStep)
        {
            step = nextStep;
            stepClock = 0f;

            if (step == TutorialStep.Controls)
            {
                movementStart = player != null ? player.position : Vector3.zero;
            }
            else if (step == TutorialStep.Buoys)
            {
                buoyStartCount = CurrentBuoyScore;
                displayedBuoys = 0;
                progressFlash = 0f;
            }

            ApplyStaticUi();
            RefreshDynamicUi();
        }

        private void ApplyStaticUi()
        {
            bool showModal = step == TutorialStep.Intro
                || step == TutorialStep.BackHitRule
                || step == TutorialStep.LanFlow
                || step == TutorialStep.Complete;
            bool showObjective = step == TutorialStep.Controls || step == TutorialStep.Buoys;

            modalRoot.gameObject.SetActive(showModal);
            hudPanel.gameObject.SetActive(showObjective);
            controlsPanel.gameObject.SetActive(showObjective);

            switch (step)
            {
                case TutorialStep.Intro:
                    SetModal(
                        "SC TUTORIAL 01",
                        "云海竞速规则",
                        "先熟悉飞行手感，然后按路线触碰发光浮标。正式作战中，浮标是基础得分，也是解锁背后撞击的开关。",
                        "Space / Enter / 鼠标左键 继续",
                        CyanColor);
                    break;
                case TutorialStep.Controls:
                    objectiveTitle.text = "校准飞行";
                    objectiveBody.text = "W/S 控制推进，A/D 修正航向，鼠标调整视角。Space 上升，Ctrl 或 C 下降，Q 进入冲刺。";
                    controlsText.text = "W/S 推进   A/D 转向   鼠标 看向航线   Space 上升   Ctrl/C 下降   Q 冲刺";
                    SetHudAccent(CyanColor);
                    break;
                case TutorialStep.Buoys:
                    objectiveTitle.text = "触碰发光浮标";
                    objectiveBody.text = "跟随黄色目标浮标穿过路线。每触碰一个目标浮标，训练计数 +1。";
                    controlsText.text = "保持航向稳定，贴近当前发光浮标即可得分";
                    SetHudAccent(BorderColor);
                    break;
                case TutorialStep.BackHitRule:
                    SetModal(
                        "COMBAT RULE",
                        "浮标先行，背后命中加分",
                        "作战时浮标 +1。任意一方拿到浮标分后，背后撞击开启：从对手后方命中可获得 +2，并会打乱对方速度。",
                        "Space / Enter / 鼠标左键 继续",
                        OrangeColor);
                    break;
                case TutorialStep.LanFlow:
                    SetModal(
                        "LAN FLOW",
                        "从 V010 进入联机作战",
                        "主菜单选择联机设置，建立 Host / Client 连接后由主机点击作战。进入场景后，双方各自控制自己的飞行员，计分和航迹状态会同步。",
                        "Space / Enter / 鼠标左键 继续",
                        CyanColor);
                    break;
                case TutorialStep.Complete:
                    SetModal(
                        "READY",
                        "教程完成",
                        "你已经完成第一段训练：飞行控制、浮标得分、背后撞击和联机入口都已讲完。可以继续自由练习，或回到主菜单开始联机。",
                        "M 返回主菜单   Space 留在训练",
                        BorderColor);
                    break;
            }
        }

        private void RefreshDynamicUi()
        {
            if (step == TutorialStep.Controls)
            {
                float moved = player != null ? Vector3.Distance(player.position, movementStart) : 0f;
                float progress = Mathf.Clamp01(moved / Mathf.Max(1f, movementGoalDistance));
                objectiveProgress.text = $"移动校准 {Mathf.RoundToInt(progress * 100f)}%";
                SetProgress(progress, 3);
            }
            else if (step == TutorialStep.Buoys)
            {
                objectiveProgress.text = $"浮标进度 {displayedBuoys}/{buoyGoal}";
                SetProgress(buoyGoal > 0 ? displayedBuoys / (float)buoyGoal : 0f, buoyGoal);
            }
        }

        private void SetModal(string kicker, string title, string body, string hint, Color accent)
        {
            modalKicker.text = kicker;
            modalTitle.text = title;
            modalBody.text = body;
            modalHint.text = hint;
            modalAccent.color = accent;
            BuildBorder(modalPanel, BorderColor, 3f);
        }

        private void SetHudAccent(Color color)
        {
            hudAccent.color = color;
        }

        private void SetProgress(float normalized, int activeSlots)
        {
            normalized = Mathf.Clamp01(normalized);
            int slots = Mathf.Clamp(activeSlots, 1, progressPips.Length);
            int lit = Mathf.RoundToInt(normalized * slots);
            Color active = progressFlash > 0f ? Color.Lerp(BorderColor, Color.white, progressFlash * 1.8f) : BorderColor;

            for (int i = 0; i < progressPips.Length; i++)
            {
                bool inUse = i < slots;
                bool isLit = i < lit;
                progressPips[i].gameObject.SetActive(inUse);
                progressPips[i].color = isLit ? active : new Color(0.18f, 0.36f, 0.5f, 0.65f);
            }
        }

        private void ReturnToMainMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.LoadScene(mainMenuSceneName);
        }

        private static bool WasContinuePressed()
        {
            if (Time.unscaledTime < 0.2f)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null
                && (keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        private static bool WasMenuPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.mKey.wasPressedThisFrame;
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("Tutorial Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform root = canvasObject.GetComponent<RectTransform>();
            Stretch(root);

            modalRoot = CreateRect(root, "Modal Root");
            Stretch(modalRoot);

            Image backdrop = CreateImage(modalRoot, "Backdrop", BackdropColor);
            Stretch(backdrop.rectTransform);

            modalPanel = CreatePanel(modalRoot, "Modal Panel", new Vector2(920f, 390f), ModalColor);
            modalPanel.anchorMin = new Vector2(0.5f, 0.5f);
            modalPanel.anchorMax = new Vector2(0.5f, 0.5f);
            modalPanel.pivot = new Vector2(0.5f, 0.5f);
            modalPanel.anchoredPosition = new Vector2(0f, -42f);

            modalAccent = CreateImage(modalPanel, "Accent Bar", CyanColor);
            SetTopLeftRect(modalAccent.rectTransform, 0f, 0f, 920f, 7f);

            modalKicker = CreateText(modalPanel, "Kicker", 22, FontStyle.Bold, BorderColor, TextAnchor.MiddleLeft);
            SetTopLeftRect(modalKicker.rectTransform, 34f, 28f, 260f, 34f);

            Text emblem = CreateText(modalPanel, "Emblem", 28, FontStyle.Bold, BorderColor, TextAnchor.MiddleCenter);
            emblem.text = "SC";
            SetTopRightRect(emblem.rectTransform, 28f, 24f, 58f, 42f);

            modalTitle = CreateText(modalPanel, "Title", 48, FontStyle.Bold, BorderColor, TextAnchor.UpperLeft);
            SetTopLeftRect(modalTitle.rectTransform, 34f, 82f, 830f, 72f);

            modalBody = CreateText(modalPanel, "Body", 25, FontStyle.Normal, TextColor, TextAnchor.UpperLeft);
            modalBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            modalBody.verticalOverflow = VerticalWrapMode.Truncate;
            modalBody.lineSpacing = 1.16f;
            SetTopLeftRect(modalBody.rectTransform, 36f, 164f, 835f, 132f);

            modalHint = CreateText(modalPanel, "Hint", 22, FontStyle.Bold, CyanColor, TextAnchor.MiddleRight);
            SetBottomRightRect(modalHint.rectTransform, 34f, 24f, 480f, 42f);

            hudPanel = CreatePanel(root, "Objective Panel", new Vector2(540f, 178f), PanelColor);
            hudPanel.anchorMin = new Vector2(0f, 1f);
            hudPanel.anchorMax = new Vector2(0f, 1f);
            hudPanel.pivot = new Vector2(0f, 1f);
            hudPanel.anchoredPosition = new Vector2(36f, -42f);

            hudAccent = CreateImage(hudPanel, "Hud Accent", CyanColor);
            SetTopLeftRect(hudAccent.rectTransform, 0f, 0f, 540f, 6f);

            Text hudKicker = CreateText(hudPanel, "Hud Kicker", 18, FontStyle.Bold, BorderColor, TextAnchor.MiddleLeft);
            hudKicker.text = "TRAINING";
            SetTopLeftRect(hudKicker.rectTransform, 24f, 18f, 160f, 28f);

            objectiveTitle = CreateText(hudPanel, "Objective Title", 31, FontStyle.Bold, BorderColor, TextAnchor.MiddleLeft);
            SetTopLeftRect(objectiveTitle.rectTransform, 24f, 48f, 490f, 42f);

            objectiveBody = CreateText(hudPanel, "Objective Body", 18, FontStyle.Normal, TextColor, TextAnchor.UpperLeft);
            objectiveBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            objectiveBody.lineSpacing = 1.08f;
            SetTopLeftRect(objectiveBody.rectTransform, 24f, 96f, 492f, 48f);

            objectiveProgress = CreateText(hudPanel, "Objective Progress", 19, FontStyle.Bold, CyanColor, TextAnchor.MiddleLeft);
            SetBottomLeftRect(objectiveProgress.rectTransform, 24f, 16f, 180f, 24f);

            BuildProgressPips(hudPanel);

            controlsPanel = CreatePanel(root, "Controls Panel", new Vector2(940f, 72f), new Color(0.015f, 0.055f, 0.13f, 0.72f));
            controlsPanel.anchorMin = new Vector2(0.5f, 0f);
            controlsPanel.anchorMax = new Vector2(0.5f, 0f);
            controlsPanel.pivot = new Vector2(0.5f, 0f);
            controlsPanel.anchoredPosition = new Vector2(0f, 34f);

            Image controlsAccent = CreateImage(controlsPanel, "Controls Accent", BorderColor);
            SetTopLeftRect(controlsAccent.rectTransform, 0f, 0f, 940f, 4f);

            controlsText = CreateText(controlsPanel, "Controls Text", 21, FontStyle.Bold, TextColor, TextAnchor.MiddleCenter);
            controlsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetCenterRect(controlsText.rectTransform, 0f, -2f, 884f, 52f);
        }

        private void BuildProgressPips(RectTransform parent)
        {
            progressPips = new Image[Mathf.Max(3, buoyGoal)];
            for (int i = 0; i < progressPips.Length; i++)
            {
                Image pip = CreateImage(parent, $"Progress Pip {i + 1}", new Color(0.18f, 0.36f, 0.5f, 0.65f));
                SetBottomRightRect(pip.rectTransform, 24f + (progressPips.Length - 1 - i) * 44f, 20f, 34f, 11f);
                progressPips[i] = pip;
            }
        }

        private Text CreateText(RectTransform parent, string childName, int size, FontStyle style, Color color, TextAnchor alignment)
        {
            RectTransform child = CreateRect(parent, childName);
            Text text = child.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(size * 0.66f));
            text.resizeTextMaxSize = size;

            Outline outline = child.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.42f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        private RectTransform CreatePanel(RectTransform parent, string childName, Vector2 size, Color color)
        {
            RectTransform panel = CreateRect(parent, childName);
            panel.sizeDelta = size;

            Image background = panel.gameObject.AddComponent<Image>();
            background.color = color;
            background.raycastTarget = false;

            BuildBorder(panel, BorderColor, 3f);
            return panel;
        }

        private Image CreateImage(RectTransform parent, string childName, Color color)
        {
            RectTransform child = CreateRect(parent, childName);
            Image image = child.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(Transform parent, string childName)
        {
            var child = new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static void BuildBorder(RectTransform panel, Color color, float thickness)
        {
            float width = Mathf.Max(0f, panel.sizeDelta.x);
            float height = Mathf.Max(0f, panel.sizeDelta.y);
            CreateBorder(panel, "Border Top", color, 0f, 0f, width, thickness, true);
            CreateBorder(panel, "Border Bottom", color, 0f, 0f, width, thickness, false);
            CreateBorder(panel, "Border Left", color, 0f, 0f, thickness, height, true);
            CreateBorder(panel, "Border Right", color, 0f, 0f, thickness, height, true, true);
        }

        private static void CreateBorder(
            RectTransform panel,
            string childName,
            Color color,
            float x,
            float y,
            float width,
            float height,
            bool top,
            bool right = false)
        {
            Transform existing = panel.Find(childName);
            Image image;
            RectTransform rect;
            if (existing != null && existing.TryGetComponent(out image))
            {
                rect = image.rectTransform;
            }
            else
            {
                rect = CreateRect(panel, childName);
                image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
            }

            image.color = color;
            if (right)
            {
                SetTopRightRect(rect, x, y, width, height);
            }
            else if (top)
            {
                SetTopLeftRect(rect, x, y, width, height);
            }
            else
            {
                SetBottomLeftRect(rect, x, y, width, height);
            }
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetTopLeftRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(x, -y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopRightRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-x, -y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private static void SetBottomLeftRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = new Vector2(x, y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private static void SetBottomRightRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(1f, 0f);
            rectTransform.anchoredPosition = new Vector2(-x, y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }

        private static void SetCenterRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(x, y);
            rectTransform.sizeDelta = new Vector2(width, height);
        }
    }

}
