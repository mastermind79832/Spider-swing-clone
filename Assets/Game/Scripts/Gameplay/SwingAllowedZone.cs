using System;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    // Compatibility type for older scenes. New gameplay references
    // SwingForbiddenZone instead.
    [Obsolete("Use SwingForbiddenZone.")]
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
}
