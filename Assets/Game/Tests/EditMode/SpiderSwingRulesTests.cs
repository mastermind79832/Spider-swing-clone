using NUnit.Framework;
using SpiderSwing.Gameplay;
using UnityEngine;

namespace SpiderSwing.Tests
{
    public sealed class SpiderSwingRulesTests
    {
        [Test]
        public void SwingRequiresAirbornePlayerChargeAndAllowedZone()
        {
            Assert.That(SwingRules.CanStartSwing(false, 1, true, false), Is.True);
            Assert.That(SwingRules.CanStartSwing(true, 1, true, false), Is.False);
            Assert.That(SwingRules.CanStartSwing(false, 0, true, false), Is.False);
            Assert.That(SwingRules.CanStartSwing(false, 1, false, false), Is.False);
            Assert.That(SwingRules.CanStartSwing(false, 1, true, true), Is.False);
        }

        [Test]
        public void JumpVelocitySupportsTheHighDemoJump()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            try
            {
                Assert.That(config.jumpHeight, Is.EqualTo(6f).Within(0.0001f));
                Assert.That(
                    SwingRules.EvaluateJumpVelocity(config.jumpHeight, config.gravity),
                    Is.EqualTo(Mathf.Sqrt(288f)).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CaptureDirectionIsFixedPlanarAndFallsBackToForward()
        {
            var inputDirection = SwingRules.CaptureDirection(
                new Vector3(1f, 10f, 1f),
                Vector3.forward);
            var fallbackDirection = SwingRules.CaptureDirection(
                Vector3.zero,
                Vector3.right);

            Assert.That(inputDirection, Is.EqualTo(new Vector3(1f, 0f, 1f).normalized));
            Assert.That(fallbackDirection, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void DefaultCurveAndDurationMatchMilestone()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            try
            {
                Assert.That(config.maxSwings, Is.EqualTo(2));
                Assert.That(config.swingDuration, Is.EqualTo(1.25f).Within(0.0001f));
                Assert.That(config.swingVerticalCurve.Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(config.swingVerticalCurve.Evaluate(0.5f), Is.EqualTo(-2f).Within(0.0001f));
                Assert.That(config.swingVerticalCurve.Evaluate(1f), Is.EqualTo(6f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CurveEvaluationUsesFixedDuration()
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, -2f),
                new Keyframe(1f, 6f));

            Assert.That(SwingRules.EvaluateVertical(10f, curve, 0f, 1.25f), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(SwingRules.EvaluateVertical(10f, curve, 0.625f, 1.25f), Is.EqualTo(8f).Within(0.0001f));
            Assert.That(SwingRules.EvaluateVertical(10f, curve, 1.25f, 1.25f), Is.EqualTo(16f).Within(0.0001f));
        }

        [Test]
        public void EarlyReleaseVelocityIsClamped()
        {
            var steepCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(1f, 100f));

            var releaseVelocity = SwingRules.EvaluateExitVelocity(
                steepCurve,
                0.5f,
                1.25f,
                -12f,
                12f);

            Assert.That(releaseVelocity, Is.EqualTo(12f).Within(0.0001f));
        }

        [Test]
        public void TraversalDistanceRejectsZeroDeadAndRespawnMovement()
        {
            Assert.That(TraversalDistanceRules.TryGetDistance(
                Vector3.zero,
                new Vector3(0f, 0f, 2f),
                PlayerMovementState.Airborne,
                false,
                out var livingDistance), Is.True);
            Assert.That(livingDistance, Is.EqualTo(2f).Within(0.0001f));

            Assert.That(TraversalDistanceRules.TryGetDistance(
                Vector3.zero,
                Vector3.zero,
                PlayerMovementState.Grounded,
                false,
                out _), Is.False);
            Assert.That(TraversalDistanceRules.TryGetDistance(
                Vector3.zero,
                Vector3.one,
                PlayerMovementState.Dead,
                false,
                out _), Is.False);
            Assert.That(TraversalDistanceRules.TryGetDistance(
                Vector3.zero,
                Vector3.one,
                PlayerMovementState.Grounded,
                true,
                out _), Is.False);
        }

        [Test]
        public void OnlyTopPlatformNormalsCanRefill()
        {
            Assert.That(CoursePlatform.IsTopLanding(Vector3.up), Is.True);
            Assert.That(CoursePlatform.IsTopLanding(new Vector3(0f, 0.69f, 0.72f)), Is.False);
            Assert.That(CoursePlatform.IsTopLanding(Vector3.right), Is.False);
        }
    }
}
