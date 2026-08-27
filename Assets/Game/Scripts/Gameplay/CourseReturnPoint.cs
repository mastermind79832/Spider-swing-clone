using System;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public sealed class CourseReturnPoint : MonoBehaviour
    {
        [SerializeField] private CoursePlatform platform;
        [SerializeField] private int rewardValue = 1;
        private bool activationLocked;

        public CoursePlatform Platform => platform;
        public int RewardValue => rewardValue;
        public event Action<int, Vector3> OnReturnRewardAwarded;

        public void Configure(CoursePlatform configuredPlatform, int configuredRewardValue)
        {
            platform = configuredPlatform;
            rewardValue = Mathf.Max(0, configuredRewardValue);
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponent<LocalPlayerController>();
            if (player == null)
            {
                player = other.GetComponentInParent<LocalPlayerController>();
            }

            TryActivate(player);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<LocalPlayerController>() != null
                || other.GetComponentInParent<LocalPlayerController>() != null)
            {
                activationLocked = false;
            }
        }

        public bool TryActivate(LocalPlayerController player)
        {
            if (activationLocked || player == null || player.IsDead)
            {
                return false;
            }

            var deathController = player.GetComponent<PlayerDeathController>();
            if (deathController == null || !deathController.TryReturnToStart())
            {
                return false;
            }

            activationLocked = true;
            var rewardPosition = transform.position;
            player.GetComponent<PlayerDemoRewards>()?.AwardReturn(rewardValue, rewardPosition);
            FloatingRewardText.Show(rewardValue, rewardPosition);
            OnReturnRewardAwarded?.Invoke(rewardValue, rewardPosition);
            return true;
        }
    }
}
