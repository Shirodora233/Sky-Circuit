using UnityEngine;

namespace SkyCircuit.Menu
{
    [RequireComponent(typeof(Animator))]
    public sealed class SkyCircuitMenuCharacterLookAtCamera : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float lookAtWeight = 0.88f;
        [SerializeField] private float bodyWeight = 0.18f;
        [SerializeField] private float headWeight = 0.82f;
        [SerializeField] private float eyesWeight = 0.42f;
        [SerializeField] private float clampWeight = 0.55f;

        private Animator animator;

        public void Configure(Camera camera)
        {
            targetCamera = camera;
            ResolveAnimator();
        }

        private void Awake()
        {
            ResolveAnimator();
        }

        private void OnEnable()
        {
            ResolveAnimator();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || targetCamera == null || !animator.isHuman)
            {
                return;
            }

            animator.SetLookAtWeight(lookAtWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
            animator.SetLookAtPosition(targetCamera.transform.position);
        }

        private void ResolveAnimator()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }
    }
}
