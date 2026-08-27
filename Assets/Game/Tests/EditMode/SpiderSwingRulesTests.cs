using NUnit.Framework;
using SpiderSwing.Gameplay;
using UnityEngine;

namespace SpiderSwing.Tests
{
    public sealed class SpiderSwingRulesTests
    {
        [Test]
        public void DistanceXpHonorsTheConfiguredMultiplier()
        {
            Assert.That(ProgressionRules.XpFromDistance(12.5f, 1f), Is.EqualTo(12.5f).Within(0.0001f));
            Assert.That(ProgressionRules.XpFromDistance(12.5f, 2f), Is.EqualTo(25f).Within(0.0001f));
            Assert.That(ProgressionRules.XpFromDistance(-1f, 2f), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void RequiredXpUsesTheCurrentLevelMultiplier()
        {
            Assert.That(ProgressionRules.RequiredXpForLevel(1, 100f), Is.EqualTo(100f));
            Assert.That(ProgressionRules.RequiredXpForLevel(5, 100f), Is.EqualTo(500f));
        }

        [Test]
        public void XpOverflowCarriesAcrossMultipleLevels()
        {
            var result = ProgressionRules.ResolveXp(1, 0f, 350f, 10, 100f);

            Assert.That(result.level, Is.EqualTo(3));
            Assert.That(result.xp, Is.EqualTo(50f).Within(0.0001f));
            Assert.That(result.levelsGained, Is.EqualTo(2));
        }

        [Test]
        public void MaximumLevelStopsFurtherXpProgression()
        {
            var result = ProgressionRules.ResolveXp(10, 25f, 500f, 10, 100f);

            Assert.That(result.level, Is.EqualTo(10));
            Assert.That(result.xp, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.levelsGained, Is.EqualTo(0));
        }

        [Test]
        public void ProgressionStatsMatchTheLockedFastDemoCurve()
        {
            Assert.That(ProgressionRules.MoveSpeedForLevel(7f, 1, 0.75f), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(ProgressionRules.MoveSpeedForLevel(7f, 5, 0.75f), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(ProgressionRules.MoveSpeedForLevel(7f, 10, 0.75f), Is.EqualTo(13.75f).Within(0.0001f));

            Assert.That(ProgressionRules.SwingMultiplierForLevel(1f, 1, 0.15f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(ProgressionRules.SwingMultiplierForLevel(1f, 5, 0.15f), Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(ProgressionRules.SwingMultiplierForLevel(1f, 10, 0.15f), Is.EqualTo(2.35f).Within(0.0001f));

            Assert.That(ProgressionRules.MaxSwingsForLevel(2, 1, 2), Is.EqualTo(2));
            Assert.That(ProgressionRules.MaxSwingsForLevel(2, 5, 2), Is.EqualTo(4));
            Assert.That(ProgressionRules.MaxSwingsForLevel(2, 10, 2), Is.EqualTo(7));
        }

        [Test]
        public void CapacityChangesPreservePartialSwingStockButExpandFullStock()
        {
            Assert.That(ProgressionRules.SwingsAfterMaxChange(2, 2, 3), Is.EqualTo(3));
            Assert.That(ProgressionRules.SwingsAfterMaxChange(1, 2, 3), Is.EqualTo(1));
            Assert.That(ProgressionRules.SwingsAfterMaxChange(0, 2, 5), Is.EqualTo(0));
        }

        [Test]
        public void DemoRewardsSpendOnlyWhenThePlayerCanAffordTheCost()
        {
            var rewardsObject = new GameObject("RewardsTest");
            try
            {
                var rewards = rewardsObject.AddComponent<PlayerDemoRewards>();
                rewards.AwardReturn(5, Vector3.zero);

                Assert.That(rewards.CanAfford(5), Is.True);
                Assert.That(rewards.TrySpend(5), Is.True);
                Assert.That(rewards.ReturnPoints, Is.EqualTo(0));
                Assert.That(rewards.TrySpend(1), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(rewardsObject);
            }
        }

        [Test]
        public void SwingRequiresAirbornePlayerChargeAndPermittedLocation()
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

        [Test]
        public void ForbiddenHubContainsOnlyItsColliderAndMissingZoneFailsOpen()
        {
            var zoneObject = new GameObject("SwingForbiddenZone");
            try
            {
                var collider = zoneObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(10f, 4f, 10f);
                var zone = zoneObject.AddComponent<SwingForbiddenZone>();
                zone.Configure(collider);

                Assert.That(zone.Contains(Vector3.zero), Is.True);
                Assert.That(zone.Contains(new Vector3(6f, 0f, 0f)), Is.False);
                Assert.That(SwingRules.CanStartSwing(false, 1, true, false), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(zoneObject);
            }
        }

        [Test]
        public void WorldLimitsKillAtSidesAndBelowButClampAbove()
        {
            Assert.That(WorldLimitRules.IsBeyondSideLimit(new Vector3(45.01f, 0f, 0f), 45f), Is.True);
            Assert.That(WorldLimitRules.IsBeyondSideLimit(new Vector3(-45.01f, 0f, 0f), 45f), Is.True);
            Assert.That(WorldLimitRules.IsBelowDeathLimit(new Vector3(0f, -20.01f, 0f), -20f), Is.True);
            Assert.That(WorldLimitRules.IsBelowDeathLimit(new Vector3(0f, -20f, 0f), -20f), Is.False);
            Assert.That(
                WorldLimitRules.ClampMaximumY(new Vector3(3f, 61f, 7f), 60f),
                Is.EqualTo(new Vector3(3f, 60f, 7f)));
        }

        [Test]
        public void ProgressiveCourseGapsIncreaseAndMatchFormula()
        {
            var expected = new[] { 3, 5, 10, 18, 29 };
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(CourseLayoutRules.GapForIndex(index), Is.EqualTo(expected[index]));
                if (index > 0)
                {
                    Assert.That(
                        CourseLayoutRules.GapForIndex(index),
                        Is.GreaterThan(CourseLayoutRules.GapForIndex(index - 1)));
                }
            }
        }

        [Test]
        public void PlatformProgressKeepsLatestSavePointAndReward()
        {
            var playerObject = new GameObject("CheckpointTestPlayer");
            var firstObject = new GameObject("P01");
            var secondObject = new GameObject("P02");
            var firstSave = new GameObject("Save point");
            var secondSave = new GameObject("Save point");
            try
            {
                var progress = playerObject.AddComponent<PlayerCheckpointProgress>();
                var first = firstObject.AddComponent<CoursePlatform>();
                var second = secondObject.AddComponent<CoursePlatform>();
                first.Configure("P01", firstSave.transform, null, 1);
                second.Configure("P02", secondSave.transform, null, 2);
                firstSave.transform.position = new Vector3(0f, 1f, 3f);
                secondSave.transform.position = new Vector3(0f, 1f, 10f);

                Assert.That(progress.HasReachedCheckpoint, Is.False);
                Assert.That(progress.Reach(first), Is.True);
                Assert.That(progress.LastCheckpointId, Is.EqualTo("P01"));
                Assert.That(progress.Reach(first), Is.False);
                Assert.That(progress.Reach(second), Is.True);
                Assert.That(progress.LastCheckpointId, Is.EqualTo("P02"));
                Assert.That(progress.TryGetRespawn(out var position, out _), Is.True);
                Assert.That(position, Is.EqualTo(secondSave.transform.position));
                Assert.That(second.ReturnReward, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(firstSave);
                Object.DestroyImmediate(secondSave);
            }
        }

        [Test]
        public void PlatformRewardsMapToTheirOneBasedIndex()
        {
            for (var index = 1; index <= 20; index++)
            {
                var platformObject = new GameObject($"P{index:00}");
                try
                {
                    var platform = platformObject.AddComponent<CoursePlatform>();
                    platform.Configure($"P{index:00}", platformObject.transform, null, index);
                    Assert.That(platform.ReturnReward, Is.EqualTo(index));
                }
                finally
                {
                    Object.DestroyImmediate(platformObject);
                }
            }
        }
    }
}
