using SkyCircuit.Match;
using UnityEngine;

namespace SkyCircuit.Presentation
{
    public sealed class MatchWorldIndicator : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MatchController match;
        [SerializeField] private BuoyRoute route;

        [Header("Screen Placement")]
        [SerializeField] private float screenMargin = 48f;
        [SerializeField] private float viewportInsetX = 0.06f;
        [SerializeField] private float viewportInsetY = 0.1f;
        [SerializeField] private float worldLabelOffset = 5f;
        [SerializeField] private float behindDeadZone = 0.22f;
        [SerializeField] private float behindEdgeAngle = 85f;
        [SerializeField] private float behindEasePower = 2f;

        [Header("Range")]
        [SerializeField] private float touchPromptMultiplier = 1.35f;

        [Header("Colors")]
        [SerializeField] private Color targetColor = new Color(0.35f, 0.9f, 1f, 1f);
        [SerializeField] private Color touchColor = new Color(1f, 0.84f, 0.35f, 1f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.85f);

        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle arrowStyle;

        public void Configure(Camera camera, MatchController matchController, BuoyRoute buoyRoute)
        {
            targetCamera = camera;
            match = matchController;
            route = buoyRoute;
        }

        private void Awake()
        {
            labelStyle = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = targetColor }
            };

            smallStyle = new GUIStyle(labelStyle)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            arrowStyle = new GUIStyle(labelStyle)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (labelStyle == null)
            {
                Awake();
            }

            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            Competitor player = match != null ? match.Player : null;
            Transform target = route != null ? route.GetTarget(player) : null;
            if (camera == null || player == null || player.Body == null || target == null)
            {
                return;
            }

            float distance = Vector3.Distance(player.Body.position, target.position);
            bool touchReady = distance <= route.TouchRadius * touchPromptMultiplier;
            Color color = touchReady ? touchColor : targetColor;
            string distanceText = touchReady ? "TOUCH" : $"{distance:0}m";
            int buoyNumber = player.TargetIndex + 1;

            Vector3 labelWorldPosition = target.position + Vector3.up * worldLabelOffset;
            Vector3 viewport = camera.WorldToViewportPoint(labelWorldPosition);
            if (IsOnScreen(viewport))
            {
                Vector2 screenPosition = new Vector2(
                    viewport.x * Screen.width,
                    (1f - viewport.y) * Screen.height);
                DrawOnScreenLabel(screenPosition, buoyNumber, distanceText, color);
                return;
            }

            DrawOffScreenIndicator(camera, labelWorldPosition, buoyNumber, distance, color);
        }

        private bool IsOnScreen(Vector3 viewport)
        {
            return viewport.z > 0f
                && viewport.x > viewportInsetX
                && viewport.x < 1f - viewportInsetX
                && viewport.y > viewportInsetY
                && viewport.y < 1f - viewportInsetY;
        }

        private void DrawOnScreenLabel(Vector2 screenPosition, int buoyNumber, string distanceText, Color color)
        {
            var labelRect = new Rect(screenPosition.x - 55f, screenPosition.y - 44f, 110f, 42f);
            DrawShadowLabel(labelRect, $"BUOY {buoyNumber}\n{distanceText}", labelStyle, color);

            var markerRect = new Rect(screenPosition.x - 12f, screenPosition.y - 7f, 24f, 18f);
            DrawShadowLabel(markerRect, "[]", smallStyle, color);
        }

        private void DrawOffScreenIndicator(Camera camera, Vector3 targetPosition, int buoyNumber, float distance, Color color)
        {
            Vector3 localTarget = camera.transform.InverseTransformPoint(targetPosition);
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 edgePosition;
            Vector2 direction;

            if (localTarget.z < 0f)
            {
                edgePosition = ProjectBehindTargetToBottomEdge(localTarget);
                direction = (edgePosition - center).normalized;
            }
            else
            {
                direction = CalculateOffScreenDirection(localTarget);
                edgePosition = ProjectToSafeEdge(center, direction);
            }

            DrawArrow(edgePosition, direction, color);

            var labelRect = new Rect(edgePosition.x - 42f, edgePosition.y + 15f, 84f, 38f);
            labelRect.x = Mathf.Clamp(labelRect.x, screenMargin, Screen.width - screenMargin - labelRect.width);
            labelRect.y = Mathf.Clamp(labelRect.y, screenMargin, Screen.height - screenMargin - labelRect.height);
            DrawShadowLabel(labelRect, $"B{buoyNumber}\n{distance:0}m", smallStyle, color);
        }

        private Vector2 ProjectBehindTargetToBottomEdge(Vector3 localTarget)
        {
            float angleFromBehind = Mathf.Atan2(localTarget.x, -localTarget.z) * Mathf.Rad2Deg;
            float deadZoneAngle = Mathf.Atan(Mathf.Max(0f, behindDeadZone)) * Mathf.Rad2Deg;
            float edgeAngle = Mathf.Clamp(behindEdgeAngle, deadZoneAngle + 1f, 89f);
            float lateral01 = Mathf.InverseLerp(deadZoneAngle, edgeAngle, Mathf.Abs(angleFromBehind));
            lateral01 = Mathf.Pow(lateral01, Mathf.Max(1f, behindEasePower));

            float signedLateral = Mathf.Sign(angleFromBehind) * lateral01;
            float x = Mathf.Lerp(screenMargin, Screen.width - screenMargin, (signedLateral + 1f) * 0.5f);

            return new Vector2(x, Screen.height - screenMargin);
        }

        private Vector2 CalculateOffScreenDirection(Vector3 localTarget)
        {
            Vector2 direction = new Vector2(localTarget.x, -localTarget.y);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector2.down;
            }

            return direction.normalized;
        }

        private Vector2 ProjectToSafeEdge(Vector2 center, Vector2 direction)
        {
            float minX = screenMargin;
            float maxX = Screen.width - screenMargin;
            float minY = screenMargin;
            float maxY = Screen.height - screenMargin;

            float scaleX = direction.x > 0f
                ? (maxX - center.x) / Mathf.Max(0.0001f, direction.x)
                : (minX - center.x) / Mathf.Min(-0.0001f, direction.x);
            float scaleY = direction.y > 0f
                ? (maxY - center.y) / Mathf.Max(0.0001f, direction.y)
                : (minY - center.y) / Mathf.Min(-0.0001f, direction.y);

            float scale = Mathf.Min(Mathf.Abs(scaleX), Mathf.Abs(scaleY));
            return center + direction * scale;
        }

        private void DrawArrow(Vector2 position, Vector2 direction, Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, position);
            DrawShadowLabel(new Rect(position.x - 15f, position.y - 18f, 30f, 36f), ">", arrowStyle, color);
            GUI.matrix = previousMatrix;
        }

        private void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previousColor = style.normal.textColor;
            style.normal.textColor = shadowColor;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = previousColor;
        }
    }
}
