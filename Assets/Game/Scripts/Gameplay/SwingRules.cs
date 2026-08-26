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
        public static float EvaluateJumpVelocity(float jumpHeight, float gravity)
        {
            var downwardGravity = Mathf.Abs(gravity);
            return Mathf.Sqrt(Mathf.Max(0f, jumpHeight * 2f * downwardGravity));
        }

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
            float slope;
            if (time <= sampleOffset)
            {
                var start = curve.Evaluate(time);
                var next = curve.Evaluate(time + sampleOffset);
                slope = (next - start) / (sampleOffset * duration);
            }
            else if (time >= 1f - sampleOffset)
            {
                var previous = curve.Evaluate(time - sampleOffset);
                var end = curve.Evaluate(time);
                slope = (end - previous) / (sampleOffset * duration);
            }
            else
            {
                var before = curve.Evaluate(time - sampleOffset);
                var after = curve.Evaluate(time + sampleOffset);
                slope = (after - before) / (2f * sampleOffset * duration);
            }

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
