using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class SwingForbiddenZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider zoneCollider;

        public void Configure(BoxCollider configuredCollider)
        {
            zoneCollider = configuredCollider;
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<BoxCollider>();
            }

            return zoneCollider != null && zoneCollider.bounds.Contains(worldPosition);
        }
    }
}
