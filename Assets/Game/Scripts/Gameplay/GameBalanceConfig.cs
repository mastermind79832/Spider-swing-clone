using UnityEngine;

namespace SpiderSwing.Gameplay
{
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Spider Swing/Game Balance Config")]
    public sealed class GameBalanceConfig : ScriptableObject
    {
        [Header("Movement")]
        [Min(0f)] public float moveSpeed = 7f;
        [Min(0f)] public float rotationSharpness = 14f;
        [Negative] public float gravity = -24f;
        [Min(0f)] public float jumpHeight = 6f;
        [Min(0f)] public float jumpLandingGraceDuration = 0.12f;
        [Min(1f)] public float maximumTravelSpeed = 50f;

        [Header("Swing")]
        [Min(1)] public int maxSwings = 2;
        [Min(0.05f)] public float swingDuration = 1.25f;
        [Min(0f)] public float swingForwardMultiplier = 1f;
        public AnimationCurve swingVerticalCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, -2f),
            new Keyframe(1f, 6f));
        [Min(0f)] public float webAnchorHeightOffset = 20f;
        public float releaseVelocityMinimum = -12f;
        public float releaseVelocityMaximum = 12f;

        [Header("Respawn")]
        [Min(0f)] public float respawnDelay = 1f;

        [Header("World Limits")]
        [Min(0.1f)] public float sideDeathX = 45f;
        public float deathY = -20f;
        [Min(0.1f)] public float maximumY = 60f;

        [Header("Progression")]
        [Min(0f)] public float xpMultiplier = 1f;
        [Min(0f)] public float baseXpToNextLevel = 100f;
        [Min(0f)] public float movementSpeedPerLevel = 1.25f;
        [Min(0f)] public float swingForwardMultiplierPerLevel = 0.15f;
        [Min(1)] public int extraSwingEveryLevels = 2;

        [Header("Training")]
        [Min(0f)] public float treadmillXpPerSecond = 10f;

        [Header("Animation")]
        [Min(0f)] public float animationBlendDuration = 0.1f;
        [Min(0f)] public float landingRecoveryDuration = 0.4f;

        private void OnValidate()
        {
            gravity = -Mathf.Abs(gravity);
            jumpLandingGraceDuration = Mathf.Max(0f, jumpLandingGraceDuration);
            maximumTravelSpeed = Mathf.Max(1f, maximumTravelSpeed);
            maxSwings = Mathf.Max(1, maxSwings);
            swingDuration = Mathf.Max(0.05f, swingDuration);
            releaseVelocityMinimum = Mathf.Min(releaseVelocityMinimum, releaseVelocityMaximum);
            releaseVelocityMaximum = Mathf.Max(releaseVelocityMinimum, releaseVelocityMaximum);
            maximumY = Mathf.Max(0.1f, maximumY);
            deathY = Mathf.Min(deathY, maximumY - 0.1f);
            xpMultiplier = Mathf.Max(0f, xpMultiplier);
            baseXpToNextLevel = Mathf.Max(0f, baseXpToNextLevel);
            movementSpeedPerLevel = Mathf.Max(0f, movementSpeedPerLevel);
            swingForwardMultiplierPerLevel = Mathf.Max(0f, swingForwardMultiplierPerLevel);
            extraSwingEveryLevels = Mathf.Max(1, extraSwingEveryLevels);
            treadmillXpPerSecond = Mathf.Max(0f, treadmillXpPerSecond);
            animationBlendDuration = Mathf.Max(0f, animationBlendDuration);
            landingRecoveryDuration = Mathf.Max(0f, landingRecoveryDuration);
        }
    }

    // Inspector attribute kept local to this file so balance fields remain plain serialized values.
    internal sealed class NegativeAttribute : PropertyAttribute
    {
    }
}
