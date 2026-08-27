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
        private GameObject treadmillObject;
        private GameObject firstUpgradeObject;
        private GameObject secondUpgradeObject;
        private InputActionAsset inputActions;
        private GameBalanceConfig balanceConfig;
        private PlayerCheckpointProgress checkpointProgress;
        private PlayerDemoRewards demoRewards;
        private PlayerProgression progression;

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
            Destroy(treadmillObject);
            Destroy(firstUpgradeObject);
            Destroy(secondUpgradeObject);
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
        public IEnumerator TraversalMovementAwardsXpThroughOneSubscription()
        {
            CreatePlayer(out var localController);

            typeof(LocalPlayerController)
                .GetMethod("MoveAndTrack", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(localController, new object[] { Vector3.forward, true });
            yield return null;

            Assert.That(progression.CurrentXp, Is.GreaterThan(0.01f));

            progression.enabled = false;
            progression.enabled = true;
            var beforeSecondMove = progression.CurrentXp;
            typeof(LocalPlayerController)
                .GetMethod("MoveAndTrack", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(localController, new object[] { Vector3.forward, true });
            yield return null;

            var secondAward = progression.CurrentXp - beforeSecondMove;
            Assert.That(secondAward, Is.GreaterThan(0.1f));
            Assert.That(secondAward, Is.LessThan(1.5f));
        }

        [UnityTest]
        public IEnumerator LevelUpKeepsAFullPlayerAtTheNewMaximum()
        {
            CreatePlayer(out var localController);

            progression.AddTraversalDistance(100f);

            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.CurrentXp, Is.EqualTo(0f).Within(0.001f));
            Assert.That(localController.MoveSpeed, Is.EqualTo(7.75f).Within(0.001f));
            Assert.That(localController.SwingForwardMultiplier, Is.EqualTo(1.15f).Within(0.001f));
            Assert.That(localController.MaxSwings, Is.EqualTo(3));
            Assert.That(localController.CurrentSwings, Is.EqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelUpDoesNotRefillPartiallySpentSwings()
        {
            CreatePlayer(out var localController);
            SetCurrentSwings(localController, 1);

            progression.AddTraversalDistance(100f);

            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(localController.MaxSwings, Is.EqualTo(3));
            Assert.That(localController.CurrentSwings, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrainingAndStackedUpgradesUseTheSameProgressionState()
        {
            CreatePlayer(out var localController);
            var upgradeState = playerObject.AddComponent<PlayerUpgradeState>();
            upgradeState.Configure(progression, demoRewards, localController);
            demoRewards.AwardReturn(25, Vector3.zero);
            SetCurrentSwings(localController, 1);

            firstUpgradeObject = new GameObject("Upgrade01");
            secondUpgradeObject = new GameObject("Upgrade02");
            firstUpgradeObject.AddComponent<BoxCollider>().isTrigger = true;
            secondUpgradeObject.AddComponent<BoxCollider>().isTrigger = true;
            var firstPad = firstUpgradeObject.AddComponent<UpgradePad>();
            var secondPad = secondUpgradeObject.AddComponent<UpgradePad>();
            firstPad.Configure("Upgrade01", 5, 2f, 3, null, Color.cyan);
            secondPad.Configure("Upgrade02", 20, 2f, 3, null, Color.magenta);

            Assert.That(upgradeState.TryPurchase(firstPad), Is.True);
            Assert.That(upgradeState.TryPurchase(secondPad), Is.True);
            Assert.That(progression.UpgradeXpMultiplier, Is.EqualTo(4f).Within(0.001f));
            Assert.That(progression.CurrentMaxSwings, Is.EqualTo(8));
            Assert.That(localController.CurrentSwings, Is.EqualTo(1));
            Assert.That(upgradeState.TryPurchase(firstPad), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TreadmillAwardsXpWhileTheLivingPlayerIsInside()
        {
            CreatePlayer(out _);
            treadmillObject = new GameObject("TestTreadmill");
            var treadmillCollider = treadmillObject.AddComponent<BoxCollider>();
            treadmillCollider.isTrigger = true;
            var treadmill = treadmillObject.AddComponent<TreadmillXpZone>();
            treadmill.Configure(10f);

            typeof(TreadmillXpZone)
                .GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(treadmill, new object[] { playerObject.GetComponent<CharacterController>() });
            var before = progression.CurrentXp;
            typeof(TreadmillXpZone)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(treadmill, null);

            Assert.That(progression.CurrentXp, Is.GreaterThan(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DeathAndRespawnPreserveProgressionState()
        {
            var deathController = CreatePlayer(out var localController);
            progression.AddTraversalDistance(125f);
            var expectedXp = progression.CurrentXp;
            progression.enabled = false;

            Assert.That(deathController.TryDie(PlayerDeathReason.BottomFloor), Is.True);
            yield return new WaitForSecondsRealtime(1.1f);
            progression.enabled = true;

            Assert.That(progression.Level, Is.EqualTo(2));
            Assert.That(progression.CurrentXp, Is.EqualTo(expectedXp).Within(0.001f));
            Assert.That(localController.MaxSwings, Is.EqualTo(3));
            Assert.That(localController.CurrentSwings, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator LandingAfterLevelUpRefillsToProgressionMaximum()
        {
            CreatePlayer(out var localController);
            progression.AddTraversalDistance(100f);
            CreateLandingPlatform(
                "P01",
                new Vector3(0f, 0.5f, 10000f),
                new Vector3(0f, 2f, 10000f),
                out platformObject,
                out savePointObject);

            SetCurrentSwings(localController, 0);
            LandPlayerOnPlatform(localController, playerObject.GetComponent<CharacterController>(), new Vector3(0f, 3f, 10000f));
            yield return null;

            Assert.That(localController.CurrentSwings, Is.EqualTo(3));
            Assert.That(localController.CurrentSwings, Is.EqualTo(localController.MaxSwings));
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
            progression = playerObject.AddComponent<PlayerProgression>();
            progression.Configure(balanceConfig, localController);

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
