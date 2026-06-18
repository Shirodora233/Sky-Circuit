using System.Collections;
using System.Collections.Generic;
using SkyCircuit.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCircuit.Menu
{
    [DisallowMultipleComponent]
    public sealed class RaceTypeSelectionPanel : MonoBehaviour
    {
        private enum SelectionMode
        {
            Training,
            Lan
        }

        private static readonly CompetitorArchetype[] ArchetypeOrder =
        {
            CompetitorArchetype.Speeder,
            CompetitorArchetype.AllRounder,
            CompetitorArchetype.Fighter,
        };

        private static Sprite whiteSprite;

        private SkyCircuitMainMenuController controller;
        private LanRaceReadyCoordinator readyCoordinator;
        private RectTransform canvasRect;
        private RectTransform root;
        private Text titleText;
        private Text statusText;
        private Text actionButtonText;
        private Button actionButton;
        private Button backButton;
        private readonly TypeCard[] cards = new TypeCard[ArchetypeOrder.Length];
        private readonly List<MenuVisibilityState> hiddenMenuObjects = new List<MenuVisibilityState>();
        private CompetitorArchetype selectedArchetype = CompetitorArchetype.AllRounder;
        private SelectionMode mode;
        private Coroutine trainingCountdownRoutine;
        private bool initialized;
        private bool menuHidden;

        public bool IsOpen => root != null && root.gameObject.activeSelf;

        public void Configure(
            SkyCircuitMainMenuController menuController,
            RectTransform menuCanvas,
            LanRaceReadyCoordinator coordinator)
        {
            controller = menuController;
            canvasRect = menuCanvas;
            readyCoordinator = coordinator;
            EnsureBuilt();
            Hide();
        }

        public void ShowTraining()
        {
            EnsureBuilt();
            mode = SelectionMode.Training;
            selectedArchetype = CompetitorArchetype.AllRounder;
            StopTrainingCountdown();
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            controller.CloseSettings();
            HideMenuObjects();
            Refresh();
        }

        public void ShowLan()
        {
            EnsureBuilt();
            mode = SelectionMode.Lan;
            StopTrainingCountdown();
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            controller.CloseSettings();
            HideMenuObjects();
            Refresh();
        }

        public void Hide()
        {
            StopTrainingCountdown();
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }

            RestoreMenuObjects();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Refresh();
        }

        private void EnsureBuilt()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            if (canvasRect == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            }

            if (canvasRect == null)
            {
                Debug.LogWarning("Race type selection panel could not find a main menu canvas.");
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Texture2D logoStrip = Resources.Load<Texture2D>("SkyCircuit/Menu/SC_RaceTypeLogoStrip");
            root = CreateRect("Race Type Selection Panel", canvasRect, Vector2.zero, Vector2.zero);
            Stretch(root);

            titleText = CreateText(
                "Selection Title",
                root,
                string.Empty,
                font,
                44,
                FontStyle.Bold,
                TextAnchor.MiddleLeft,
                new Color(0.08f, 0.09f, 0.1f, 0.96f),
                new Vector2(540f, 54f),
                new Vector2(-270f, 234f));

            CreateTypeCards(font, logoStrip);

            statusText = CreateText(
                "Selection Status",
                root,
                string.Empty,
                font,
                24,
                FontStyle.Bold,
                TextAnchor.MiddleRight,
                new Color(0.18f, 0.22f, 0.26f, 0.92f),
                new Vector2(520f, 34f),
                new Vector2(-113f, -232f));

            backButton = CreateButton(
                "Selection Back Button",
                root,
                "返回",
                font,
                new Vector2(126f, 42f),
                new Vector2(-457f, -286f));
            backButton.onClick.AddListener(Hide);

            actionButton = CreateButton(
                "Selection Action Button",
                root,
                string.Empty,
                font,
                new Vector2(126f, 42f),
                new Vector2(231f, -286f));
            actionButtonText = actionButton.GetComponentInChildren<Text>();
            actionButton.onClick.AddListener(HandleActionClicked);
        }

        private void HideMenuObjects()
        {
            if (menuHidden || canvasRect == null || root == null)
            {
                return;
            }

            hiddenMenuObjects.Clear();
            for (int i = 0; i < canvasRect.childCount; i++)
            {
                Transform child = canvasRect.GetChild(i);
                if (child == null || child == root || child == transform)
                {
                    continue;
                }

                hiddenMenuObjects.Add(new MenuVisibilityState(child.gameObject, child.gameObject.activeSelf));
                child.gameObject.SetActive(false);
            }

            menuHidden = true;
        }

        private void RestoreMenuObjects()
        {
            if (!menuHidden)
            {
                return;
            }

            for (int i = 0; i < hiddenMenuObjects.Count; i++)
            {
                if (hiddenMenuObjects[i].GameObject != null)
                {
                    hiddenMenuObjects[i].GameObject.SetActive(hiddenMenuObjects[i].WasActive);
                }
            }

            hiddenMenuObjects.Clear();
            menuHidden = false;
        }

        private void CreateTypeCards(Font font, Texture2D logoStrip)
        {
            Vector2 cardSize = new Vector2(250f, 330f);
            float[] xPositions = { -395f, -113f, 169f };
            string[] labels = { "竞速型", "全能型", "缠斗型" };
            string[] hints =
            {
                "高速冲刺 / 直线优势",
                "均衡操控 / 稳定发挥",
                "强转向 / 背击压制",
            };

            for (int i = 0; i < ArchetypeOrder.Length; i++)
            {
                TypeCard card = new TypeCard
                {
                    Archetype = ArchetypeOrder[i],
                    Root = CreateRect($"Race Type Card {i + 1}", root, cardSize, new Vector2(xPositions[i], 28f)),
                };

                Image background = card.Root.gameObject.AddComponent<Image>();
                background.sprite = WhiteSprite;
                background.color = Color.white;

                Shadow shadow = card.Root.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
                shadow.effectDistance = new Vector2(7f, -7f);

                card.Outline = card.Root.gameObject.AddComponent<Outline>();
                card.Outline.effectColor = new Color(0.68f, 0.7f, 0.72f, 0.36f);
                card.Outline.effectDistance = new Vector2(2f, -2f);

                card.Button = card.Root.gameObject.AddComponent<Button>();
                card.Button.targetGraphic = background;
                card.Button.colors = BuildCardButtonColors();

                int capturedIndex = i;
                card.Button.onClick.AddListener(() => Select(ArchetypeOrder[capturedIndex]));

                card.Title = CreateText(
                    "Type Title",
                    card.Root,
                    labels[i],
                    font,
                    40,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Color(0.08f, 0.09f, 0.1f, 1f),
                    new Vector2(220f, 48f),
                    new Vector2(0f, 118f));

                RawImage logo = CreateRect("Type Logo", card.Root, new Vector2(188f, 165f), new Vector2(0f, -2f))
                    .gameObject.AddComponent<RawImage>();
                logo.texture = logoStrip;
                logo.uvRect = new Rect(i / 3f, 0f, 1f / 3f, 1f);
                logo.color = new Color(1f, 1f, 1f, 0.72f);
                logo.raycastTarget = false;

                card.Hint = CreateText(
                    "Type Hint",
                    card.Root,
                    hints[i],
                    font,
                    20,
                    FontStyle.Bold,
                    TextAnchor.MiddleCenter,
                    new Color(0.46f, 0.5f, 0.54f, 0.86f),
                    new Vector2(220f, 30f),
                    new Vector2(0f, -114f));

                card.Accent = CreateRect("Type Accent", card.Root, new Vector2(220f, 4f), new Vector2(0f, -154f))
                    .gameObject.AddComponent<Image>();
                card.Accent.sprite = WhiteSprite;
                card.Accent.color = new Color(1f, 0.35f, 0.02f, 0.72f);
                card.Accent.raycastTarget = false;

                cards[i] = card;
            }
        }

        private void Select(CompetitorArchetype archetype)
        {
            if (mode == SelectionMode.Lan && readyCoordinator != null && readyCoordinator.LocalReady)
            {
                return;
            }

            selectedArchetype = archetype;
            Refresh();
        }

        private void HandleActionClicked()
        {
            if (mode == SelectionMode.Training)
            {
                StartTrainingCountdown();
                return;
            }

            if (readyCoordinator == null)
            {
                controller.ShowMenuStatus("联机准备状态暂不可用");
                return;
            }

            readyCoordinator.SetLocalReady(selectedArchetype, !readyCoordinator.LocalReady);
            Refresh();
        }

        private void StartTrainingCountdown()
        {
            if (trainingCountdownRoutine != null)
            {
                return;
            }

            trainingCountdownRoutine = StartCoroutine(TrainingCountdownRoutine());
        }

        private IEnumerator TrainingCountdownRoutine()
        {
            float remaining = 3f;
            actionButton.interactable = false;
            while (remaining > 0f)
            {
                statusText.text = $"即将开始 {Mathf.CeilToInt(remaining)}";
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            trainingCountdownRoutine = null;
            controller.BeginSelectedTraining(selectedArchetype);
        }

        private void StopTrainingCountdown()
        {
            if (trainingCountdownRoutine != null)
            {
                StopCoroutine(trainingCountdownRoutine);
                trainingCountdownRoutine = null;
            }
        }

        private void Refresh()
        {
            if (root == null)
            {
                return;
            }

            bool lanMode = mode == SelectionMode.Lan;
            bool localReady = lanMode && readyCoordinator != null && readyCoordinator.LocalReady;
            bool locked = localReady || trainingCountdownRoutine != null;
            titleText.text = lanMode ? "联机作战准备" : "选择训练类型";

            for (int i = 0; i < cards.Length; i++)
            {
                bool selected = cards[i].Archetype == selectedArchetype;
                cards[i].Button.interactable = !locked;
                cards[i].Outline.effectColor = selected
                    ? new Color(1f, 0.35f, 0.02f, 0.9f)
                    : new Color(0.68f, 0.7f, 0.72f, 0.36f);
                cards[i].Accent.color = selected
                    ? new Color(1f, 0.35f, 0.02f, 1f)
                    : new Color(1f, 0.35f, 0.02f, 0.35f);
                cards[i].Title.color = selected
                    ? new Color(0.02f, 0.025f, 0.03f, 1f)
                    : new Color(0.12f, 0.13f, 0.14f, 0.92f);
            }

            backButton.interactable = !locked;
            actionButton.interactable = trainingCountdownRoutine == null
                && (!lanMode || (readyCoordinator != null && readyCoordinator.IsListening));
            actionButtonText.text = ResolveActionButtonText(lanMode, localReady);
            statusText.text = ResolveStatusText(lanMode, localReady);
        }

        private string ResolveActionButtonText(bool lanMode, bool localReady)
        {
            if (!lanMode)
            {
                return trainingCountdownRoutine != null ? "启动中" : "开始";
            }

            return localReady ? "取消准备" : "准备";
        }

        private string ResolveStatusText(bool lanMode, bool localReady)
        {
            if (!lanMode)
            {
                return trainingCountdownRoutine != null
                    ? statusText.text
                    : "AI 对手：全能型";
            }

            if (readyCoordinator == null || !readyCoordinator.IsListening)
            {
                return "联机连接已断开";
            }

            if (readyCoordinator.CountdownActive)
            {
                return $"双方已准备，{Mathf.CeilToInt(readyCoordinator.CountdownRemaining)} 秒后进入";
            }

            string left = ReadyText(0);
            string right = ReadyText(1);
            return $"{left}    {right}";
        }

        private string ReadyText(int slot)
        {
            if (readyCoordinator == null || !readyCoordinator.TryGetEntry(slot, out _, out CompetitorArchetype archetype, out bool ready))
            {
                return slot == 0 ? "Host: --" : "Client: --";
            }

            string role = slot == 0 ? "Host" : "Client";
            string state = ready ? "Ready" : "Selecting";
            return $"{role}: {ArchetypeLabel(archetype)} / {state}";
        }

        private static string ArchetypeLabel(CompetitorArchetype archetype)
        {
            switch (archetype)
            {
                case CompetitorArchetype.Speeder:
                    return "竞速型";
                case CompetitorArchetype.Fighter:
                    return "缠斗型";
                default:
                    return "全能型";
            }
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Font font,
            Vector2 size,
            Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = WhiteSprite;
            image.color = new Color(0.98f, 0.985f, 0.99f, 0.98f);
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.35f, 0.02f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = BuildButtonColors();
            CreateText(
                "Label",
                rect,
                label,
                font,
                22,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.06f, 0.07f, 0.08f, 1f),
                size,
                Vector2.zero);
            return button;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            string value,
            Font font,
            int fontSize,
            FontStyle style,
            TextAnchor alignment,
            Color color,
            Vector2 size,
            Vector2 position)
        {
            Text text = CreateRect(name, parent, size, position).gameObject.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size, Vector2 position)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static ColorBlock BuildButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.86f, 1f);
            colors.pressedColor = new Color(1f, 0.78f, 0.62f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.74f, 0.74f, 0.74f, 0.72f);
            return colors;
        }

        private static ColorBlock BuildCardButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.97f, 0.94f, 1f);
            colors.pressedColor = new Color(1f, 0.9f, 0.82f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.92f, 0.92f, 0.92f, 0.9f);
            return colors;
        }

        private static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    whiteSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f));
                }

                return whiteSprite;
            }
        }

        private struct TypeCard
        {
            public CompetitorArchetype Archetype;
            public RectTransform Root;
            public Button Button;
            public Outline Outline;
            public Text Title;
            public Text Hint;
            public Image Accent;
        }

        private readonly struct MenuVisibilityState
        {
            public readonly GameObject GameObject;
            public readonly bool WasActive;

            public MenuVisibilityState(GameObject gameObject, bool wasActive)
            {
                GameObject = gameObject;
                WasActive = wasActive;
            }
        }
    }
}
