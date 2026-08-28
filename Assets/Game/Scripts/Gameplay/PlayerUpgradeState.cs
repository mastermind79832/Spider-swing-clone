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
        private PlayerSkinVisual playerSkinVisual;
        private string currentSkinId = "Default";

        public event Action<UpgradePad> OnUpgradePurchased;
        public event Action<string> OnSkinChanged;

        public string CurrentSkinId => currentSkinId;

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
            playerSkinVisual ??= GetComponent<PlayerSkinVisual>() ?? gameObject.AddComponent<PlayerSkinVisual>();
            if (progression == null
                || demoRewards == null
                || !pad.TryGetSkinMaterials(out var armMaterial, out var bodyMaterial)
                || !demoRewards.CanAfford(pad.Cost))
            {
                return false;
            }

            if (!progression.ApplyUpgrade(pad.UpgradeId, pad.XpMultiplier, pad.ExtraSwings))
            {
                return false;
            }

            if (!demoRewards.TrySpend(pad.Cost))
            {
                return false;
            }

            purchasedUpgradeIds.Add(pad.UpgradeId);
            playerSkinVisual.Apply(armMaterial, bodyMaterial);
            currentSkinId = pad.UpgradeId;
            pad.MarkPurchased();
            OnUpgradePurchased?.Invoke(pad);
            OnSkinChanged?.Invoke(currentSkinId);
            return true;
        }

        public void ResetForNewSession()
        {
            purchasedUpgradeIds.Clear();
            ResolveReferences();
            progression?.ResetForNewSession();
            currentSkinId = "Default";
            playerSkinVisual?.ResetToDefault();
            OnSkinChanged?.Invoke(currentSkinId);
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
            playerSkinVisual ??= GetComponent<PlayerSkinVisual>();
        }
    }
}
