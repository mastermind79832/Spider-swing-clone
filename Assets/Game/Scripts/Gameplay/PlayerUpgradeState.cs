using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    [RequireComponent(typeof(PlayerProgression))]
    [RequireComponent(typeof(PlayerDemoRewards))]
    public sealed class PlayerUpgradeState : MonoBehaviour
    {
        private readonly HashSet<string> purchasedUpgradeIds = new HashSet<string>();

        private PlayerProgression progression;
        private PlayerDemoRewards demoRewards;
        private LocalPlayerController playerController;

        public event Action<UpgradePad> OnUpgradePurchased;

        public void Configure(
            PlayerProgression configuredProgression,
            PlayerDemoRewards configuredRewards,
            LocalPlayerController configuredPlayerController)
        {
            progression = configuredProgression != null
                ? configuredProgression
                : GetComponent<PlayerProgression>();
            demoRewards = configuredRewards != null
                ? configuredRewards
                : GetComponent<PlayerDemoRewards>();
            playerController = configuredPlayerController != null
                ? configuredPlayerController
                : GetComponent<LocalPlayerController>();
        }

        public bool HasPurchased(string upgradeId)
        {
            return !string.IsNullOrWhiteSpace(upgradeId)
                && purchasedUpgradeIds.Contains(upgradeId);
        }

        public bool TryPurchase(UpgradePad pad)
        {
            if (pad == null || HasPurchased(pad.UpgradeId))
            {
                return false;
            }

            ResolveReferences();
            if (progression == null
                || demoRewards == null
                || !demoRewards.TrySpend(pad.Cost))
            {
                return false;
            }

            if (!progression.ApplyUpgrade(pad.UpgradeId, pad.XpMultiplier, pad.ExtraSwings))
            {
                return false;
            }

            purchasedUpgradeIds.Add(pad.UpgradeId);
            playerController?.ApplyPlayerSkinMaterial(pad.PlayerSkinMaterial);
            pad.MarkPurchased();
            OnUpgradePurchased?.Invoke(pad);
            return true;
        }

        public void ResetForNewSession()
        {
            purchasedUpgradeIds.Clear();
            ResolveReferences();
            progression?.ResetForNewSession();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            progression ??= GetComponent<PlayerProgression>();
            demoRewards ??= GetComponent<PlayerDemoRewards>();
            playerController ??= GetComponent<LocalPlayerController>();
        }
    }
}
