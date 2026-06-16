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
        [SerializeField] private Texture2D hitTexture;
        [SerializeField] private Renderer hitMarkerRenderer;
        [SerializeField] private Vector3 hitMarkerLocalPosition = Vector3.zero;
        [SerializeField] private Vector2 hitMarkerSize = new Vector2(2.35f, 2.35f);
        [SerializeField, Range(0f, 1f)] private float hitMarkerAlpha = 1f;
        [SerializeField] private float hitMarkerLifetime = 0.85f;
        [SerializeField] private float hitMarkerFadeDuration = 0.24f;
        [SerializeField] private float hitMarkerFlashScale = 1.45f;

        private float flashRemaining;
        private float markerRemaining;
        private bool available;
        private Material hitMarkerMaterial;
        private Transform hitMarkerTransform;

        public void Configure(Renderer renderer, Light light, Texture2D texture = null)
        {
            pointFieldRenderer = renderer;
            pointLight = light;
            hitTexture = texture;
            EnsureHitMarker();
            ApplyColor(lockedColor);
        }

        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
        }

        public void TriggerHit()
        {
            flashRemaining = hitFlashDuration;
            markerRemaining = Mathf.Max(hitMarkerLifetime, hitFlashDuration);
            ShowHitMarker();
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

            EnsureHitMarker();
        }

        private void Update()
        {
            markerRemaining = Mathf.Max(0f, markerRemaining - Time.deltaTime);
            FaceMarkerToCamera();

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

            ApplyHitMarker();
        }

        private void EnsureHitMarker()
        {
            if (hitTexture == null)
            {
                return;
            }

            if (hitMarkerRenderer == null)
            {
                Transform existing = transform.Find("Back Hit Triangle");
                if (existing != null)
                {
                    hitMarkerRenderer = existing.GetComponent<Renderer>();
                }
            }

            if (hitMarkerRenderer == null)
            {
                GameObject markerObject = new GameObject("Back Hit Triangle");
                markerObject.transform.SetParent(transform, false);
                markerObject.transform.localPosition = hitMarkerLocalPosition;

                MeshFilter meshFilter = markerObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = CreateQuadMesh();

                MeshRenderer meshRenderer = markerObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                hitMarkerRenderer = meshRenderer;
            }

            hitMarkerTransform = hitMarkerRenderer.transform;
            hitMarkerTransform.localPosition = hitMarkerLocalPosition;

            Shader shader = Shader.Find("Sprites/Default");
            hitMarkerMaterial = new Material(shader)
            {
                name = "Back Hit Triangle Runtime",
                mainTexture = hitTexture,
                renderQueue = 3000
            };
            hitMarkerMaterial.SetTexture("_MainTex", hitTexture);
            hitMarkerMaterial.SetTexture("_BaseMap", hitTexture);
            hitMarkerRenderer.sharedMaterial = hitMarkerMaterial;
            hitMarkerRenderer.gameObject.SetActive(false);
            ApplyHitMarker();
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh
            {
                name = "Back Hit Triangle Quad"
            };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void ApplyHitMarker()
        {
            if (hitMarkerRenderer == null || hitTexture == null)
            {
                return;
            }

            if (markerRemaining <= 0f)
            {
                hitMarkerRenderer.gameObject.SetActive(false);
                return;
            }

            hitMarkerRenderer.gameObject.SetActive(true);
            float flash01 = hitFlashDuration > 0f ? Mathf.Clamp01(flashRemaining / hitFlashDuration) : 0f;
            float fade01 = hitMarkerFadeDuration > 0f ? Mathf.Clamp01(markerRemaining / hitMarkerFadeDuration) : 1f;
            float alpha = hitMarkerAlpha * fade01;
            float scale = Mathf.Lerp(1f, Mathf.Max(1f, hitMarkerFlashScale), flash01);

            if (hitMarkerMaterial == null)
            {
                hitMarkerMaterial = Application.isPlaying ? hitMarkerRenderer.material : hitMarkerRenderer.sharedMaterial;
            }

            if (hitMarkerMaterial != null)
            {
                Color markerColor = new Color(1f, 1f, 1f, alpha);
                hitMarkerMaterial.color = markerColor;
                hitMarkerMaterial.SetColor("_Color", markerColor);
                hitMarkerMaterial.SetColor("_BaseColor", markerColor);
            }

            if (hitMarkerTransform != null)
            {
                hitMarkerTransform.localScale = new Vector3(hitMarkerSize.x * scale, hitMarkerSize.y * scale, 1f);
            }
        }

        private void ShowHitMarker()
        {
            EnsureHitMarker();
            if (hitMarkerTransform == null)
            {
                return;
            }

            Vector3 markerPosition = transform.TransformPoint(hitMarkerLocalPosition);
            hitMarkerTransform.SetParent(null, true);
            hitMarkerTransform.position = markerPosition;
            hitMarkerTransform.localScale = new Vector3(hitMarkerSize.x, hitMarkerSize.y, 1f);
            hitMarkerTransform.gameObject.SetActive(true);
            FaceMarkerToCamera();
        }

        private void FaceMarkerToCamera()
        {
            if (hitMarkerTransform == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 toCamera = hitMarkerTransform.position - camera.transform.position;
            if (toCamera.sqrMagnitude > 0.001f)
            {
                hitMarkerTransform.rotation = Quaternion.LookRotation(toCamera, camera.transform.up);
            }
        }
    }
}
