using SkyCircuit.Combat;
using SkyCircuit.Match;
using UnityEngine;

namespace SkyCircuit.Presentation
{
    public sealed class MatchPlayerIndicator : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private MatchController match;
        [SerializeField] private DogfightController dogfight;

        [Header("Threat Rules")]
        [SerializeField] private bool requireDogfightUnlocked = true;
        [SerializeField] private float threatRange = 55f;
        [SerializeField] private float fullIntensityDistance = 16f;
        [SerializeField] private float criticalThreatRange = 14f;
        [SerializeField] private float criticalBehindDotThreshold = -0.35f;
        [SerializeField] private float criticalFacingDotThreshold = 0.2f;

        [Header("Edge Selection")]
        [SerializeField] private float onScreenInset = 0.04f;
        [SerializeField] private float rearCenterRatio = 0.38f;
        [SerializeField] private float edgeHoldTime = 0.25f;
        [SerializeField] private float fadeSharpness = 10f;

        [Header("Visuals")]
        [SerializeField] private float edgeThickness = 120f;
        [SerializeField] private float sideBandHeight = 0.72f;
        [SerializeField] private float bottomBandWidth = 0.78f;
        [SerializeField] private int edgeBands = 12;
        [SerializeField] private float edgeFalloffPower = 1.8f;
        [SerializeField] private float minAlpha = 0.1f;
        [SerializeField] private float maxAlpha = 0.5f;
        [SerializeField] private float pulseStrength = 0.12f;
        [SerializeField] private float pulseSpeed = 5f;
        [SerializeField] private Color warningColor = new Color(1f, 0.48f, 0.12f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.08f, 0.04f, 1f);

        private ThreatEdge currentEdge;
        private float edgeLockRemaining;
        private float currentIntensity;
        private bool currentCritical;

