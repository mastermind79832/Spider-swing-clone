using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SpiderSwing.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SpiderSwing.Tests
{
    public sealed class LocalTraversalPlayModeTests
    {
        private GameObject playerObject;
        private GameObject boundsObject;
        private GameObject hubObject;
        private GameObject hubFloorObject;
        private GameObject platformObject;
        private InputActionAsset inputActions;
        private GameBalanceConfig balanceConfig;

        [TearDown]
        public void TearDown()
        {
            if (playerObject != null)
            {
                Object.DestroyImmediate(playerObject);
            }

            if (boundsObject != null)
            {
                Object.DestroyImmediate(boundsObject);
            }

            if (hubObject != null)
            {
                Object.DestroyImmediate(hubObject);
            }

            if (hubFloorObject != null)
            {
                Object.DestroyImmediate(hubFloorObject);
            }

            if (platformObject != null)
            {
                Object.DestroyImmediate(platformObject);
            }

            if (inputActions != null)
            {
                Object.DestroyImmediate(inputActions);
            }

            if (balanceConfig != null)
            {
                Object.DestroyImmediate(balanceConfig);
            }
        }

        [UnityTest]
        public IEnumerator FloorDeathIsGuardedAndRespawnsAtHubWithFullSwings()
        {
            var deathController = CreatePlayer(out var localController);
            yield return null;

            Assert.That(deathController.TryDie(PlayerDeathReason.BottomFloor), Is.True);
            Assert.That(deathController.TryDie(PlayerDeathReason.BottomFloor), Is.False);
            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Dead));
            Assert.That(localController.IsWebVisible, Is.False);

            yield return new WaitForSecondsRealtime(1.1f);

            Assert.That(deathController.IsRespawning, Is.False);
            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Grounded));
            Assert.That(localController.CurrentSwings, Is.EqualTo(localController.MaxSwings));
            Assert.That(playerObject.transform.position, Is.EqualTo(hubObject.transform.position));
            Assert.That(localController.IsWebVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator OutOfBoundsDeathUsesSameGuardedReset()
        {
            var deathController = CreatePlayer(out var localController);
            yield return null;

            deathController.CheckPosition(new Vector3(100f, 1f, 0f));
            Assert.That(deathController.IsRespawning, Is.True);
            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Dead));

            yield return new WaitForSecondsRealtime(1.1f);

            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Grounded));
            Assert.That(localController.CurrentSwings, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PlatformRefillRuleAcceptsTopAndRejectsSideContact()
        {
            CreatePlayer(out var localController);
            platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platformObject.name = "TestPlatform";
            platformObject.transform.position = new Vector3(0f, 0.5f, 0f);
            platformObject.transform.localScale = new Vector3(8f, 1f, 8f);
            platformObject.AddComponent<CoursePlatform>().Configure("P01");

            typeof(LocalPlayerController)
                .GetField("currentSwings", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(localController, 0);
            var characterController = playerObject.GetComponent<CharacterController>();
            characterController.enabled = false;
            playerObject.transform.position = new Vector3(0f, 2f, 0f);
            characterController.enabled = true;
            yield return null;

            Assert.That(localController.CurrentSwings, Is.EqualTo(localController.MaxSwings));
            Assert.That(CoursePlatform.IsTopLanding(Vector3.up), Is.True);
            Assert.That(CoursePlatform.IsTopLanding(Vector3.right), Is.False);
        }

        private PlayerDeathController CreatePlayer(out LocalPlayerController localController)
        {
            balanceConfig = ScriptableObject.CreateInstance<GameBalanceConfig>();
            balanceConfig.respawnDelay = 1f;

            inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var playerMap = inputActions.AddActionMap("Player");
            playerMap.AddAction("Move", InputActionType.Value, "Vector2");
            playerMap.AddAction("Jump", InputActionType.Button);

            playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "TestLocalPlayer";
            playerObject.transform.position = new Vector3(0f, 1f, 0f);
            playerObject.AddComponent<CharacterController>();
            localController = playerObject.AddComponent<LocalPlayerController>();
            localController.Configure(inputActions, null, balanceConfig);
            localController.enabled = true;

            boundsObject = new GameObject("TestBounds");
            var boundsCollider = boundsObject.AddComponent<BoxCollider>();
            boundsCollider.isTrigger = true;
            boundsCollider.size = new Vector3(20f, 100f, 20f);
            var courseBounds = boundsObject.AddComponent<CourseBounds>();
            courseBounds.Configure(boundsCollider);

            hubObject = new GameObject("TestHubSpawn");
            hubObject.transform.position = new Vector3(0f, 1f, 0f);

            hubFloorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hubFloorObject.name = "TestHubFloor";
            hubFloorObject.transform.position = new Vector3(0f, -0.5f, 0f);
            hubFloorObject.transform.localScale = new Vector3(12f, 1f, 12f);

            var deathController = playerObject.AddComponent<PlayerDeathController>();
            deathController.Configure(balanceConfig, courseBounds, hubObject.transform);
            localController.ConfigureWorld(null, deathController, null);
            return deathController;
        }
    }
}
