using NUnit.Framework;
using SpiderSwing.Gameplay;
using UnityEngine;

namespace SpiderSwing.Tests
{
    public sealed class PlayerAnimationControllerTests
    {
        [Test]
        public void AnimationStateValuesRemainStable()
        {
            Assert.That((int)PlayerAnimationState.Idle, Is.EqualTo(0));
            Assert.That((int)PlayerAnimationState.Walk, Is.EqualTo(1));
            Assert.That((int)PlayerAnimationState.Jump, Is.EqualTo(2));
            Assert.That((int)PlayerAnimationState.SwingBack, Is.EqualTo(3));
            Assert.That((int)PlayerAnimationState.SwingForward, Is.EqualTo(4));
            Assert.That((int)PlayerAnimationState.Landing, Is.EqualTo(5));
        }

        [Test]
        public void RepeatedStateRequestsDoNotEmitDuplicateEvents()
        {
            var player = new GameObject("AnimationTestPlayer");
            try
            {
                var controller = player.AddComponent<PlayerAnimationController>();
                var eventCount = 0;
                controller.OnAnimationStateChanged += _ => eventCount++;

                Assert.That(controller.SetState(PlayerAnimationState.Walk), Is.True);
                Assert.That(controller.SetState(PlayerAnimationState.Walk), Is.False);
                Assert.That(controller.SetState(PlayerAnimationState.Jump), Is.True);
                Assert.That(eventCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void MissingAnimatorDoesNotBlockStateChanges()
        {
            var player = new GameObject("AnimationFallbackPlayer");
            try
            {
                var controller = player.AddComponent<PlayerAnimationController>();

                Assert.DoesNotThrow(() => controller.SetState(PlayerAnimationState.Landing));
                Assert.That(controller.CurrentState, Is.EqualTo(PlayerAnimationState.Landing));
                Assert.That(PlayerAnimationController.IsValidState(0), Is.True);
                Assert.That(PlayerAnimationController.IsValidState(5), Is.True);
                Assert.That(PlayerAnimationController.IsValidState(-1), Is.False);
                Assert.That(PlayerAnimationController.IsValidState(6), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void LandingRecoveryDefaultsMatchDemoBalance()
        {
            var config = ScriptableObject.CreateInstance<GameBalanceConfig>();
            try
            {
                Assert.That(config.animationBlendDuration, Is.EqualTo(0.1f).Within(0.0001f));
                Assert.That(config.landingRecoveryDuration, Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
