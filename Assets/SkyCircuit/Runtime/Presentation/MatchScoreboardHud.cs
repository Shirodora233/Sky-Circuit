using SkyCircuit.Match;
using SkyCircuit.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCircuit.Presentation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MatchScoreboardHud : MonoBehaviour
    {
        [SerializeField] private MatchController match;
        [SerializeField] private LanRaceSessionController lanSession;

        [Header("Layout")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private Vector2 boardSize = new Vector2(780f, 170f);
        [SerializeField] private float topOffset = 18f;
        [SerializeField] private float centerGap = 96f;
        [SerializeField, Range(0.18f, 0.42f)] private float headerHeightRatio = 0.28f;
        [SerializeField, Range(1, 4)] private int scoreDigits = 2;

        [Header("Text")]
        [SerializeField] private string leftNameOverride = string.Empty;
        [SerializeField] private string rightNameOverride = string.Empty;
        [SerializeField] private int nameFontSize = 26;
        [SerializeField] private Font labelFont = null;

        [Header("Visuals")]
        [SerializeField] private Color lineColor = new Color(0.95f, 0.92f, 0.02f, 0.92f);
        [SerializeField] private Color scoreColor = new Color(1f, 0.96f, 0f, 1f);
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.38f);
        [SerializeField] private Color glowColor = new Color(1f, 0.96f, 0f, 0f);
        [SerializeField] private float borderThickness = 3f;
        [SerializeField] private float glowPadding = 12f;
        [SerializeField, Range(0.06f, 0.28f)] private float segmentThickness = 0.15f;
        [SerializeField, Range(0f, 1f)] private float inactiveSegmentAlpha = 0f;

        private const string CanvasName = "Scoreboard Canvas";
        private const string RootName = "Scoreboard Root";
        private const string LeftPanelName = "Left Scoreboard";
        private const string RightPanelName = "Right Scoreboard";

        private RectTransform root;
        private Font resolvedLabelFont;
        private Text leftNameText;
        private Text rightNameText;
        private SevenSegmentScoreGraphic leftScore;
        private SevenSegmentScoreGraphic rightScore;
        private SevenSegmentScoreGraphic leftScoreGlow;
        private SevenSegmentScoreGraphic rightScoreGlow;

        public void Configure(MatchController matchController)
        {
            match = matchController;
            lanSession = null;
            RebuildNow();
        }

        public void Configure(LanRaceSessionController sessionController)
        {
            match = null;
            lanSession = sessionController;
            RebuildNow();
        }

        [ContextMenu("Rebuild Scoreboard UI")]
        public void RebuildNow()
        {
            EnsureUi();
            ApplyScores();
        }

        private void Reset()
        {
            match = FindAnyObjectByType<MatchController>();
        }

        private void Awake()
        {
            EnsureUi();
        }

        private void OnEnable()
        {
            EnsureUi();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            EnsureUi();
            ApplyScores();
        }

        private void LateUpdate()
        {
            EnsureUi();
            ApplyScores();
        }

        private void EnsureUi()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            RectTransform canvasTransform = EnsureRectChild(transform, CanvasName);
            Canvas canvas = EnsureComponent<Canvas>(canvasTransform.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;

            CanvasScaler scaler = EnsureComponent<CanvasScaler>(canvasTransform.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            EnsureComponent<GraphicRaycaster>(canvasTransform.gameObject);

            root = EnsureRectChild(canvasTransform, RootName);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -Mathf.Max(0f, topOffset));
            root.sizeDelta = boardSize;

            RectTransform leftPanel = EnsureRectChild(root, LeftPanelName);
            RectTransform rightPanel = EnsureRectChild(root, RightPanelName);
            ConfigurePanelRect(leftPanel, true);
            ConfigurePanelRect(rightPanel, false);
            BuildPanel(leftPanel, true);
            BuildPanel(rightPanel, false);
        }

        private void ConfigurePanelRect(RectTransform panel, bool left)
        {
            float width = Mathf.Max(100f, (boardSize.x - Mathf.Max(0f, centerGap)) * 0.5f);
            panel.anchorMin = new Vector2(left ? 0f : 1f, 0.5f);
            panel.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
            panel.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(width, boardSize.y);
        }

        private void BuildPanel(RectTransform panel, bool left)
        {
            Image background = EnsureImage(panel, "Background");
            Stretch(background.rectTransform);
            background.color = backgroundColor;
            background.raycastTarget = false;

            BuildBorder(panel);

            float headerHeight = Mathf.Clamp(boardSize.y * headerHeightRatio, 32f, boardSize.y * 0.5f);
            float iconWidth = headerHeight;
            float scoreHeight = Mathf.Max(20f, boardSize.y - headerHeight);

            Text icon = EnsureText(panel, "Emblem");
            icon.text = "SC";
            icon.fontSize = Mathf.RoundToInt(headerHeight * 0.42f);
            icon.fontStyle = FontStyle.Bold;
            icon.alignment = TextAnchor.MiddleCenter;
            icon.color = scoreColor;
            SetTopLeftRect(icon.rectTransform, 0f, 0f, iconWidth, headerHeight);

            Text name = EnsureText(panel, "Name");
            name.fontSize = nameFontSize;
            name.fontStyle = FontStyle.Bold;
            name.alignment = TextAnchor.MiddleCenter;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 14;
            name.resizeTextMaxSize = Mathf.Max(14, nameFontSize);
            name.color = scoreColor;
            SetTopLeftRect(name.rectTransform, iconWidth, 0f, panel.rect.width - iconWidth, headerHeight);

            SevenSegmentScoreGraphic glow = EnsureSegment(panel, "Score Glow");
            SevenSegmentScoreGraphic score = EnsureSegment(panel, "Score");
            SetTopLeftRect(glow.rectTransform, -glowPadding, headerHeight - glowPadding * 0.5f, panel.rect.width + glowPadding * 2f, scoreHeight + glowPadding * 1.5f);
            SetTopLeftRect(score.rectTransform, 16f, headerHeight + 7f, panel.rect.width - 32f, scoreHeight - 14f);
            glow.SetSegmentStyle(glowColor, segmentThickness, 0f);
            score.SetSegmentStyle(scoreColor, segmentThickness, inactiveSegmentAlpha);
            glow.Refresh();
            score.Refresh();
            glow.gameObject.SetActive(glowColor.a > 0f);

            Image headerLine = EnsureImage(panel, "Header Line");
            SetTopLeftRect(headerLine.rectTransform, 0f, headerHeight - borderThickness * 0.5f, panel.rect.width, borderThickness);
            headerLine.color = lineColor;
            headerLine.raycastTarget = false;

            Image iconLine = EnsureImage(panel, "Emblem Line");
            SetTopLeftRect(iconLine.rectTransform, iconWidth - borderThickness * 0.5f, 0f, borderThickness, headerHeight);
            iconLine.color = lineColor;
            iconLine.raycastTarget = false;

            if (left)
            {
                leftNameText = name;
                leftScore = score;
                leftScoreGlow = glow;
            }
            else
            {
                rightNameText = name;
                rightScore = score;
                rightScoreGlow = glow;
            }
        }

        private void BuildBorder(RectTransform panel)
        {
            Image top = EnsureImage(panel, "Border Top");
            SetTopLeftRect(top.rectTransform, 0f, 0f, panel.rect.width, borderThickness);
            top.color = lineColor;
            top.raycastTarget = false;

            Image bottom = EnsureImage(panel, "Border Bottom");
            SetBottomLeftRect(bottom.rectTransform, 0f, 0f, panel.rect.width, borderThickness);
            bottom.color = lineColor;
            bottom.raycastTarget = false;

            Image left = EnsureImage(panel, "Border Left");
            SetTopLeftRect(left.rectTransform, 0f, 0f, borderThickness, panel.rect.height);
            left.color = lineColor;
            left.raycastTarget = false;

            Image right = EnsureImage(panel, "Border Right");
            SetTopRightRect(right.rectTransform, 0f, 0f, borderThickness, panel.rect.height);
            right.color = lineColor;
            right.raycastTarget = false;
        }

        private void ApplyScores()
        {
            if (lanSession != null)
            {
                ApplySide(leftNameText, leftScore, leftScoreGlow, lanSession.LeftDisplayName, lanSession.LeftScore, leftNameOverride);
                ApplySide(rightNameText, rightScore, rightScoreGlow, lanSession.RightDisplayName, lanSession.RightScore, rightNameOverride);
                return;
            }

            Competitor player = match != null ? match.Player : null;
            Competitor opponent = match != null ? match.Opponent : null;

            ApplySide(leftNameText, leftScore, leftScoreGlow, player, leftNameOverride);
            ApplySide(rightNameText, rightScore, rightScoreGlow, opponent, rightNameOverride);
        }

        private void ApplySide(
            Text nameText,
            SevenSegmentScoreGraphic scoreGraphic,
            SevenSegmentScoreGraphic glowGraphic,
            Competitor competitor,
            string overrideName)
        {
            string label = !string.IsNullOrWhiteSpace(overrideName)
                ? overrideName
                : competitor != null ? competitor.DisplayName : "--";
            int score = competitor != null ? competitor.Score : 0;

            ApplySide(nameText, scoreGraphic, glowGraphic, label, score, string.Empty);
        }

        private void ApplySide(
            Text nameText,
            SevenSegmentScoreGraphic scoreGraphic,
            SevenSegmentScoreGraphic glowGraphic,
            string label,
            int score,
            string overrideName)
        {
            if (!string.IsNullOrWhiteSpace(overrideName))
            {
                label = overrideName;
            }

            if (nameText != null)
            {
                nameText.text = label;
                nameText.color = scoreColor;
                if (labelFont != null)
                {
                    nameText.font = labelFont;
                }
                else if (resolvedLabelFont != null)
                {
                    nameText.font = resolvedLabelFont;
                }
            }

            scoreGraphic?.SetValue(score, scoreDigits);
            glowGraphic?.SetValue(score, scoreDigits);
        }

        private static RectTransform EnsureRectChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            {
                return existingRect;
            }

            var child = new GameObject(childName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static Image EnsureImage(RectTransform parent, string childName)
        {
            RectTransform child = EnsureRectChild(parent, childName);
            Image image = EnsureComponent<Image>(child.gameObject);
            image.raycastTarget = false;
            return image;
        }

        private Text EnsureText(RectTransform parent, string childName)
        {
            RectTransform child = EnsureRectChild(parent, childName);
            Text text = EnsureComponent<Text>(child.gameObject);
            text.raycastTarget = false;
            Font font = ResolveLabelFont();
            if (font != null)
            {
                text.font = font;
            }

            return text;
        }

        private Font ResolveLabelFont()
        {
            if (labelFont != null)
            {
                return labelFont;
            }

            if (resolvedLabelFont == null)
            {
                resolvedLabelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return resolvedLabelFont;
        }

        private static SevenSegmentScoreGraphic EnsureSegment(RectTransform parent, string childName)
        {
            RectTransform child = EnsureRectChild(parent, childName);
            SevenSegmentScoreGraphic graphic = EnsureComponent<SevenSegmentScoreGraphic>(child.gameObject);
            graphic.raycastTarget = false;
            return graphic;
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
            rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        private static void SetTopRightRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-x, -y);
            rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }

        private static void SetBottomLeftRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = Vector2.zero;
            rectTransform.anchoredPosition = new Vector2(x, y);
            rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
        }
    }
}
