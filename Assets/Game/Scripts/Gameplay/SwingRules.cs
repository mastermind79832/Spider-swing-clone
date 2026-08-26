using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public enum PlayerMovementState
    {
        Grounded,
        Airborne,
        Swinging,
        Dead,
    }

    public static class SwingRules
    {
        public static bool CanStartSwing(
            bool grounded,
            int currentSwings,
            bool insideSwingZone,
            bool alreadySwinging)
        {
            return !grounded && currentSwings > 0 && insideSwingZone && !alreadySwinging;
        }

        public static Vector3 CaptureDirection(Vector3 movementDirection, Vector3 forward)
        {
            var planarMovement = Vector3.ProjectOnPlane(movementDirection, Vector3.up);
            if (planarMovement.sqrMagnitude > 0.0001f)
            {
                return planarMovement.normalized;
            }

            var planarForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return planarForward.sqrMagnitude > 0.0001f
                ? planarForward.normalized
                : Vector3.forward;
        }

        public static float EvaluateVertical(
            float startY,
            AnimationCurve curve,
            float elapsed,
            float duration)
        {
            var normalizedTime = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            return startY + curve.Evaluate(normalizedTime);
        }

        public static float EvaluateExitVelocity(
            AnimationCurve curve,
            float normalizedTime,
            float duration,
            float minimum,
            float maximum)
        {
            if (curve == null || duration <= 0f)
            {
                return 0f;
            }

            var time = Mathf.Clamp01(normalizedTime);
            const float sampleOffset = 0.001f;
            var before = curve.Evaluate(Mathf.Clamp01(time - sampleOffset));
            var after = curve.Evaluate(Mathf.Clamp01(time + sampleOffset));
            var slope = (after - before) / (2f * sampleOffset * duration);
            return Mathf.Clamp(slope, minimum, maximum);
        }
    }

    public static class TraversalDistanceRules
    {
        public static bool TryGetDistance(
            Vector3 previousPosition,
            Vector3 currentPosition,
            PlayerMovementState state,
            bool isRespawning,
            out float distance)
        {
            distance = Vector3.Distance(previousPosition, currentPosition);
            return state != PlayerMovementState.Dead && !isRespawning && distance > 0.0001f;
        }
    }
}
