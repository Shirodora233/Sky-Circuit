using UnityEngine;

namespace SkyCircuit.Practice
{
    public sealed class FlightPracticeRoute : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform[] buoys;
        [SerializeField] private float touchRadius = 6f;
        [SerializeField] private Material idleMaterial;
        [SerializeField] private Material targetMaterial;
        [SerializeField] private Material completedMaterial;
        [SerializeField] private LineRenderer routeLine;

        private int targetIndex;
        private int gatesTouched;

        public int TargetIndex => targetIndex;
        public int GatesTouched => gatesTouched;
        public int BuoyCount => buoys == null ? 0 : buoys.Length;

        public void Configure(Transform routePlayer, Transform[] routeBuoys, LineRenderer line, Material idle, Material target, Material completed)
        {
            player = routePlayer;
            buoys = routeBuoys;
            routeLine = line;
            idleMaterial = idle;
            targetMaterial = target;
            completedMaterial = completed;
            RefreshVisuals();
        }

        private void Start()
        {
            RefreshVisuals();
        }

        private void Update()
        {
            if (player == null || buoys == null || buoys.Length == 0 || buoys[targetIndex] == null)
            {
                return;
            }

            float distance = Vector3.Distance(player.position, buoys[targetIndex].position);
            if (distance > touchRadius)
            {
                return;
            }

            gatesTouched++;
            targetIndex = (targetIndex + 1) % buoys.Length;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (buoys == null)
            {
                return;
            }

            for (int i = 0; i < buoys.Length; i++)
            {
                SetBuoyMaterial(buoys[i], i == targetIndex ? targetMaterial : idleMaterial);
            }

            if (buoys.Length > 0)
            {
                int previous = (targetIndex - 1 + buoys.Length) % buoys.Length;
                SetBuoyMaterial(buoys[previous], completedMaterial);
            }

            RefreshRouteLine();
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

            var renderer = buoy.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (buoys == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            foreach (Transform buoy in buoys)
            {
                if (buoy != null)
                {
                    Gizmos.DrawWireSphere(buoy.position, touchRadius);
                }
            }
        }
    }
}
