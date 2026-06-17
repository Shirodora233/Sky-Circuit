using UnityEngine;

namespace SkyCircuit.Menu
{
    public sealed class SkyCircuitMenuFloat : MonoBehaviour
    {
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0f, 0.05f, 0f);
        [SerializeField] private float positionFrequency = 0.2f;
        [SerializeField] private Vector3 rotationAmplitude = new Vector3(0f, 2f, 0f);
        [SerializeField] private float rotationFrequency = 0.16f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private bool hasBaseTransform;

        public void Configure(
            Vector3 bobAmplitude,
            float bobFrequency,
            Vector3 swayAmplitude,
            float swayFrequency)
        {
            positionAmplitude = bobAmplitude;
            positionFrequency = bobFrequency;
            rotationAmplitude = swayAmplitude;
            rotationFrequency = swayFrequency;
            CacheBaseTransform();
        }

        private void OnEnable()
        {
            CacheBaseTransform();
        }

        private void Update()
        {
            if (!hasBaseTransform)
            {
                CacheBaseTransform();
            }

            float time = Time.time;
            float bob = Mathf.Sin(time * Mathf.PI * 2f * positionFrequency);
            float sway = Mathf.Sin((time + 0.37f) * Mathf.PI * 2f * rotationFrequency);

            transform.localPosition = baseLocalPosition + positionAmplitude * bob;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(rotationAmplitude * sway);
        }

        private void CacheBaseTransform()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            hasBaseTransform = true;
        }
    }
}
