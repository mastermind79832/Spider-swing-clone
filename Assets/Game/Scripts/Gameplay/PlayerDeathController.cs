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
        [SerializeField] private Transform hubSpawnPoint;
        [SerializeField] private PlayerCheckpointProgress checkpointProgress;

        private LocalPlayerController playerController;
        private CharacterController characterController;
        private bool isRespawning;
        private bool reviveAtLastSafePoint;

        public bool IsRespawning => isRespawning;
        public bool HasCheckpointRevive => checkpointProgress != null && checkpointProgress.HasReachedCheckpoint;
        [Obsolete("Use HasCheckpointRevive.")]
        public bool HasSafePointRevive => HasCheckpointRevive;
        public event Action<PlayerDeathReason> OnPlayerDied;
        public event Action OnPlayerRespawned;
        public event Action OnReviveChoiceAvailable;
        public event Action OnReturnedToStart;

        public void Configure(
            GameBalanceConfig config,
            Transform configuredHubSpawn)
        {
            balanceConfig = config;
            hubSpawnPoint = configuredHubSpawn;
            checkpointProgress = GetComponent<PlayerCheckpointProgress>();
        }

        private void Awake()
        {
            playerController = GetComponent<LocalPlayerController>();
            characterController = GetComponent<CharacterController>();
            checkpointProgress = GetComponent<PlayerCheckpointProgress>();
        }

        public void CheckPosition(Vector3 position)
        {
            if (isRespawning)
            {
                return;
            }

            var sideDeathX = balanceConfig != null ? balanceConfig.sideDeathX : 45f;
            var deathY = balanceConfig != null ? balanceConfig.deathY : -20f;
            if (WorldLimitRules.IsBeyondSideLimit(position, sideDeathX))
            {
                TryDie(PlayerDeathReason.OutOfBounds);
            }
            else if (WorldLimitRules.IsBelowDeathLimit(position, deathY))
            {
                TryDie(PlayerDeathReason.BottomFloor);
            }
        }

        public bool TryDie(PlayerDeathReason reason)
        {
            if (isRespawning || playerController == null || playerController.State == PlayerMovementState.Dead)
            {
                return false;
            }

            isRespawning = true;
            reviveAtLastSafePoint = false;
            playerController.EnterDeadState();
            OnPlayerDied?.Invoke(reason);
            if (HasCheckpointRevive)
            {
                OnReviveChoiceAvailable?.Invoke();
            }
            StartCoroutine(RespawnAfterDelay());
            return true;
        }

        public bool ChooseLastSafePointRevive()
        {
            if (!isRespawning || !HasCheckpointRevive)
            {
                return false;
            }

            reviveAtLastSafePoint = true;
            return true;
        }

        public bool ChooseLastPlatformRevive()
        {
            return ChooseLastSafePointRevive();
        }

        public bool ChooseHubRevive()
        {
            if (!isRespawning)
            {
                return false;
            }

            reviveAtLastSafePoint = false;
            return true;
        }

        public bool TryReturnToStart()
        {
            if (isRespawning
                || playerController == null
                || playerController.IsDead
                || hubSpawnPoint == null
                || characterController == null)
            {
                return false;
            }

            isRespawning = true;
            characterController.enabled = false;
            transform.SetPositionAndRotation(hubSpawnPoint.position, hubSpawnPoint.rotation);
            characterController.enabled = true;
            playerController.ResetAfterRespawn();
            isRespawning = false;
            OnReturnedToStart?.Invoke();
            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            var delay = balanceConfig != null ? balanceConfig.respawnDelay : 1f;
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));

            var respawnPosition = hubSpawnPoint != null ? hubSpawnPoint.position : transform.position;
            var respawnRotation = hubSpawnPoint != null ? hubSpawnPoint.rotation : transform.rotation;
            if (reviveAtLastSafePoint
                && checkpointProgress != null
                && checkpointProgress.TryGetRespawn(out var checkpointPosition, out var checkpointRotation))
            {
                respawnPosition = checkpointPosition;
                respawnRotation = checkpointRotation;
            }

            characterController.enabled = false;
            transform.SetPositionAndRotation(respawnPosition, respawnRotation);
            characterController.enabled = true;

            playerController.ResetAfterRespawn();
            isRespawning = false;
            OnPlayerRespawned?.Invoke();
        }

        private void OnGUI()
        {
            if (!isRespawning || !HasCheckpointRevive)
            {
                return;
            }

            GUILayout.BeginArea(
                new Rect(Screen.width * 0.5f - 175f, Screen.height * 0.5f - 85f, 350f, 170f),
                GUI.skin.box);
            GUILayout.Label($"You reached {checkpointProgress.LastCheckpointId}");
            GUILayout.Label("Revive at your last platform or return to the hub.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Last Platform"))
            {
                ChooseLastPlatformRevive();
            }

            if (GUILayout.Button("Hub"))
            {
                ChooseHubRevive();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
