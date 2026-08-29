using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public struct ProgressionResolution
    {
        public int level;
        public float xp;
        public int levelsGained;
    }

    public static class ProgressionRules
    {
        public static float XpFromDistance(float distance, float multiplier)
        {
            return Mathf.Max(0f, distance) * Mathf.Max(0f, multiplier);
        }

        public static float RequiredXpForLevel(int level, float baseXpToNextLevel)
        {
            return Mathf.Max(0f, baseXpToNextLevel) * Mathf.Max(1, level);
        }

        public static float MoveSpeedForLevel(
            float baseMoveSpeed,
            int level,
            float movementSpeedPerLevel,
            float maximumTravelSpeed = float.PositiveInfinity)
        {
            var levelOffset = Mathf.Max(0, level - 1);
            var uncappedSpeed = Mathf.Max(0f, baseMoveSpeed)
                + levelOffset * Mathf.Max(0f, movementSpeedPerLevel);
            return ClampTravelSpeed(uncappedSpeed, maximumTravelSpeed);
        }

        public static float ClampTravelSpeed(float speed, float maximumTravelSpeed)
        {
            return Mathf.Min(
                Mathf.Max(0f, speed),
                Mathf.Max(0f, maximumTravelSpeed));
        }

        public static float SwingMultiplierForLevel(
            float baseSwingMultiplier,
            int level,
            float swingForwardMultiplierPerLevel)
        {
            var levelOffset = Mathf.Max(0, level - 1);
            return Mathf.Max(0f, baseSwingMultiplier)
                + levelOffset * Mathf.Max(0f, swingForwardMultiplierPerLevel);
        }

        public static int MaxSwingsForLevel(
            int baseMaxSwings,
            int level,
            int extraSwingEveryLevels)
        {
            var interval = Mathf.Max(1, extraSwingEveryLevels);
            var extraSwings = Mathf.Max(1, level) / interval;
            return Mathf.Max(1, baseMaxSwings) + extraSwings;
        }

        public static int SwingsAfterMaxChange(int currentSwings, int previousMaximum, int newMaximum)
        {
            var safePreviousMaximum = Mathf.Max(1, previousMaximum);
            var safeNewMaximum = Mathf.Max(1, newMaximum);
            return currentSwings >= safePreviousMaximum
                ? safeNewMaximum
                : Mathf.Clamp(currentSwings, 0, safeNewMaximum);
        }

        public static ProgressionResolution ResolveXp(
            int currentLevel,
            float currentXp,
            float addedXp,
            float baseXpToNextLevel)
        {
            var level = Mathf.Max(1, currentLevel);
            var xp = Mathf.Max(0f, currentXp) + Mathf.Max(0f, addedXp);
            var levelsGained = 0;

            while (level < int.MaxValue)
            {
                var threshold = RequiredXpForLevel(level, baseXpToNextLevel);
                if (threshold <= 0f || xp < threshold)
                {
                    break;
                }

                xp -= threshold;
                level++;
                levelsGained++;
            }

            return new ProgressionResolution
            {
                level = level,
                xp = xp,
                levelsGained = levelsGained,
            };
        }

        // Kept as a source-compatible overload for older callers. Progression is intentionally uncapped;
        // the former maximum-level argument is ignored.
        public static ProgressionResolution ResolveXp(
            int currentLevel,
            float currentXp,
            float addedXp,
            int ignoredMaximumLevel,
            float baseXpToNextLevel)
        {
            return ResolveXp(currentLevel, currentXp, addedXp, baseXpToNextLevel);
        }
    }
}
