using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class SwingAllowedZone : MonoBehaviour
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

    public sealed class CoursePlatform : MonoBehaviour
    {
        [SerializeField] private string platformId = "P01";

        public string PlatformId => platformId;

        public void Configure(string id)
        {
            platformId = string.IsNullOrWhiteSpace(id) ? "Platform" : id;
        }

        public static bool IsTopLanding(Vector3 normal)
        {
            return normal.y >= 0.7f;
        }
    }

    public sealed class DeathSurface : MonoBehaviour
    {
    }

    public sealed class CourseBounds : MonoBehaviour
    {
        [SerializeField] private BoxCollider boundsCollider;

        public void Configure(BoxCollider configuredCollider)
        {
            boundsCollider = configuredCollider;
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (boundsCollider == null)
            {
                boundsCollider = GetComponent<BoxCollider>();
            }

            return boundsCollider != null && boundsCollider.bounds.Contains(worldPosition);
        }
    }
}
