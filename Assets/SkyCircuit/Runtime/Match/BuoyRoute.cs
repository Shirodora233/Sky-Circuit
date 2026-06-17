using UnityEngine;

namespace SkyCircuit.Match
{
    public sealed class BuoyRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] buoys;
        [SerializeField] private float touchRadius = 6f;
        [SerializeField] private int scorePerBuoy = 1;
        [SerializeField] private Material idleMaterial;
        [SerializeField] private Material targetMaterial;
        [SerializeField] private Material completedMaterial;
        [SerializeField] private LineRenderer routeLine;

        public int BuoyCount => buoys == null ? 0 : buoys.Length;
        public float TouchRadius => touchRadius;

        public void Configure(
            Transform[] routeBuoys,
            LineRenderer line,
            Material idle,
            Material target,
            Material completed)
        {
            buoys = routeBuoys;
            routeLine = line;
            idleMaterial = idle;
            targetMaterial = target;
            completedMaterial = completed;
            RefreshRouteLine();
        }

        private void Start()
        {
            RefreshRouteLine();
        }

        public Transform GetTarget(Competitor competitor)
        {
            if (competitor == null || buoys == null || buoys.Length == 0)
            {
                return null;
            }

            return GetTarget(competitor.TargetIndex);
        }

        public Transform GetTarget(int targetIndex)
        {
            if (buoys == null || buoys.Length == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(targetIndex, 0, buoys.Length - 1);
            return buoys[index];
        }

        public bool TryScore(Competitor competitor)
        {
            Transform target = GetTarget(competitor);
            if (target == null || competitor.Body == null)
            {
                return false;
            }

            float distance = Vector3.Distance(competitor.Body.position, target.position);
            if (distance > touchRadius)
            {
                return false;
            }

            competitor.AddBuoyScore(scorePerBuoy);
            competitor.AdvanceTarget(BuoyCount);
            if (competitor.IsPlayer)
            {
                RefreshPlayerTargetVisual(competitor);
            }

            return true;
        }

        public void RefreshPlayerTargetVisual(Competitor player)
        {
            if (player == null)
            {
                RefreshTargetVisual(0, false);
                return;
            }

            RefreshTargetVisual(player.TargetIndex, player.BuoyScoreCount > 0);
        }

        public void RefreshTargetVisual(int targetIndex, bool hasCompletedAny)
        {
            if (buoys == null)
            {
                return;
            }

            for (int i = 0; i < buoys.Length; i++)
            {
                SetBuoyMaterial(buoys[i], idleMaterial);
            }

            if (buoys.Length == 0)
            {
                return;
            }

            int target = Mathf.Clamp(targetIndex, 0, buoys.Length - 1);
            int previous = (target - 1 + buoys.Length) % buoys.Length;
            if (hasCompletedAny)
            {
                SetBuoyMaterial(buoys[previous], completedMaterial);
            }

            SetBuoyMaterial(buoys[target], targetMaterial);
        }

        private void RefreshRouteLine()
        {
            if (routeLine == null || buoys == null || buoys.Length == 0)
            {
                return;
            }

            routeLine.positionCount = buoys.Length + 1;
            for (int i = 0; i < buoys.Length; i++)
            {
                routeLine.SetPosition(i, buoys[i].position);
            }

            routeLine.SetPosition(buoys.Length, buoys[0].position);
        }

        private static void SetBuoyMaterial(Transform buoy, Material material)
        {
            if (buoy == null || material == null)
            {
                return;
            }

            var renderer = buoy.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
