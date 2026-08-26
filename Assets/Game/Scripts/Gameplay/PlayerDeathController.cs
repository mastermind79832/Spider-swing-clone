using System;
using System.Collections;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public enum PlayerDeathReason
    {
        BottomFloor,
        OutOfBounds,
    }

    [RequireComponent(typeof(LocalPlayerController))]
    public sealed class PlayerDeathController : MonoBehaviour
    {
        [SerializeField] private GameBalanceConfig balanceConfig;
        [SerializeField] private CourseBounds courseBounds;
        [SerializeField] private Transform hubSpawnPoint;

        private LocalPlayerController playerController;
        private CharacterController characterController;
        private bool isRespawning;

        public bool IsRespawning => isRespawning;
        public event Action<PlayerDeathReason> OnPlayerDied;
        public event Action OnPlayerRespawned;

        public void Configure(
            GameBalanceConfig config,
            CourseBounds configuredBounds,
            Transform configuredHubSpawn)
        {
            balanceConfig = config;
            courseBounds = configuredBounds;
            hubSpawnPoint = configuredHubSpawn;
        }

        private void Awake()
        {
            playerController = GetComponent<LocalPlayerController>();
            characterController = GetComponent<CharacterController>();
        }

        public void CheckPosition(Vector3 position)
        {
            if (!isRespawning && courseBounds != null && !courseBounds.Contains(position))
            {
                TryDie(PlayerDeathReason.OutOfBounds);
            }
        }

        public bool TryDie(PlayerDeathReason reason)
        {
            if (isRespawning || playerController == null || playerController.State == PlayerMovementState.Dead)
            {
                return false;
            }

            isRespawning = true;
            playerController.EnterDeadState();
            OnPlayerDied?.Invoke(reason);
            StartCoroutine(RespawnAfterDelay());
            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            var delay = balanceConfig != null ? balanceConfig.respawnDelay : 1f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

            if (hubSpawnPoint != null)
            {
                characterController.enabled = false;
                transform.SetPositionAndRotation(hubSpawnPoint.position, hubSpawnPoint.rotation);
                characterController.enabled = true;
            }

            playerController.ResetAfterRespawn();
            isRespawning = false;
            OnPlayerRespawned?.Invoke();
        }
    }
}
