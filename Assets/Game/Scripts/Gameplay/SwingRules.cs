using UnityEngine;

namespace SpiderSwing.Gameplay
{
    public enum PlayerMovementState
    {
        Grounded,
        Airborne,
        Swinging,
        Dead,
        Landing,
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
            bool swingPermitted,
            bool alreadySwinging)
        {
            return !grounded && currentSwings > 0 && swingPermitted && !alreadySwinging;
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

        public static Vector3 CalculateProjectedAnchor(
            Vector3 startPosition,
            Vector3 direction,
            float effectiveSwingSpeed,
            float swingDuration,
            AnimationCurve verticalCurve,
            float verticalOffset)
        {
            var planarDirection = CaptureDirection(direction, Vector3.forward);
            var safeSpeed = Mathf.Max(0f, effectiveSwingSpeed);
            var safeDuration = Mathf.Max(0f, swingDuration);
            var halfSwingTime = safeDuration * 0.5f;
            var midpointCurveOffset = verticalCurve != null
                ? verticalCurve.Evaluate(0.5f)
                : 0f;

            var projectedMidpoint = startPosition
                + planarDirection * safeSpeed * halfSwingTime;
            projectedMidpoint.y = startPosition.y + midpointCurveOffset;
            return projectedMidpoint + Vector3.up * verticalOffset;
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

    public static class LandingRules
    {
        private const float VerticalStationaryTolerance = 0.0001f;

        public static bool CanStartRecovery(
            PlayerMovementState state,
            bool groundedContact,
            float requestedVerticalDelta,
            float graceRemaining)
        {
            if (state != PlayerMovementState.Airborne
                && state != PlayerMovementState.Swinging)
            {
                return false;
            }

            return groundedContact
                && graceRemaining <= 0f
                && requestedVerticalDelta <= VerticalStationaryTolerance;
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
            return state != PlayerMovementState.Dead
                && state != PlayerMovementState.Landing
                && !isRespawning
                && distance > 0.0001f;
        }
    }

    public static class WorldLimitRules
    {
        public static bool IsBeyondSideLimit(Vector3 position, float sideDeathX)
        {
            return Mathf.Abs(position.x) > Mathf.Abs(sideDeathX);
        }

        public static bool IsBelowDeathLimit(Vector3 position, float deathY)
        {
            return position.y < deathY;
        }

        public static Vector3 ClampMaximumY(Vector3 position, float maximumY)
        {
            return new Vector3(position.x, Mathf.Min(position.y, maximumY), position.z);
        }
    }

    public static class CourseLayoutRules
    {
        public static int GapForIndex(int index)
        {
            var safeIndex = Mathf.Max(0, index);
            return Mathf.RoundToInt(3f + 0.5f * safeIndex + 1.5f * safeIndex * safeIndex);
        }
    }
}
