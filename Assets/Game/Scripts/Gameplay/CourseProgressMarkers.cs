using System;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class PlayerCheckpointProgress : MonoBehaviour
    {
        private CoursePlatform lastReachedPlatform;

        public CoursePlatform LastReachedPlatform => lastReachedPlatform;
        public bool HasReachedCheckpoint => lastReachedPlatform != null;
        public string LastCheckpointId => lastReachedPlatform != null
            ? lastReachedPlatform.PlatformId
            : string.Empty;

        public event Action<CoursePlatform> OnPlatformReached;

        public bool Reach(CoursePlatform platform)
        {
            if (platform == null || platform.SavePoint == null)
            {
                return false;
            }

            var changed = lastReachedPlatform != platform;
            lastReachedPlatform = platform;
            if (changed)
            {
                OnPlatformReached?.Invoke(platform);
            }

            return changed;
        }

        public bool TryGetRespawn(out Vector3 position, out Quaternion rotation)
        {
            if (lastReachedPlatform == null || lastReachedPlatform.SavePoint == null)
            {
                position = default;
                rotation = Quaternion.identity;
                return false;
            }

            position = lastReachedPlatform.SavePoint.position;
            rotation = lastReachedPlatform.SavePoint.rotation;
            return true;
        }
    }

}
