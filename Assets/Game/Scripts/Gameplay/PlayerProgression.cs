using System;
using UnityEngine;

namespace SpiderSwing.Gameplay
{
    [RequireComponent(typeof(LocalPlayerController))]
    public sealed class PlayerProgression : MonoBehaviour
    {
        [SerializeField] private GameBalanceConfig balanceConfig;
        [SerializeField] private LocalPlayerController playerController;

        private int level = 1;
        private float currentXp;
        private float upgradeXpMultiplier = 1f;
        private int upgradeSwingBonus;
        private bool subscribed;

        public int Level => level;
        public float CurrentXp => currentXp;
        public int MaximumLevel => GetBalanceValue(value => value.maximumLevel, 10);
        public float BaseXpMultiplier => GetBalanceValue(value => value.xpMultiplier, 1f);
        public float UpgradeXpMultiplier => upgradeXpMultiplier;
        public float XpMultiplier => BaseXpMultiplier * upgradeXpMultiplier;
        public int UpgradeSwingBonus => upgradeSwingBonus;
        public bool IsAtMaximumLevel => level >= MaximumLevel;
        public float XpToNextLevel => IsAtMaximumLevel
            ? 0f
            : ProgressionRules.RequiredXpForLevel(
                level,
                GetBalanceValue(value => value.baseXpToNextLevel, 100f));
        public float CurrentMoveSpeed => ProgressionRules.MoveSpeedForLevel(
            GetBalanceValue(value => value.moveSpeed, playerController != null ? playerController.BaseMoveSpeed : 7f),
            level,
            GetBalanceValue(value => value.movementSpeedPerLevel, 0.75f));
        public float CurrentSwingForwardMultiplier => ProgressionRules.SwingMultiplierForLevel(
            GetBalanceValue(value => value.swingForwardMultiplier, playerController != null
                ? playerController.BaseSwingForwardMultiplier
                : 1f),
            level,
            GetBalanceValue(value => value.swingForwardMultiplierPerLevel, 0.15f));
        public int CurrentMaxSwings => ProgressionRules.MaxSwingsForLevel(
            GetBalanceValue(value => value.maxSwings, playerController != null
                ? playerController.BaseMaxSwings
                : 2),
            level,
            GetBalanceValue(value => value.extraSwingEveryLevels, 2)) + upgradeSwingBonus;

        public event Action<int> OnLevelChanged;
        public event Action<float, float> OnXpChanged;

        public void Configure(
            GameBalanceConfig configuredBalance,
            LocalPlayerController configuredPlayerController = null)
        {
            balanceConfig = configuredBalance;
            playerController = configuredPlayerController != null
                ? configuredPlayerController
                : GetComponent<LocalPlayerController>();
            playerController?.ConfigureProgression(this);
            ApplyCurrentStats();
        }

        public void AddTraversalDistance(float distance)
        {
            AddRawXp(ProgressionRules.XpFromDistance(distance, 1f));
        }

        public void AddTrainingXp(float rawXp)
        {
            AddRawXp(rawXp);
        }

        public void AddRawXp(float rawXp)
        {
            if (IsAtMaximumLevel || rawXp <= 0f)
            {
                return;
            }

            var addedXp = rawXp * XpMultiplier;
            if (addedXp <= 0f)
            {
                return;
            }

            var previousLevel = level;
            var resolution = ProgressionRules.ResolveXp(
                level,
                currentXp,
                addedXp,
                MaximumLevel,
                GetBalanceValue(value => value.baseXpToNextLevel, 100f));
            level = resolution.level;
            currentXp = resolution.xp;

            ApplyCurrentStats();
            if (level != previousLevel)
            {
                OnLevelChanged?.Invoke(level);
            }

            OnXpChanged?.Invoke(currentXp, XpToNextLevel);
        }

        public void ResetForNewSession()
        {
            level = 1;
            currentXp = 0f;
            upgradeXpMultiplier = 1f;
            upgradeSwingBonus = 0;
            ApplyCurrentStats();
            OnLevelChanged?.Invoke(level);
            OnXpChanged?.Invoke(currentXp, XpToNextLevel);
        }

        public bool ApplyUpgrade(string upgradeId, float xpMultiplier, int extraSwings)
        {
            if (string.IsNullOrWhiteSpace(upgradeId)
                || xpMultiplier < 1f
                || extraSwings < 0)
            {
                return false;
            }

            upgradeXpMultiplier *= xpMultiplier;
            upgradeSwingBonus += extraSwings;
            ApplyCurrentStats();
            OnXpChanged?.Invoke(currentXp, XpToNextLevel);
            return true;
        }

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<LocalPlayerController>();
            }

            playerController?.ConfigureProgression(this);

            ApplyCurrentStats();
        }

        private void OnEnable()
        {
            if (playerController == null)
            {
                playerController = GetComponent<LocalPlayerController>();
            }

            if (!subscribed && playerController != null)
            {
                playerController.OnTraversalDistanceMoved += AddTraversalDistance;
                subscribed = true;
            }

            ApplyCurrentStats();
        }

        private void OnDisable()
        {
            if (subscribed && playerController != null)
            {
                playerController.OnTraversalDistanceMoved -= AddTraversalDistance;
                subscribed = false;
            }
        }

        private void ApplyCurrentStats()
        {
            if (playerController == null)
            {
                return;
            }

            playerController.ApplyProgressionStats(
                CurrentMoveSpeed,
                CurrentSwingForwardMultiplier,
                CurrentMaxSwings);
        }

        private float GetBalanceValue(
            Func<GameBalanceConfig, float> selector,
            float fallback)
        {
            return balanceConfig != null ? selector(balanceConfig) : fallback;
        }

        private int GetBalanceValue(
            Func<GameBalanceConfig, int> selector,
            int fallback)
        {
            return balanceConfig != null ? selector(balanceConfig) : fallback;
        }
    }
}
