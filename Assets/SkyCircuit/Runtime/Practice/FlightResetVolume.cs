using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Practice
{
    public sealed class FlightResetVolume : MonoBehaviour
    {
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float minAltitude = -3f;
        [SerializeField] private float maxDistanceFromOrigin = 190f;

        public void Configure(SkyCircuitFlightController flightController, Transform spawn)
        {
            controller = flightController;
            spawnPoint = spawn;
        }

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<SkyCircuitFlightController>();
            }
        }

        private void Update()
        {
            if (controller == null || spawnPoint == null)
            {
                return;
            }

            Vector3 position = controller.transform.position;
            Vector2 horizontal = new Vector2(position.x, position.z);
            if (position.y < minAltitude || horizontal.magnitude > maxDistanceFromOrigin)
            {
                controller.ResetFlight(spawnPoint.position, spawnPoint.rotation);
            }
        }
    }
}
