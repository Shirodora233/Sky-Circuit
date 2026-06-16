using UnityEngine;

namespace SkyCircuit.Presentation
{
    [ExecuteAlways]
    public sealed class CloudSeaMaterialScroller : MonoBehaviour
    {
        private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Vector2 primarySpeed = new Vector2(0.006f, 0.002f);
        [SerializeField] private Vector2 detailSpeed = new Vector2(-0.002f, 0.004f);

        private MaterialPropertyBlock propertyBlock;

        public void Configure(Renderer renderer, Vector2 primary, Vector2 detail)
        {
            targetRenderer = renderer;
            primarySpeed = primary;
            detailSpeed = detail;
            ApplyOffset();
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            ApplyOffset();
        }

        private void Update()
        {
            ApplyOffset();
        }

        private void ApplyOffset()
        {
            if (targetRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);

            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            Vector4 offset = new Vector4(
                Mathf.Repeat(primarySpeed.x * time, 1f),
                Mathf.Repeat(primarySpeed.y * time, 1f),
                Mathf.Repeat(detailSpeed.x * time, 1f),
                Mathf.Repeat(detailSpeed.y * time, 1f));

            propertyBlock.SetVector(ScrollOffsetId, offset);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
