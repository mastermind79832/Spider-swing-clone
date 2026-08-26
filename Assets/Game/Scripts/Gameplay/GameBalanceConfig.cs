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

        [Header("Swing")]
        [Min(1)] public int maxSwings = 2;
        [Min(0.05f)] public float swingDuration = 1.25f;
        [Min(0f)] public float swingForwardMultiplier = 1f;
        public AnimationCurve swingVerticalCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, -2f),
            new Keyframe(1f, 6f));
        [Min(0f)] public float webAnchorForwardOffset = 6f;
        [Min(0f)] public float webAnchorHeightOffset = 10f;
        public float releaseVelocityMinimum = -12f;
        public float releaseVelocityMaximum = 12f;

        [Header("Respawn")]
        [Min(0f)] public float respawnDelay = 1f;

        private void OnValidate()
        {
            gravity = -Mathf.Abs(gravity);
            maxSwings = Mathf.Max(1, maxSwings);
            swingDuration = Mathf.Max(0.05f, swingDuration);
            releaseVelocityMinimum = Mathf.Min(releaseVelocityMinimum, releaseVelocityMaximum);
            releaseVelocityMaximum = Mathf.Max(releaseVelocityMinimum, releaseVelocityMaximum);
        }
    }

    // Inspector attribute kept local to this file so balance fields remain plain serialized values.
    internal sealed class NegativeAttribute : PropertyAttribute
    {
    }
}
