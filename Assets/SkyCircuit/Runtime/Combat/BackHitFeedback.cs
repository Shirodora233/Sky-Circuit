using UnityEngine;

namespace SkyCircuit.Combat
{
    [DefaultExecutionOrder(10920)]
    public sealed class BackHitFeedback : MonoBehaviour
    {
        public static Vector3 DefaultBackAnchorLocalPosition => new Vector3(0f, 1.18f, -0.95f);
        public static Vector3 DefaultBackAnchorLocalScale => new Vector3(1.55f, 1.05f, 0.32f);

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
        [SerializeField] private bool followCharacterBack = true;
        [SerializeField, Min(0f)] private float backSurfaceOffset = 0.2f;
        [SerializeField, Range(0f, 1f)] private float backHeightBlend = 0.55f;

        private static readonly Vector3[] CandidateBackAxes =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        private float flashRemaining;
        private float markerRemaining;
        private bool available;
        private Material hitMarkerMaterial;
        private Transform hitMarkerTransform;
        private Transform ownerRoot;
        private Animator characterAnimator;
        private Transform lowerBackBone;
        private Transform upperBackBone;
        private Transform backDirectionBone;
        private Vector3 backNormalLocalAxis = Vector3.back;
        private bool backNormalAxisResolved;

        public void ResetToDefaultBackAnchor()
        {
            transform.localPosition = DefaultBackAnchorLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = DefaultBackAnchorLocalScale;

            if (hitMarkerRenderer != null && hitMarkerRenderer.transform.parent == transform)
            {
                hitMarkerRenderer.transform.localPosition = hitMarkerLocalPosition;
            }
        }

        public void Configure(Renderer renderer, Light light, Texture2D texture = null)
        {
            UpgradeLegacyLowAnchor();
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
            UpgradeLegacyLowAnchor();

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

        private void UpgradeLegacyLowAnchor()
        {
            Vector3 localPosition = transform.localPosition;
            if (localPosition.y < 0.8f && localPosition.z < -0.5f)
            {
                ResetToDefaultBackAnchor();
            }
        }

        private void Update()
        {
            markerRemaining = Mathf.Max(0f, markerRemaining - Time.deltaTime);

            if (flashRemaining > 0f)
            {
                flashRemaining -= Time.deltaTime;
                ApplyColor(hitColor);
                return;
            }

            ApplyColor(available ? availableColor : lockedColor);
        }

        private void LateUpdate()
        {
            FollowCharacterBack();
            FaceMarkerToCamera();
        }

        private void FollowCharacterBack()
        {
            if (!followCharacterBack || !TryResolveBackAnchor(out Vector3 position, out Quaternion rotation))
            {
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private bool TryResolveBackAnchor(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
            ResolveBackReferences();

            if (backDirectionBone == null)
            {
                return false;
            }

            if (!backNormalAxisResolved)
            {
                ResolveBackNormalAxis();
            }

            Vector3 backNormal = backDirectionBone.TransformDirection(backNormalLocalAxis);
            if (backNormal.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            backNormal.Normalize();
            Vector3 lowerPosition = lowerBackBone != null ? lowerBackBone.position : backDirectionBone.position;
            Vector3 upperPosition = upperBackBone != null ? upperBackBone.position : backDirectionBone.position;
            Vector3 anchorPosition = Vector3.Lerp(lowerPosition, upperPosition, backHeightBlend);
            Vector3 anchorUp = ResolveBackAnchorUp(backNormal, lowerPosition, upperPosition);

            position = anchorPosition + backNormal * backSurfaceOffset;
            rotation = Quaternion.LookRotation(backNormal, anchorUp);
            return true;
        }

        private void ResolveBackReferences()
        {
            Transform currentRoot = transform.parent != null ? transform.parent : transform.root;
            if (ownerRoot != currentRoot)
            {
                ownerRoot = currentRoot;
                characterAnimator = null;
                lowerBackBone = null;
                upperBackBone = null;
                backDirectionBone = null;
                backNormalAxisResolved = false;
            }

            if (characterAnimator == null && ownerRoot != null)
            {
                characterAnimator = ownerRoot.GetComponentInChildren<Animator>();
                backNormalAxisResolved = false;
            }

            if (characterAnimator == null || !characterAnimator.isHuman)
            {
                return;
            }

            lowerBackBone = characterAnimator.GetBoneTransform(HumanBodyBones.Spine)
                ?? characterAnimator.GetBoneTransform(HumanBodyBones.Chest)
                ?? characterAnimator.GetBoneTransform(HumanBodyBones.Hips);
            upperBackBone = characterAnimator.GetBoneTransform(HumanBodyBones.UpperChest)
                ?? characterAnimator.GetBoneTransform(HumanBodyBones.Chest)
                ?? lowerBackBone;
            backDirectionBone = upperBackBone ?? lowerBackBone;
        }

        private void ResolveBackNormalAxis()
        {
            if (backDirectionBone == null)
            {
                return;
            }

            Vector3 referenceBack = ownerRoot != null ? -ownerRoot.forward : -transform.forward;
            if (referenceBack.sqrMagnitude <= 0.0001f)
            {
                referenceBack = -transform.forward;
            }

            referenceBack.Normalize();
            float bestDot = float.NegativeInfinity;
            Vector3 bestAxis = Vector3.back;
            foreach (Vector3 candidate in CandidateBackAxes)
            {
                Vector3 candidateWorld = backDirectionBone.TransformDirection(candidate);
                if (candidateWorld.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                float dot = Vector3.Dot(candidateWorld.normalized, referenceBack);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestAxis = candidate;
                }
            }

            backNormalLocalAxis = bestAxis;
            backNormalAxisResolved = true;
        }

        private Vector3 ResolveBackAnchorUp(Vector3 backNormal, Vector3 lowerPosition, Vector3 upperPosition)
        {
            Vector3 spineUp = upperPosition - lowerPosition;
            Vector3 projectedUp = Vector3.ProjectOnPlane(spineUp, backNormal);
            if (projectedUp.sqrMagnitude > 0.0001f)
            {
                return projectedUp.normalized;
            }

            if (backDirectionBone != null)
            {
                projectedUp = Vector3.ProjectOnPlane(backDirectionBone.up, backNormal);
                if (projectedUp.sqrMagnitude > 0.0001f)
                {
                    return projectedUp.normalized;
                }
            }

            Vector3 fallbackUp = ownerRoot != null ? ownerRoot.up : Vector3.up;
            projectedUp = Vector3.ProjectOnPlane(fallbackUp, backNormal);
            return projectedUp.sqrMagnitude > 0.0001f ? projectedUp.normalized : Vector3.up;
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
