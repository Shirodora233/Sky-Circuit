using SkyCircuit.Flight;
using SkyCircuit.Profiles;
using UnityEngine;

namespace SkyCircuit.Match
{
    public sealed class Competitor : MonoBehaviour
    {
        [SerializeField] private string displayName = "Competitor";
        [SerializeField] private bool isPlayer;
        [SerializeField] private CompetitorProfile profile;
        [SerializeField] private Transform body;
        [SerializeField] private SkyCircuitFlightController controller;
        [SerializeField] private Transform spawnPoint;

        private int score;
        private int buoyScoreCount;
        private int backHitScoreCount;
        private int targetIndex;

        public string DisplayName => displayName;
        public bool IsPlayer => isPlayer;
        public int Score => score;
        public int BuoyScoreCount => buoyScoreCount;
        public int BackHitScoreCount => backHitScoreCount;
        public int TargetIndex => targetIndex;
        public CompetitorProfile Profile => profile;
        public string ProfileName => profile != null ? profile.DisplayName : "Prototype";
        public Transform Body => body != null ? body : transform;
        public SkyCircuitFlightController Controller => controller;

        public void Configure(
            string competitorName,
            bool playerControlled,
            SkyCircuitFlightController flightController,
            Transform spawn,
            CompetitorProfile initialProfile = null)
        {
            displayName = competitorName;
            isPlayer = playerControlled;
            controller = flightController;
            body = flightController != null ? flightController.transform : transform;
            spawnPoint = spawn;
            SetProfile(initialProfile, true);
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

            SetProfile(profile, true);
        }

        public void ResetForMatch()
        {
            score = 0;
            buoyScoreCount = 0;
            backHitScoreCount = 0;
            targetIndex = 0;
            ResetToSpawn();
            controller?.ResetDashSkill();
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

        public void AddBuoyScore(int amount)
        {
            buoyScoreCount = Mathf.Max(0, buoyScoreCount + amount);
            AddScore(amount);
        }

        public void AddBackHitScore(int amount)
        {
            backHitScoreCount = Mathf.Max(0, backHitScoreCount + amount);
            AddScore(amount);
        }

        public void SetProfile(CompetitorProfile newProfile, bool resetSpeed)
        {
            if (newProfile == null)
            {
                return;
            }

            profile = newProfile;
            controller?.ApplyProfile(profile, resetSpeed);
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