        public void Configure(Camera camera, MatchController matchController, DogfightController dogfightController)
        {
            targetCamera = camera;
            match = matchController;
            dogfight = dogfightController;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            edgeLockRemaining = Mathf.Max(0f, edgeLockRemaining - dt);

            ThreatEdge candidate = ResolveThreatEdge(out float targetIntensity, out bool critical);
            if (candidate == ThreatEdge.None)
            {
                currentIntensity = Mathf.Lerp(currentIntensity, 0f, DampBlend(fadeSharpness, dt));
                if (currentIntensity <= 0.01f)
                {
                    currentEdge = ThreatEdge.None;
                    currentCritical = false;
                }

                return;
            }

            if (currentEdge == ThreatEdge.None || (candidate != currentEdge && edgeLockRemaining <= 0f))
            {
                currentEdge = candidate;
                edgeLockRemaining = edgeHoldTime;
            }

            currentCritical = critical;
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, DampBlend(fadeSharpness, dt));
        }

        private void OnGUI()
        {
            if (currentEdge == ThreatEdge.None || currentIntensity <= 0.01f)
            {
                return;
            }

            Color color = currentCritical ? dangerColor : warningColor;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseStrength;
            DrawEdgeHalo(currentEdge, color, Mathf.Clamp01(currentIntensity * pulse));
        }

        private ThreatEdge ResolveThreatEdge(out float intensity, out bool critical)
        {
            intensity = 0f;
            critical = false;

            if (match == null || match.Phase != MatchPhase.Running)
            {
                return ThreatEdge.None;
            }

            if (requireDogfightUnlocked && !(dogfight != null ? dogfight.IsUnlocked : match.DogfightUnlocked))
            {
                return ThreatEdge.None;
            }

            Competitor player = match.Player;
            Competitor opponent = match.Opponent;
            if (player == null || opponent == null || player.Body == null || opponent.Body == null)
            {
                return ThreatEdge.None;
            }

            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            if (camera == null)
            {
                return ThreatEdge.None;
            }

            Vector3 playerPosition = player.Body.position;
            Vector3 opponentPosition = opponent.Body.position;
            float distance = Vector3.Distance(playerPosition, opponentPosition);
            if (distance > threatRange)
            {
                return ThreatEdge.None;
            }

            if (IsOnScreen(camera.WorldToViewportPoint(opponentPosition)))
            {
                return ThreatEdge.None;
            }

            float rangeSpan = Mathf.Max(0.01f, threatRange - fullIntensityDistance);
            intensity = Mathf.Lerp(minAlpha, maxAlpha, Mathf.Clamp01((threatRange - distance) / rangeSpan));
            critical = IsCriticalThreat(player, opponent, distance);

            return SelectThreatEdge(camera, opponentPosition);
        }

        private bool IsOnScreen(Vector3 viewport)
        {
            return viewport.z > 0f
                && viewport.x > onScreenInset
                && viewport.x < 1f - onScreenInset
                && viewport.y > onScreenInset
                && viewport.y < 1f - onScreenInset;
        }

        private ThreatEdge SelectThreatEdge(Camera camera, Vector3 opponentPosition)
        {
            Vector3 local = camera.transform.InverseTransformPoint(opponentPosition);
            if (local.z < 0f)
            {
                float lateral = local.x / Mathf.Max(0.01f, -local.z);
                if (Mathf.Abs(lateral) <= rearCenterRatio)
                {
                    return ThreatEdge.Bottom;
                }

                return lateral < 0f ? ThreatEdge.Left : ThreatEdge.Right;
            }

            Vector3 viewport = camera.WorldToViewportPoint(opponentPosition);
            if (viewport.y < 0f)
            {
                return ThreatEdge.Bottom;
            }

            if (viewport.x < 0.5f)
            {
                return ThreatEdge.Left;
            }

            return ThreatEdge.Right;
        }

        private bool IsCriticalThreat(Competitor player, Competitor opponent, float distance)
        {
            if (distance > criticalThreatRange)
            {
                return false;
            }

            Vector3 playerToOpponent = opponent.Body.position - player.Body.position;
            if (playerToOpponent.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            Vector3 playerToOpponentDirection = playerToOpponent.normalized;
            float behindDot = Vector3.Dot(player.Body.forward, playerToOpponentDirection);
            if (behindDot > criticalBehindDotThreshold)
            {
                return false;
            }

            float opponentFacingDot = Vector3.Dot(opponent.Body.forward, -playerToOpponentDirection);
            return opponentFacingDot >= criticalFacingDotThreshold;
        }

        private void DrawEdgeHalo(ThreatEdge edge, Color color, float intensity)
        {
            Color previousColor = GUI.color;
            int bands = Mathf.Max(1, edgeBands);
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, intensity);

            for (int i = 0; i < bands; i++)
            {
                float band01 = i / Mathf.Max(1f, bands - 1f);
                float bandAlpha = alpha * Mathf.Pow(1f - band01, edgeFalloffPower);
                GUI.color = new Color(color.r, color.g, color.b, bandAlpha);
                GUI.DrawTexture(EdgeBandRect(edge, i, bands), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
        }

        private Rect EdgeBandRect(ThreatEdge edge, int index, int bands)
        {
            float slice = edgeThickness / bands;
            switch (edge)
            {
                case ThreatEdge.Left:
                    return new Rect(
                        index * slice,
                        Screen.height * (1f - sideBandHeight) * 0.5f,
                        slice + 1f,
                        Screen.height * sideBandHeight);
                case ThreatEdge.Right:
                    return new Rect(
                        Screen.width - (index + 1f) * slice,
                        Screen.height * (1f - sideBandHeight) * 0.5f,
                        slice + 1f,
                        Screen.height * sideBandHeight);
                case ThreatEdge.Bottom:
                    return new Rect(
                        Screen.width * (1f - bottomBandWidth) * 0.5f,
                        Screen.height - (index + 1f) * slice,
                        Screen.width * bottomBandWidth,
                        slice + 1f);
                default:
                    return Rect.zero;
            }
        }

        private static float DampBlend(float sharpness, float dt)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, sharpness) * dt);
        }

        private enum ThreatEdge
        {
            None,
            Left,
            Right,
            Bottom
        }
    }
}
