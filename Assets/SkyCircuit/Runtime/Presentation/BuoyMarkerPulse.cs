using UnityEngine;

namespace SkyCircuit.Presentation
{
    public sealed class BuoyMarkerPulse : MonoBehaviour
    {
        [SerializeField] private float rotateSpeed = 35f;
        [SerializeField] private float pulseAmplitude = 0.08f;
        [SerializeField] private float pulseSpeed = 2.5f;

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
            float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            transform.localScale = baseScale * scale;
        }
    }
}
