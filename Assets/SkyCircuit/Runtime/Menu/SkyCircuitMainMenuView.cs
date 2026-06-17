using System;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCircuit.Menu
{
    [ExecuteAlways]
    public sealed class SkyCircuitMainMenuView : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private Texture2D logoTexture;
        [SerializeField] private Texture2D iconTexture;
        [SerializeField] private Texture2D combatPreviewTexture;
        [SerializeField] private Texture2D combatTitleTexture;
        [SerializeField] private Texture2D trainingTitleTexture;
        [SerializeField] private Texture2D tutorialTitleTexture;
        [SerializeField] private Texture2D settingsTitleTexture;
        [SerializeField] private Font menuFont;

        [Header("Canvas")]
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Vector2 canvasSize = new Vector2(1280f, 720f);
        [SerializeField] private float canvasWorldScale = 0.00255f;
        [SerializeField] private Vector3 canvasLocalPosition = new Vector3(-1.05f, 1.32f, 0.38f);
        [SerializeField] private Vector3 canvasLocalEulerAngles = new Vector3(0f, -9.5f, 0f);

        [Header("Logo")]
        [SerializeField] private RawImage logoImage;
        [SerializeField] private RectTransform logoRect;
        [SerializeField] private Vector2 logoSize = new Vector2(407.8f, 280.8f);
        [SerializeField] private Vector2 logoPosition = new Vector2(-471.8f, 217.15f);
        [SerializeField] private Color logoColor = new Color(1f, 1f, 1f, 0.96f);

        [Header("Cards")]
        [SerializeField] private CardLayout combatCard = CardLayout.CreatePrimary();
        [SerializeField] private CardLayout trainingCard = CardLayout.CreateSecondary("\u8bad\u7ec3\u573a", new Vector2(-410f, -212f), new Vector2(244f, 188f), new Rect(0.5f, 0.5f, 0.5f, 0.5f));
        [SerializeField] private CardLayout tutorialCard = CardLayout.CreateSecondary("\u6559\u7a0b", new Vector2(-152f, -212f), new Vector2(244f, 188f), new Rect(0.5f, 0f, 0.5f, 0.5f));
        [SerializeField] private CardLayout settingsCard = CardLayout.CreateSecondary("\u8bbe\u7f6e", new Vector2(106f, -212f), new Vector2(244f, 188f), new Rect(0f, 0f, 0.5f, 0.5f));

        [Header("Settings Panel")]
        [SerializeField] private RectTransform settingsPanelRect;
        [SerializeField] private Vector2 settingsPanelSize = new Vector2(720f, 352f);
        [SerializeField] private Vector2 settingsPanelPosition = new Vector2(0f, -106f);
        [SerializeField] private Text settingsTitleText;
        [SerializeField] private int settingsTitleFontSize = 68;
        [SerializeField] private Text[] settingsRowTexts = Array.Empty<Text>();
        [SerializeField] private int settingsRowFontSize = 34;
        [SerializeField] private Text closeButtonText;
        [SerializeField] private int closeButtonFontSize = 30;
        [SerializeField] private Text statusText;
        [SerializeField] private int statusFontSize = 28;

        public void Configure(
            Font font,
            Texture2D logo,
            Texture2D icons,
            Texture2D combatPreview,
            Texture2D combatTitle,
            Texture2D trainingTitle,
            Texture2D tutorialTitle,
            Texture2D settingsTitleGraphic,
            RectTransform canvas,
            RawImage logoGraphic,
            RectTransform logoTransform,
            CardLayout combat,
            CardLayout training,
            CardLayout settings,
            CardLayout tutorial,
            RectTransform settingsPanel,
            Text settingsTitle,
            Text[] settingsRows,
            Text closeLabel,
            Text statusLabel)
        {
            menuFont = font;
            logoTexture = logo;
            iconTexture = icons;
            combatPreviewTexture = combatPreview;
            combatTitleTexture = combatTitle;
            trainingTitleTexture = trainingTitle;
            tutorialTitleTexture = tutorialTitle;
            settingsTitleTexture = settingsTitleGraphic;
            canvasRect = canvas;
            logoImage = logoGraphic;
            logoRect = logoTransform;
            combatCard = combat;
            trainingCard = training;
            settingsCard = settings;
            tutorialCard = tutorial;
            settingsPanelRect = settingsPanel;
            settingsTitleText = settingsTitle;
            settingsRowTexts = settingsRows ?? Array.Empty<Text>();
            closeButtonText = closeLabel;
            statusText = statusLabel;
            Apply();
        }

        private void Reset()
        {
            canvasRect = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

#if UNITY_EDITOR
        private bool editorApplyQueued;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                Apply();
                return;
            }

            if (editorApplyQueued)
            {
                return;
            }

            editorApplyQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyAfterValidate;
        }

        private void ApplyAfterValidate()
        {
            editorApplyQueued = false;
            if (this == null)
            {
                return;
            }

            Apply();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#else
        private void OnValidate()
        {
            Apply();
        }
#endif

        public void Apply()
        {
            ApplyCanvas();
            ApplyLogo();
            combatCard.Apply(menuFont, iconTexture, combatPreviewTexture, combatTitleTexture);
            trainingCard.Apply(menuFont, iconTexture, null, trainingTitleTexture);
            tutorialCard.Apply(menuFont, iconTexture, null, tutorialTitleTexture);
            settingsCard.Apply(menuFont, iconTexture, null, settingsTitleTexture);
            ApplySettingsPanel();
        }

        private void ApplyCanvas()
        {
            if (canvasRect == null)
            {
                canvasRect = GetComponent<RectTransform>();
            }

            if (canvasRect == null)
            {
                return;
            }

            canvasRect.sizeDelta = canvasSize;
            canvasRect.localScale = Vector3.one * Mathf.Max(0.0001f, canvasWorldScale);
            canvasRect.localPosition = canvasLocalPosition;
            canvasRect.localRotation = Quaternion.Euler(canvasLocalEulerAngles);
        }

        private void ApplyLogo()
        {
            if (logoImage != null)
            {
                if (logoTexture != null)
                {
                    logoImage.texture = logoTexture;
                }

                logoImage.color = logoColor;
                logoImage.raycastTarget = false;
            }

            ApplyRect(logoRect, logoSize, logoPosition);
        }

        private void ApplySettingsPanel()
        {
            ApplyRect(settingsPanelRect, settingsPanelSize, settingsPanelPosition);
            ApplyText(settingsTitleText, menuFont, settingsTitleFontSize);

            foreach (Text rowText in settingsRowTexts)
            {
                ApplyText(rowText, menuFont, settingsRowFontSize);
            }

            ApplyText(closeButtonText, menuFont, closeButtonFontSize);
            ApplyText(statusText, menuFont, statusFontSize);
        }

        private static void ApplyRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null)
            {
                return;
            }

            rect.sizeDelta = size;
            rect.anchoredPosition = position;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(rect);
            }
