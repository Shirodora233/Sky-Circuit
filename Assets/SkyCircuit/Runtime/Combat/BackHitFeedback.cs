using UnityEngine;

namespace SkyCircuit.Combat
{
    public sealed class BackHitFeedback : MonoBehaviour
    {
        [SerializeField] private Renderer pointFieldRenderer;
        [SerializeField] private Light pointLight;
        [SerializeField] private Color lockedColor = new Color(0.2f, 0.45f, 1f, 0.22f);
        [SerializeField] private Color availableColor = new Color(1f, 0.88f, 0.18f, 0.48f);
        [SerializeField] private Color hitColor = new Color(1f, 0.18f, 0.12f, 0.72f);
        [SerializeField] private float hitFlashDuration = 0.35f;

        private float flashRemaining;
        private bool available;

        public void Configure(Renderer renderer, Light light)
        {
            pointFieldRenderer = renderer;
            pointLight = light;
            ApplyColor(lockedColor);
        }

        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
        }

        public void TriggerHit()
        {
            flashRemaining = hitFlashDuration;
            ApplyColor(hitColor);
        }

        private void Awake()
        {
            if (pointFieldRenderer == null)
            {
                pointFieldRenderer = GetComponentInChildren<Renderer>();
            }

            if (pointLight == null)
            {
                pointLight = GetComponentInChildren<Light>();
            }
        }

        private void Update()
        {
            if (flashRemaining > 0f)
            {
                flashRemaining -= Time.deltaTime;
                ApplyColor(hitColor);
                return;
            }

            ApplyColor(available ? availableColor : lockedColor);
        }

        private void ApplyColor(Color color)
        {
            if (pointFieldRenderer != null)
            {
                Material material = Application.isPlaying ? pointFieldRenderer.material : pointFieldRenderer.sharedMaterial;
                if (material != null)
                {
                    material.color = color;
                    material.SetColor("_BaseColor", color);
                    material.SetColor("_EmissionColor", color);
                }
            }

            if (pointLight != null)
            {
                pointLight.color = color;
                pointLight.intensity = available || flashRemaining > 0f ? 2.2f : 0.7f;
            }
        }
    }
}
