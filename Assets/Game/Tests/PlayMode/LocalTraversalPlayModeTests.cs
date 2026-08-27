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
        private GameObject hubObject;
        private GameObject hubFloorObject;
        private GameObject platformObject;
        private GameObject savePointObject;
        private GameObject secondPlatformObject;
        private GameObject secondSavePointObject;
        private GameObject returnPointObject;
        private InputActionAsset inputActions;
        private GameBalanceConfig balanceConfig;
        private PlayerCheckpointProgress checkpointProgress;
        private PlayerDemoRewards demoRewards;

        [TearDown]
        public void TearDown()
        {
            Destroy(playerObject);
            Destroy(hubObject);
            Destroy(hubFloorObject);
            Destroy(platformObject);
            Destroy(savePointObject);
            Destroy(secondPlatformObject);
            Destroy(secondSavePointObject);
            Destroy(returnPointObject);
            Destroy(inputActions);
            Destroy(balanceConfig);

            foreach (var rewardText in Object.FindObjectsByType<FloatingRewardText>())
            {
                Destroy(rewardText.gameObject);
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
            Assert.That(
                localController.CurrentSwings,
                Is.EqualTo(localController.MaxSwings),
                $"After hub respawn: state={localController.State}, position={playerObject.transform.position}, grounded={localController.IsGrounded}");
            Assert.That(playerObject.transform.position.x, Is.EqualTo(hubObject.transform.position.x).Within(0.02f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(hubObject.transform.position.y).Within(0.15f));
            Assert.That(playerObject.transform.position.z, Is.EqualTo(hubObject.transform.position.z).Within(0.02f));
            Assert.That(localController.IsWebVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator XLimitDeathUsesTheSameGuardedReset()
        {
            var deathController = CreatePlayer(out var localController);
            yield return null;

            deathController.CheckPosition(new Vector3(45.01f, 1f, 999f));
            Assert.That(deathController.IsRespawning, Is.True);
            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Dead));

            yield return new WaitForSecondsRealtime(1.1f);

            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Grounded));
            Assert.That(localController.CurrentSwings, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LowerYDeathDoesNotNeedAFloorCollider()
        {
            var deathController = CreatePlayer(out var localController);
            yield return null;

            deathController.CheckPosition(new Vector3(0f, -20.01f, 500f));
            Assert.That(deathController.IsRespawning, Is.True);
            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Dead));
        }

        [UnityTest]
        public IEnumerator UpperYClampsWithoutDeath()
        {
            var deathController = CreatePlayer(out var localController);
            yield return null;

            playerObject.GetComponent<CharacterController>().enabled = false;
            playerObject.transform.position = new Vector3(0f, 61f, 0f);
            playerObject.GetComponent<CharacterController>().enabled = true;
            typeof(LocalPlayerController)
                .GetMethod("ClampMaximumY", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(localController, null);

            Assert.That(playerObject.transform.position.y, Is.EqualTo(60f).Within(0.001f));
            Assert.That(deathController.IsRespawning, Is.False);
            Assert.That(localController.State, Is.Not.EqualTo(PlayerMovementState.Dead));
        }

        [UnityTest]
        public IEnumerator TopLandingRefillsAndSavesThePrefabCheckpoint()
        {
            CreatePlayer(out var localController);
            platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platformObject.name = "P01";
            // Keep physics fixtures clear of the authored Gameplay scene, which
            // remains loaded by the Play Mode test runner.
            platformObject.transform.position = new Vector3(0f, 0.5f, 10000f);
            platformObject.transform.localScale = new Vector3(8f, 1f, 8f);
            var platform = platformObject.AddComponent<CoursePlatform>();
            savePointObject = new GameObject("Save point");
            savePointObject.transform.position = new Vector3(0f, 2f, 10000f);
            platform.Configure("P01", savePointObject.transform, null, 1);

            SetCurrentSwings(localController, 0);
            var characterController = playerObject.GetComponent<CharacterController>();
            LandPlayerOnPlatform(localController, characterController, new Vector3(0f, 3f, 10000f));
            yield return null;

            Assert.That(
                localController.CurrentSwings,
                Is.EqualTo(localController.MaxSwings),
                $"After P01 landing: state={localController.State}, position={playerObject.transform.position}, grounded={localController.IsGrounded}, verticalVelocity={localController.VerticalVelocity}, time={Time.time}, delta={Time.deltaTime}");
            Assert.That(checkpointProgress.LastCheckpointId, Is.EqualTo("P01"));
            Assert.That(checkpointProgress.TryGetRespawn(out var respawn, out _), Is.True);
            Assert.That(respawn, Is.EqualTo(savePointObject.transform.position));
        }

        [UnityTest]
        public IEnumerator LandingOnAnyCoursePlatformRestoresFullSwings()
        {
            CreatePlayer(out var localController);
            CreateLandingPlatform(
                "P01",
                new Vector3(0f, 0.5f, 10000f),
                new Vector3(0f, 2f, 10000f),
                out platformObject,
                out savePointObject);
            CreateLandingPlatform(
                "P02",
                new Vector3(0f, 0.5f, 10014f),
                new Vector3(0f, 2f, 10014f),
                out secondPlatformObject,
                out secondSavePointObject);

            var characterController = playerObject.GetComponent<CharacterController>();
            SetCurrentSwings(localController, 0);
            LandPlayerOnPlatform(localController, characterController, new Vector3(0f, 3f, 10000f));
            yield return null;

            Assert.That(
                localController.CurrentSwings,
                Is.EqualTo(localController.MaxSwings),
                $"After P01 landing: state={localController.State}, position={playerObject.transform.position}, grounded={localController.IsGrounded}");
            Assert.That(checkpointProgress.LastCheckpointId, Is.EqualTo("P01"));

            SetCurrentSwings(localController, 0);
            LandPlayerOnPlatform(localController, characterController, new Vector3(0f, 3f, 10014f));
            yield return null;

            Assert.That(
                localController.CurrentSwings,
                Is.EqualTo(localController.MaxSwings),
                $"After P02 landing: state={localController.State}, position={playerObject.transform.position}, grounded={localController.IsGrounded}");
            Assert.That(checkpointProgress.LastCheckpointId, Is.EqualTo("P02"));
        }

        [UnityTest]
        public IEnumerator DeathWithCheckpointCanReviveAtLastPlatform()
        {
            var deathController = CreatePlayer(out var localController);
            CreateCheckpoint("P04", new Vector3(0f, 2f, 20f));
            checkpointProgress.Reach(platformObject.GetComponent<CoursePlatform>());

            Assert.That(deathController.TryDie(PlayerDeathReason.OutOfBounds), Is.True);
            Assert.That(deathController.HasCheckpointRevive, Is.True);
            Assert.That(deathController.ChooseLastPlatformRevive(), Is.True);
            yield return new WaitForSecondsRealtime(1.1f);

            Assert.That(localController.State, Is.EqualTo(PlayerMovementState.Grounded));
            Assert.That(playerObject.transform.position.x, Is.EqualTo(savePointObject.transform.position.x).Within(0.02f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(savePointObject.transform.position.y).Within(0.15f));
            Assert.That(playerObject.transform.position.z, Is.EqualTo(savePointObject.transform.position.z).Within(0.02f));
            Assert.That(localController.CurrentSwings, Is.EqualTo(localController.MaxSwings));
            Assert.That(localController.IsWebVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator ReturnPointAwardsOnceTeleportsToHubAndPreservesCheckpoint()
        {
            var deathController = CreatePlayer(out var localController);
            CreateCheckpoint("P03", new Vector3(0f, 2f, 15f));
            checkpointProgress.Reach(platformObject.GetComponent<CoursePlatform>());

            returnPointObject = new GameObject("Return point");
            returnPointObject.transform.position = new Vector3(0f, 2f, 15f);
            var returnPoint = returnPointObject.AddComponent<CourseReturnPoint>();
            returnPoint.Configure(platformObject.GetComponent<CoursePlatform>(), 3);
            var callbackValue = 0;
            returnPoint.OnReturnRewardAwarded += (value, _) => callbackValue += value;

            Assert.That(returnPoint.TryActivate(localController), Is.True);
            Assert.That(returnPoint.TryActivate(localController), Is.False);
            Assert.That(playerObject.transform.position.x, Is.EqualTo(hubObject.transform.position.x).Within(0.02f));
            Assert.That(playerObject.transform.position.y, Is.EqualTo(hubObject.transform.position.y).Within(0.15f));
            Assert.That(playerObject.transform.position.z, Is.EqualTo(hubObject.transform.position.z).Within(0.02f));
            Assert.That(demoRewards.ReturnPoints, Is.EqualTo(3));
            Assert.That(callbackValue, Is.EqualTo(3));
            Assert.That(checkpointProgress.LastCheckpointId, Is.EqualTo("P03"));
            Assert.That(deathController.IsRespawning, Is.False);
            yield return null;
        }

        private PlayerDeathController CreatePlayer(out LocalPlayerController localController)
        {
            balanceConfig = ScriptableObject.CreateInstance<GameBalanceConfig>();
            balanceConfig.respawnDelay = 1f;

            inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
            var playerMap = inputActions.AddActionMap("Player");
            playerMap.AddAction("Move", InputActionType.Value, "Vector2");
            playerMap.AddAction("Jump", InputActionType.Button);

            playerObject = new GameObject("TestLocalPlayer");
            playerObject.transform.position = new Vector3(0f, 1f, 0f);
            playerObject.AddComponent<CharacterController>();
            localController = playerObject.AddComponent<LocalPlayerController>();
            checkpointProgress = playerObject.AddComponent<PlayerCheckpointProgress>();
            demoRewards = playerObject.AddComponent<PlayerDemoRewards>();
            localController.Configure(inputActions, null, balanceConfig);

            hubObject = new GameObject("TestHubSpawn");
            hubObject.transform.position = new Vector3(0f, 1f, 0f);

            hubFloorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hubFloorObject.name = "TestHubFloor";
            hubFloorObject.transform.position = new Vector3(0f, -0.5f, 0f);
            hubFloorObject.transform.localScale = new Vector3(12f, 1f, 12f);

            var deathController = playerObject.AddComponent<PlayerDeathController>();
            deathController.Configure(balanceConfig, hubObject.transform);
            localController.ConfigureWorld(null, deathController, null);
            return deathController;
        }

        private void CreateCheckpoint(string id, Vector3 savePosition)
        {
            platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platformObject.name = id;
            platformObject.transform.position = new Vector3(0f, 0.5f, savePosition.z);
            var platform = platformObject.AddComponent<CoursePlatform>();
            savePointObject = new GameObject("Save point");
            savePointObject.transform.position = savePosition;
            platform.Configure(id, savePointObject.transform, null, 3);
        }

        private static void CreateLandingPlatform(
            string id,
            Vector3 platformPosition,
            Vector3 savePosition,
            out GameObject createdPlatform,
            out GameObject createdSavePoint)
        {
            createdPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            createdPlatform.name = id;
            createdPlatform.transform.position = platformPosition;
            createdPlatform.transform.localScale = new Vector3(8f, 1f, 8f);
            var platform = createdPlatform.AddComponent<CoursePlatform>();

            createdSavePoint = new GameObject("Save point");
            createdSavePoint.transform.position = savePosition;
            platform.Configure(id, createdSavePoint.transform, null, 1);
        }

        private void MovePlayerAbovePlatform(
            CharacterController characterController,
            Vector3 position)
        {
            characterController.enabled = false;
            playerObject.transform.position = position;
            characterController.enabled = true;
            Physics.SyncTransforms();
        }

        private void LandPlayerOnPlatform(
            LocalPlayerController localController,
            CharacterController characterController,
            Vector3 position)
        {
            MovePlayerAbovePlatform(characterController, position);
            typeof(LocalPlayerController)
                .GetMethod("MoveAndTrack", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(localController, new object[] { Vector3.down * 2f, false });
        }

        private static void SetCurrentSwings(LocalPlayerController controller, int value)
        {
            typeof(LocalPlayerController)
                .GetField("currentSwings", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(controller, value);
        }

        private static void Destroy(Object target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

    }
}
