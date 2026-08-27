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
            float movementSpeedPerLevel)
        {
            var levelOffset = Mathf.Max(0, level - 1);
            return Mathf.Max(0f, baseMoveSpeed)
                + levelOffset * Mathf.Max(0f, movementSpeedPerLevel);
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

        public static ProgressionResolution ResolveXp(
            int currentLevel,
            float currentXp,
            float addedXp,
            int maximumLevel,
            float baseXpToNextLevel)
        {
            var maxLevel = Mathf.Max(1, maximumLevel);
            var level = Mathf.Clamp(currentLevel, 1, maxLevel);
            var xp = Mathf.Max(0f, currentXp) + Mathf.Max(0f, addedXp);
            var levelsGained = 0;

            while (level < maxLevel)
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

            if (level >= maxLevel)
            {
                level = maxLevel;
                xp = 0f;
            }

            return new ProgressionResolution
            {
                level = level,
                xp = xp,
                levelsGained = levelsGained,
            };
        }
    }
}