#endif
        }

        private static void ApplyText(Text text, Font font, int fontSize)
        {
            if (text == null)
            {
                return;
            }

            if (font != null)
            {
                text.font = font;
            }

            text.resizeTextForBestFit = false;
            text.fontSize = Mathf.Max(1, fontSize);
            text.resizeTextMaxSize = Mathf.Max(1, fontSize);
            text.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(fontSize * 0.55f));
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.localScale = Vector3.one;
            text.SetLayoutDirty();
            text.SetVerticesDirty();
            text.SetMaterialDirty();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(text);
            }
#endif
        }

        [Serializable]
        public sealed class CardLayout
        {
            [SerializeField] private string label = "\u4f5c\u6218";
            [SerializeField] private RectTransform cardRect;
            [SerializeField] private Image backgroundImage;
            [SerializeField] private Button button;
            [SerializeField] private Vector2 cardSize = new Vector2(760f, 278f);
            [SerializeField] private Vector2 cardPosition = new Vector2(-152f, 42f);
            [SerializeField] private Color backgroundColor = new Color(0.98f, 0.985f, 0.99f, 0.96f);

            [SerializeField] private bool showPreview = true;
            [SerializeField] private RawImage previewImage;
            [SerializeField] private RectTransform previewRect;
            [SerializeField] private Rect previewUv = new Rect(0f, 0f, 1f, 1f);
            [SerializeField] private Vector2 previewSize = new Vector2(448f, 248f);
            [SerializeField] private Vector2 previewPosition = new Vector2(-156f, 6f);
            [SerializeField] private Color previewColor = new Color(1f, 1f, 1f, 0.92f);

            [SerializeField] private Text labelText;
            [SerializeField] private RectTransform labelRect;
            [SerializeField] private RawImage labelImage;
            [SerializeField] private RectTransform labelImageRect;
            [SerializeField] private int labelFontSize = 96;
            [SerializeField] private Vector2 labelSize = new Vector2(235f, 116f);
            [SerializeField] private Vector2 labelPosition = new Vector2(189.5f, 70f);
            [SerializeField] private Color labelColor = new Color(0f, 0f, 0f, 1f);

            [SerializeField] private string detail = "\u5f53\u524d\u8d5b\u9053\uff1a<color=#f05a18>\u5929\u7a79\u73af\u7ebf</color>";
            [SerializeField] private Text detailText;
            [SerializeField] private RectTransform detailRect;
            [SerializeField] private int detailFontSize = 24;
            [SerializeField] private Vector2 detailSize = new Vector2(288f, 38f);
            [SerializeField] private Vector2 detailPosition = new Vector2(239f, -18f);
            [SerializeField] private Color detailColor = new Color(0.08f, 0.09f, 0.1f, 0.92f);

            [SerializeField] private string mode = "\u6807\u51c6\u7ade\u901f";
            [SerializeField] private Text modeText;
            [SerializeField] private RectTransform modeRect;
            [SerializeField] private int modeFontSize = 18;
            [SerializeField] private Vector2 modeSize = new Vector2(220f, 30f);
            [SerializeField] private Vector2 modePosition = new Vector2(205f, -56f);
            [SerializeField] private Color modeColor = new Color(0.16f, 0.18f, 0.2f, 0.66f);

            [SerializeField] private RawImage iconImage;
            [SerializeField] private RectTransform iconRect;
            [SerializeField] private Rect iconUv = new Rect(0f, 0.5f, 0.5f, 0.5f);
            [SerializeField] private Vector2 iconSize = new Vector2(250f, 250f);
            [SerializeField] private Vector2 iconPosition = new Vector2(285.8f, -39.5f);
            [SerializeField] private Color iconColor = new Color(0.72f, 0.75f, 0.78f, 0.23529412f);

            [SerializeField] private RectTransform cornerClipRect;
            [SerializeField] private RectTransform cornerRect;
            [SerializeField] private Image cornerImage;
            [SerializeField] private Vector2 cornerClipSize = new Vector2(22f, 22f);
            [SerializeField] private Vector2 cornerSize = new Vector2(30f, 30f);
            [SerializeField] private Color cornerColor = new Color(1f, 0.31f, 0.04f, 1f);

            [SerializeField] private RectTransform accentRect;
            [SerializeField] private Image accentImage;
            [SerializeField] private float accentHeight = 8f;
            [SerializeField] private float accentWidth = 310f;
            [SerializeField] private float accentOffsetX = 225f;
            [SerializeField] private Color accentColor = new Color(1f, 0.31f, 0.04f, 1f);

            public Button Button => button;

            public static CardLayout CreatePrimary()
            {
                return new CardLayout();
            }

            public static CardLayout CreateSecondary(string text, Vector2 position, Vector2 size, Rect uv)
            {
                return CreateSecondary(text, position, size, uv, new Color(1f, 0.31f, 0.04f, 1f));
            }

            public static CardLayout CreateSecondary(string text, Vector2 position, Vector2 size, Rect uv, Color accent)
            {
                return new CardLayout
                {
                    label = text,
                    cardSize = size,
                    cardPosition = position,
                    showPreview = false,
                    labelFontSize = 64,
                    labelSize = ResolveSecondaryLabelSize(text),
                    labelPosition = ResolveSecondaryLabelPosition(text),
                    detail = string.Empty,
                    mode = string.Empty,
                    iconUv = uv,
                    iconSize = new Vector2(126f, 116f),
                    iconPosition = new Vector2(58f, -16f),
                    iconColor = new Color(0.72f, 0.75f, 0.78f, 0.23529412f),
                    cornerColor = accent,
                    accentHeight = 9f,
                    accentWidth = 0f,
                    accentOffsetX = 0f,
                    accentColor = accent,
                };
            }

            private static Vector2 ResolveSecondaryLabelSize(string text)
            {
                return !string.IsNullOrEmpty(text) && text.Length > 2
                    ? new Vector2(132f, 62f)
                    : new Vector2(96f, 62f);
            }

            private static Vector2 ResolveSecondaryLabelPosition(string text)
            {
                return !string.IsNullOrEmpty(text) && text.Length > 2
                    ? new Vector2(-45f, 44f)
                    : new Vector2(-63f, 44f);
            }

            public void Bind(
                RectTransform root,
                Image background,
                Button sourceButton,
                RawImage preview,
                RectTransform previewTransform,
                Text title,
                RectTransform titleRect,
                RawImage titleImage,
                RectTransform titleImageTransform,
                Text detailLabel,
                RectTransform detailTransform,
                Text modeLabel,
                RectTransform modeTransform,
                RawImage icon,
                RectTransform iconTransform,
                RectTransform cornerClip,
                RectTransform cornerTransform,
                Image cornerGraphic,
                RectTransform accent,
                Image accentGraphic)
            {
                cardRect = root;
                backgroundImage = background;
                button = sourceButton;
                previewImage = preview;
                previewRect = previewTransform;
                labelText = title;
                labelRect = titleRect;
                labelImage = titleImage;
                labelImageRect = titleImageTransform;
                detailText = detailLabel;
                detailRect = detailTransform;
                modeText = modeLabel;
                modeRect = modeTransform;
                iconImage = icon;
                iconRect = iconTransform;
                cornerClipRect = cornerClip;
                cornerRect = cornerTransform;
                cornerImage = cornerGraphic;
                accentRect = accent;
                accentImage = accentGraphic;
            }

            public void Apply(Font font, Texture2D icons, Texture2D preview, Texture2D titleTexture)
            {
                ApplyRect(cardRect, cardSize, cardPosition);

                if (backgroundImage != null)
                {
                    backgroundImage.color = backgroundColor;
                }

                bool previewVisible = showPreview && previewImage != null && preview != null;
                if (previewImage != null)
                {
                    previewImage.gameObject.SetActive(previewVisible);
                    previewImage.texture = preview;
                    previewImage.uvRect = previewUv;
                    previewImage.color = previewColor;
                    previewImage.raycastTarget = false;
                }

                ApplyRect(previewRect, previewSize, previewPosition);

                bool imageLabelVisible = labelImage != null && titleTexture != null;
                if (labelImage != null)
                {
                    labelImage.gameObject.SetActive(imageLabelVisible);
                    labelImage.texture = titleTexture;
                    labelImage.color = labelColor;
                    labelImage.raycastTarget = false;
                }

                ApplyRect(labelImageRect, labelSize, labelPosition);

                if (labelText != null)
                {
                    labelText.gameObject.SetActive(!imageLabelVisible);
                    labelText.text = label;
                    labelText.color = labelColor;
                    labelText.raycastTarget = false;
                }

                ApplyRect(labelRect, labelSize, labelPosition);
                if (!imageLabelVisible)
                {
                    ApplyText(labelText, font, labelFontSize);
                }

                ApplyDetailText(detailText, detailRect, detail, detailFontSize, detailSize, detailPosition, detailColor, font);
                ApplyDetailText(modeText, modeRect, mode, modeFontSize, modeSize, modePosition, modeColor, font);

                if (iconImage != null)
                {
                    if (icons != null)
                    {
                        iconImage.texture = icons;
                    }

                    iconImage.uvRect = iconUv;
                    iconImage.color = iconColor;
                    iconImage.raycastTarget = false;
                }

                ApplyRect(iconRect, iconSize, iconPosition);
                ApplyCorner();

                if (accentRect != null)
                {
                    float resolvedAccentWidth = accentWidth > 0.01f ? accentWidth : cardSize.x;
                    accentRect.sizeDelta = new Vector2(resolvedAccentWidth, accentHeight);
                    accentRect.anchoredPosition = new Vector2(accentOffsetX, -cardSize.y * 0.5f + accentHeight * 0.5f);
                }

                if (accentImage != null)
                {
                    accentImage.color = accentColor;
                    accentImage.raycastTarget = false;
                }
            }

            private void ApplyCorner()
            {
                if (cornerClipRect != null)
                {
                    cornerClipRect.sizeDelta = cornerClipSize;
                    cornerClipRect.anchoredPosition = new Vector2(
                        cardSize.x * 0.5f - cornerClipSize.x * 0.5f,
                        cardSize.y * 0.5f - cornerClipSize.y * 0.5f);
                }

                if (cornerRect != null)
                {
                    cornerRect.sizeDelta = cornerSize;
                    cornerRect.anchoredPosition = new Vector2(cornerClipSize.x * 0.38f, cornerClipSize.y * 0.38f);
                    cornerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }

                if (cornerImage != null)
                {
                    cornerImage.color = cornerColor;
                    cornerImage.raycastTarget = false;
                }
            }

            private static void ApplyDetailText(
                Text text,
                RectTransform rect,
                string content,
                int fontSize,
                Vector2 size,
                Vector2 position,
                Color color,
                Font font)
            {
                if (text != null)
                {
                    text.gameObject.SetActive(!string.IsNullOrWhiteSpace(content));
                    text.text = content;
                    text.color = color;
                    text.raycastTarget = false;
                    text.supportRichText = true;
                }

                ApplyRect(rect, size, position);
                ApplyText(text, font, fontSize);
            }

        }
    }
}
