using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SkyCircuit.Presentation
{
    [ExecuteAlways]
    public sealed class SevenSegmentScoreGraphic : MaskableGraphic
    {
        [SerializeField, Min(0)] private int value;
        [SerializeField, Range(1, 4)] private int digits = 2;
        [SerializeField, Range(0.06f, 0.28f)] private float segmentThickness = 0.15f;
        [SerializeField, Range(0f, 0.35f)] private float digitSpacing = 0.12f;
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.08f;
        [SerializeField] private bool showInactiveSegments = true;

        private const int SegmentCount = 7;
        private readonly List<Image> segmentImages = new List<Image>();
        private bool layoutDirty = true;

        private static readonly bool[,] SegmentMap =
        {
            { true,  true,  true,  true,  true,  true,  false },
            { false, true,  true,  false, false, false, false },
            { true,  true,  false, true,  true,  false, true  },
            { true,  true,  true,  true,  false, false, true  },
            { false, true,  true,  false, false, true,  true  },
            { true,  false, true,  true,  false, true,  true  },
            { true,  false, true,  true,  true,  true,  true  },
            { true,  true,  true,  false, false, false, false },
            { true,  true,  true,  true,  true,  true,  true  },
            { true,  true,  true,  true,  false, true,  true  },
        };

        public void SetValue(int score, int digitCount)
        {
            digits = Mathf.Clamp(digitCount, 1, 4);
            value = Mathf.Clamp(score, 0, MaxValueForDigits(digits));
            MarkSegmentsDirty();
            UpdateSegments();
        }

        public void SetSegmentStyle(Color segmentColor, float thickness, float offAlpha)
        {
            color = segmentColor;
            segmentThickness = Mathf.Clamp(thickness, 0.06f, 0.28f);
            inactiveAlpha = Mathf.Clamp01(offAlpha);
            MarkSegmentsDirty();
            UpdateSegments();
        }

        public void Refresh()
        {
            MarkSegmentsDirty();
            UpdateSegments();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            MarkSegmentsDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            MarkSegmentsDirty();
            UpdateSegments();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetSegmentsActive(false);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            MarkSegmentsDirty();
        }

        private void OnValidate()
        {
            MarkSegmentsDirty();
        }

        private void LateUpdate()
        {
            UpdateSegments();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }

        private void MarkSegmentsDirty()
        {
            layoutDirty = true;
            SetVerticesDirty();
        }

        private void UpdateSegments()
        {
            if (!layoutDirty || !isActiveAndEnabled)
            {
                return;
            }

            layoutDirty = false;
            Rect rect = rectTransform.rect;
            int digitCount = Mathf.Clamp(digits, 1, 4);
            EnsureSegmentImages(digitCount * SegmentCount);

            if (rect.width <= 0f || rect.height <= 0f)
            {
                SetSegmentsActive(false);
                return;
            }

            float digitWidth = rect.width / (digitCount + digitSpacing * Mathf.Max(0, digitCount - 1));
            float gap = digitWidth * digitSpacing;
            int score = Mathf.Clamp(value, 0, MaxValueForDigits(digitCount));
            string text = score.ToString().PadLeft(digitCount, '0');

            for (int digitIndex = 0; digitIndex < digitCount; digitIndex++)
            {
                var digitRect = new Rect(
                    digitIndex * (digitWidth + gap),
                    0f,
                    digitWidth,
                    rect.height);
                int digit = text[digitIndex] - '0';
                for (int segment = 0; segment < SegmentCount; segment++)
                {
                    int imageIndex = digitIndex * SegmentCount + segment;
                    Image image = segmentImages[imageIndex];
                    Color segmentColor = SegmentColor(digit, segment);
                    image.gameObject.SetActive(segmentColor.a > 0f);
                    image.color = segmentColor;
                    ConfigureSegmentRect(image.rectTransform, digitRect, segment);
                }
            }

            for (int i = digitCount * SegmentCount; i < segmentImages.Count; i++)
            {
                segmentImages[i].gameObject.SetActive(false);
            }
        }

        private void EnsureSegmentImages(int requiredCount)
        {
            segmentImages.Clear();
            var claimed = new HashSet<Transform>();

            for (int i = 0; i < requiredCount; i++)
            {
                string segmentName = SegmentName(i);
                Image image = FindSegmentImage(segmentName);
                if (image == null)
                {
                    var segmentObject = new GameObject(segmentName, typeof(RectTransform), typeof(Image));
                    segmentObject.transform.SetParent(transform, false);
                    image = segmentObject.GetComponent<Image>();
                }

                image.name = segmentName;
                image.raycastTarget = false;
                image.enabled = true;
                image.gameObject.SetActive(true);
                segmentImages.Add(image);
                claimed.Add(image.transform);
            }

            HideUnclaimedSegments(claimed);
        }

        private Image FindSegmentImage(string segmentName)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name != segmentName)
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }

        private void HideUnclaimedSegments(HashSet<Transform> claimed)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (claimed.Contains(child) || !child.name.StartsWith("Segment ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private static string SegmentName(int index)
        {
            return $"Segment {index:00}";
        }

        private void SetSegmentsActive(bool active)
        {
            for (int i = 0; i < segmentImages.Count; i++)
            {
                if (segmentImages[i] != null)
                {
                    segmentImages[i].gameObject.SetActive(active);
                }
            }
        }

        private Color SegmentColor(int digit, int segment)
        {
            bool active = digit >= 0 && digit <= 9 && SegmentMap[digit, segment];
            if (active)
            {
                return color;
            }

            if (!showInactiveSegments || inactiveAlpha <= 0f)
            {
                return new Color(0f, 0f, 0f, 0f);
            }

            Color offColor = color;
            offColor.a *= inactiveAlpha;
            return offColor;
        }

        private void ConfigureSegmentRect(RectTransform segmentRect, Rect digitRect, int segment)
        {
            float thickness = Mathf.Min(digitRect.width, digitRect.height) * segmentThickness;
            float pad = thickness * 0.42f;
            float horizontalX = digitRect.x + pad + thickness * 0.5f;
            float horizontalWidth = Mathf.Max(thickness, digitRect.width - pad * 2f - thickness);
            float leftX = digitRect.x + pad;
            float rightX = digitRect.xMax - pad - thickness;
            float topY = pad;
            float middleY = digitRect.height * 0.5f - thickness * 0.5f;
            float bottomY = digitRect.height - pad - thickness;
            float verticalHeight = Mathf.Max(thickness, digitRect.height * 0.5f - pad - thickness * 0.85f);
            float upperY = pad + thickness * 0.55f;
            float lowerY = digitRect.height * 0.5f + thickness * 0.35f;

            switch (segment)
            {
                case 0:
                    SetTopLeftRect(segmentRect, horizontalX, topY, horizontalWidth, thickness);
                    break;
                case 1:
                    SetTopLeftRect(segmentRect, rightX, upperY, thickness, verticalHeight);
                    break;
                case 2:
                    SetTopLeftRect(segmentRect, rightX, lowerY, thickness, verticalHeight);
                    break;
                case 3:
                    SetTopLeftRect(segmentRect, horizontalX, bottomY, horizontalWidth, thickness);
                    break;
                case 4:
                    SetTopLeftRect(segmentRect, leftX, lowerY, thickness, verticalHeight);
                    break;
                case 5:
                    SetTopLeftRect(segmentRect, leftX, upperY, thickness, verticalHeight);
                    break;
                case 6:
                    SetTopLeftRect(segmentRect, horizontalX, middleY, horizontalWidth, thickness);
                    break;
            }
        }

        private static void SetTopLeftRect(RectTransform rectTransform, float x, float y, float width, float height)
        {
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(x, -y);
            rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static int MaxValueForDigits(int digitCount)
        {
            int max = 0;
            for (int i = 0; i < digitCount; i++)
            {
                max = max * 10 + 9;
            }

            return max;
        }
    }
}
