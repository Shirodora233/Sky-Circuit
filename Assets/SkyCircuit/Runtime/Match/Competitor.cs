using SkyCircuit.Flight;
using UnityEngine;

namespace SkyCircuit.Match
{
    public sealed class Competitor : MonoBehaviour
    {
        [SerializeField] private string displayName = "Competitor";
        [SerializeField] private bool isPlayer;
        [SerializeField] private Transform body;
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Transform spawnPoint;

        private int score;
        private int targetIndex;

        public string DisplayName => displayName;
        public bool IsPlayer => isPlayer;
        public int Score => score;
        public int TargetIndex => targetIndex;
        public Transform Body => body != null ? body : transform;
        public SkyCircuitFlightController Controller => controller;

        public void Configure(
            string competitorName,
            bool playerControlled,
            SkyCircuitFlightController flightController,
            Transform spawn)
        {
            displayName = competitorName;
            isPlayer = playerControlled;
            controller = flightController;
            body = flightController != null ? flightController.transform : transform;
            spawnPoint = spawn;
        }

        private void Awake()
        {
            if (body == null)
            {
                body = transform;
            }

            if (controller == null)
            {
                controller = GetComponent<SkyCircuitFlightController>();
            }
        }

        public void ResetForMatch()
        {
            score = 0;
            targetIndex = 0;
            ResetToSpawn();
        }

        public void ResetToSpawn()
        {
            if (controller == null || spawnPoint == null)
            {
                return;
            }

            controller.ResetFlight(spawnPoint.position, spawnPoint.rotation);
        }

        public void AddScore(int amount)
        {
            score = Mathf.Max(0, score + amount);
        }

        public void AdvanceTarget(int buoyCount)
        {
            if (buoyCount <= 0)
            {
                targetIndex = 0;
                return;
            }

            targetIndex = (targetIndex + 1) % buoyCount;
        }
    }
}
