using System;
using System.Collections.Generic;
using System.IO;
using SpiderSwing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

#pragma warning disable 0618

namespace SpiderSwing.Editor
{
    public static class MilestoneTwoSceneSetup
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string BalanceConfigPath = "Assets/Game/Gameplay/Config/GameBalanceConfig.asset";
        private const string PlatformPrefabPath = "Assets/Game/Prefab/Platform.prefab";
        private const string UpgradePrefabPath = "Assets/Game/Prefab/Upgrade .prefab";
        private const int PlatformCount = 20;

        [MenuItem("Spider Swing/Apply Milestone 2B - Prefab Course")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            var camera = Camera.main;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPrefabPath);

            if (player == null || camera == null || actions == null || platformPrefab == null)
            {
                throw new InvalidOperationException(
                    "Milestone 2B setup requires Gameplay, Main Camera, LocalPlayerMarker, " +
                    "InputSystem_Actions, and Assets/Game/Prefab/Platform.prefab.");
            }

            var config = EnsureBalanceConfig();
            var hubFloor = GameObject.Find("HubFloor");
            if (hubFloor == null || hubFloor.GetComponent<Collider>() == null)
            {
                throw new InvalidOperationException(
                    "Milestone 2B setup requires the user-built HubFloor with a solid collider.");
            }

            var forbiddenZone = PreserveAndPrepareForbiddenZone();
            var courseRoot = EnsureObject("Course", null);
            ClearGeneratedCourse(courseRoot.transform);
            BuildPrefabCourse(
                scene,
                courseRoot.transform,
                platformPrefab,
                hubFloor.GetComponent<Collider>().bounds.max.y,
                hubFloor.GetComponent<Collider>().bounds.max.z);

            var hubSpawn = EnsureObject("HubSpawnPoint", null);
            hubSpawn.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);

            var orbitCamera = EnsureComponent<OrbitCamera>(camera.gameObject);
            var playerController = EnsureComponent<LocalPlayerController>(player);
            EnsureComponent<PlayerCheckpointProgress>(player);
            EnsureComponent<PlayerDemoRewards>(player);
            var progression = EnsureComponent<PlayerProgression>(player);
            orbitCamera.Configure(actions, player.transform);
            playerController.Configure(actions, orbitCamera, config);
            progression.Configure(config, playerController);
            EnsureComponent<PlayerSkinVisual>(player).Configure(FindChild(player.transform, "base_rig"));

            var webLine = EnsureComponent<LineRenderer>(player);
            ConfigureWebLine(webLine);
            var deathController = EnsureComponent<PlayerDeathController>(player);
            deathController.Configure(config, hubSpawn.transform);
            playerController.ConfigureWorld(forbiddenZone, deathController, webLine);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing Milestone 2B prefab course setup completed: 20 linked platforms.");
        }

        [MenuItem("Spider Swing/Apply Milestone 3B - Local Progression")]
        public static void ApplyLocalProgression()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            var camera = Camera.main;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (player == null || camera == null || actions == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3B setup requires Gameplay, Main Camera, LocalPlayerMarker, and InputSystem_Actions.");
            }

            var config = EnsureBalanceConfig();
            var orbitCamera = EnsureComponent<OrbitCamera>(camera.gameObject);
            var localController = EnsureComponent<LocalPlayerController>(player);
            var progression = EnsureComponent<PlayerProgression>(player);
            orbitCamera.Configure(actions, player.transform);
            localController.Configure(actions, orbitCamera, config);
            progression.Configure(config, localController);
            EnsureComponent<PlayerSkinVisual>(player).Configure(FindChild(player.transform, "base_rig"));

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(progression);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing Milestone 3B local progression setup completed.");
        }

        [MenuItem("Spider Swing/Apply Milestone 3C - Training and Upgrades")]
        public static void ApplyTrainingAndUpgrades()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            var treadmill = GameObject.Find("Treadmill");
            var camera = Camera.main;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            var upgradePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradePrefabPath);
            if (player == null || treadmill == null || camera == null || actions == null || upgradePrefab == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3C setup requires the saved Treadmill, LocalPlayerMarker, " +
                    "Main Camera, InputSystem_Actions, and Assets/Game/Prefab/Upgrade .prefab.");
            }

            var config = EnsureBalanceConfig();
            var localController = EnsureComponent<LocalPlayerController>(player);
            var progression = EnsureComponent<PlayerProgression>(player);
            var rewards = EnsureComponent<PlayerDemoRewards>(player);
            var upgradeState = EnsureComponent<PlayerUpgradeState>(player);
            var orbitCamera = EnsureComponent<OrbitCamera>(camera.gameObject);
            orbitCamera.Configure(actions, player.transform);
            localController.Configure(actions, orbitCamera, config);
            progression.Configure(config, localController);
            EnsureComponent<PlayerSkinVisual>(player).Configure(FindChild(player.transform, "base_rig"));
            upgradeState.Configure(progression, rewards, localController);

            var treadmillZone = EnsureComponent<TreadmillXpZone>(treadmill);
            var treadmillCollider = treadmill.GetComponent<Collider>();
            if (treadmillCollider == null)
            {
                throw new InvalidOperationException("Treadmill must keep its existing trigger collider.");
            }

            treadmillCollider.isTrigger = true;
            treadmillZone.Configure(config.treadmillXpPerSecond);

            var upgradesRoot = EnsureObject("Demo Upgrades", null);
            var firstUpgrade = GameObject.Find("Upgrade 01")
                ?? GameObject.Find("Upgrade ")
                ?? GameObject.Find("Upgrade");
            if (firstUpgrade == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3C setup requires the existing authored Upgrade prefab instance.");
            }

            ConfigureUpgradeInstance(
                firstUpgrade,
                upgradesRoot.transform,
                new Vector3(-28f, 0.00005f, -29.75465f),
                "Upgrade 01",
                "Upgrade01",
                5,
                new Color(0.05f, 0.85f, 1f));

            var secondUpgrade = FindChild(upgradesRoot.transform, "Upgrade 02")?.gameObject;
            if (secondUpgrade == null)
            {
                secondUpgrade = (GameObject)PrefabUtility.InstantiatePrefab(upgradePrefab, scene);
            }

            ConfigureUpgradeInstance(
                secondUpgrade,
                upgradesRoot.transform,
                new Vector3(-18f, 0.00005f, -29.75465f),
                "Upgrade 02",
                "Upgrade02",
                25,
                new Color(1f, 0.1f, 0.8f));

            var thirdUpgrade = FindChild(upgradesRoot.transform, "Upgrade 03")?.gameObject;
            if (thirdUpgrade == null)
            {
                thirdUpgrade = (GameObject)PrefabUtility.InstantiatePrefab(upgradePrefab, scene);
            }

            ConfigureUpgradeInstance(
                thirdUpgrade,
                upgradesRoot.transform,
                new Vector3(-8f, 0.00005f, -29.75465f),
                "Upgrade 03",
                "Upgrade03",
                75,
                new Color(1f, 0.65f, 0.05f));

            foreach (var platform in UnityEngine.Object.FindObjectsByType<CoursePlatform>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                platform.RefreshPointText();
                EditorUtility.SetDirty(platform);
                if (platform.PointText != null)
                {
                    EditorUtility.SetDirty(platform.PointText);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(platform.PointText);
                }
            }

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(treadmill);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing Milestone 3C training, upgrades, and point labels setup completed.");
        }

        [MenuItem("Spider Swing/Apply Milestone 3D - Arm Swing Visuals")]
        public static void ApplyArmSwingVisuals()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            if (player == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3D setup requires the Gameplay scene with LocalPlayerMarker.");
            }

            var modelRoot = FindChild(player.transform, "base_rig");
            var upperArm = FindChild(modelRoot, "ArmR1");
            var lowerArm = FindChild(modelRoot, "ArmR2");
            if (modelRoot == null || upperArm == null || lowerArm == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3D setup requires base_rig/Armature.001/ArmR1 and ArmR2.");
            }

            var webLine = EnsureComponent<LineRenderer>(player);
            ConfigureWebLine(webLine);
            var webOrigin = EnsureWebOrigin(lowerArm, upperArm);
            var swingVisual = EnsureComponent<PlayerSwingVisual>(player);
            swingVisual.Configure(modelRoot, upperArm, lowerArm, webOrigin, webLine);

            var controller = player.GetComponent<LocalPlayerController>();
            if (controller != null)
            {
                controller.ConfigureWorld(
                    GameObject.Find("SwingForbiddenZone")?.GetComponent<SwingForbiddenZone>(),
                    player.GetComponent<PlayerDeathController>(),
                    webLine);
                EditorUtility.SetDirty(controller);
            }

            EditorUtility.SetDirty(swingVisual);
            EditorUtility.SetDirty(webOrigin);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing Milestone 3D arm-origin swing visual setup completed.");
        }

        [MenuItem("Spider Swing/Apply Milestone 3F - Player Animations")]
        public static void ApplyPlayerAnimations()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            var camera = Camera.main;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (player == null || camera == null || actions == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3F setup requires Gameplay, Main Camera, LocalPlayerMarker, and InputSystem_Actions.");
            }

            var modelRoot = FindChild(player.transform, "base_rig");
            var animator = modelRoot != null
                ? modelRoot.GetComponent<Animator>() ?? modelRoot.GetComponentInChildren<Animator>(true)
                : null;
            if (modelRoot == null || animator == null || animator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException(
                    "Milestone 3F setup requires LocalPlayerMarker/base_rig with the authored PlayerAnim controller.");
            }

            var config = EnsureBalanceConfig();
            var orbitCamera = EnsureComponent<OrbitCamera>(camera.gameObject);
            var localController = EnsureComponent<LocalPlayerController>(player);
            var animationController = EnsureComponent<PlayerAnimationController>(player);
            orbitCamera.Configure(actions, player.transform);
            localController.Configure(actions, orbitCamera, config);
            animationController.Configure(modelRoot, animator, config.animationBlendDuration);

            EditorUtility.SetDirty(animationController);
            EditorUtility.SetDirty(localController);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing Milestone 3F player animation setup completed.");
        }

        [MenuItem("Spider Swing/Repair Upgrade Pad Components")]
        public static void RepairUpgradePadComponents()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var upgradesRoot = GameObject.Find("Demo Upgrades");
            if (upgradesRoot == null)
            {
                throw new InvalidOperationException("Upgrade-pad repair requires the saved Demo Upgrades root.");
            }

            RepairUpgradePad(
                FindChild(upgradesRoot.transform, "Upgrade 01")?.gameObject,
                "Upgrade01",
                5,
                new Color(0.05f, 0.85f, 1f));
            RepairUpgradePad(
                FindChild(upgradesRoot.transform, "Upgrade 02")?.gameObject,
                "Upgrade02",
                25,
                new Color(1f, 0.1f, 0.8f));
            RepairUpgradePad(
                FindChild(upgradesRoot.transform, "Upgrade 03")?.gameObject,
                "Upgrade03",
                75,
                new Color(1f, 0.65f, 0.05f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing repaired upgrade pads: one configured UpgradePad remains on each pad.");
        }

        [MenuItem("Spider Swing/Repair Platform Script References")]
        public static void RepairPlatformScriptReferences()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var course = GameObject.Find("Course");
            if (course == null)
            {
                throw new InvalidOperationException("Platform script repair requires the Gameplay scene with a Course root.");
            }

            var forbiddenZone = RepairForbiddenZone();

            var repairedCount = 0;
            for (var childIndex = 0; childIndex < course.transform.childCount; childIndex++)
            {
                var platformObject = course.transform.GetChild(childIndex).gameObject;
                if (!TryGetPlatformIndex(platformObject.name, out var platformIndex))
                {
                    continue;
                }

                var savePoint = FindChild(platformObject.transform, "Save point");
                var returnObject = FindChild(platformObject.transform, "Return point");
                if (savePoint == null || returnObject == null)
                {
                    throw new InvalidOperationException(
                        $"{platformObject.name} must keep its Save point and Return point children.");
                }

                // These objects currently hold stale serialized components
                // reported by the Inspector. Rebuild only these marker
                // components so each object ends with one valid current type.
                RemoveComponents<CoursePlatform>(platformObject);
                RemoveComponents<CourseReturnPoint>(returnObject.gameObject);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(platformObject);
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(returnObject.gameObject);

                var platform = platformObject.AddComponent<CoursePlatform>();
                var returnPoint = returnObject.gameObject.AddComponent<CourseReturnPoint>();
                returnPoint.Configure(platform, platformIndex);
                platform.Configure(platformObject.name, savePoint, returnPoint, platformIndex);

                EditorUtility.SetDirty(platformObject);
                EditorUtility.SetDirty(returnObject.gameObject);
                repairedCount++;
            }

            if (repairedCount == 0)
            {
                throw new InvalidOperationException("No P01-P20 platform objects were found under Course.");
            }

            var player = GameObject.Find("LocalPlayerMarker");
            var localController = player != null ? player.GetComponent<LocalPlayerController>() : null;
            if (localController != null)
            {
                localController.ConfigureWorld(
                    forbiddenZone,
                    player.GetComponent<PlayerDeathController>(),
                    player.GetComponent<LineRenderer>());
                EditorUtility.SetDirty(localController);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Spider Swing platform script repair completed: {repairedCount} platforms repaired.");
        }

        private static SwingForbiddenZone RepairForbiddenZone()
        {
            var zoneObject = GameObject.Find("SwingForbiddenZone")
                ?? GameObject.Find("SwingNotAllowedZone");
            if (zoneObject == null)
            {
                return null;
            }

            RemoveComponents<SwingForbiddenZone>(zoneObject);
            RemoveComponents<SwingAllowedZone>(zoneObject);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(zoneObject);

            var zone = zoneObject.AddComponent<SwingForbiddenZone>();
            zone.Configure(zoneObject.GetComponent<BoxCollider>());
            EditorUtility.SetDirty(zoneObject);
            return zone;
        }

        private static void RemoveComponents<T>(GameObject target) where T : Component
        {
            foreach (var component in target.GetComponents<T>())
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static bool TryGetPlatformIndex(string objectName, out int platformIndex)
        {
            platformIndex = 0;
            return objectName != null
                && objectName.Length == 3
                && objectName[0] == 'P'
                && int.TryParse(objectName.Substring(1), out platformIndex)
                && platformIndex >= 1
                && platformIndex <= PlatformCount;
        }

        private static GameBalanceConfig EnsureBalanceConfig()
        {
            var directory = Path.GetDirectoryName(BalanceConfigPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var config = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(BalanceConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameBalanceConfig>();
                AssetDatabase.CreateAsset(config, BalanceConfigPath);
            }

            config.moveSpeed = 7f;
            config.rotationSharpness = 14f;
            config.gravity = -24f;
            config.jumpHeight = 6f;
            config.jumpLandingGraceDuration = 0.12f;
            config.maximumTravelSpeed = 50f;
            config.maxSwings = 2;
            config.swingDuration = 1.25f;
            config.swingForwardMultiplier = 1f;
            config.swingVerticalCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, -2f),
                new Keyframe(1f, 6f));
            config.webAnchorHeightOffset = 20f;
            config.respawnDelay = 1f;
            config.releaseVelocityMinimum = -12f;
            config.releaseVelocityMaximum = 12f;
            config.sideDeathX = 45f;
            config.deathY = -20f;
            config.maximumY = 60f;
            config.xpMultiplier = 1f;
            config.baseXpToNextLevel = 100f;
            config.movementSpeedPerLevel = 1.25f;
            config.swingForwardMultiplierPerLevel = 0.15f;
            config.extraSwingEveryLevels = 2;
            config.treadmillXpPerSecond = 10f;
            config.animationBlendDuration = 0.1f;
            config.landingRecoveryDuration = 0.4f;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static SwingForbiddenZone PreserveAndPrepareForbiddenZone()
        {
            var zoneObject = GameObject.Find("SwingForbiddenZone")
                ?? GameObject.Find("SwingNotAllowedZone");
            if (zoneObject == null)
            {
                throw new InvalidOperationException(
                    "Milestone 2B setup requires the existing hub object named SwingNotAllowedZone.");
            }

            var legacyZone = zoneObject.GetComponent<SwingAllowedZone>();
            if (legacyZone != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyZone);
            }

            var zone = EnsureComponent<SwingForbiddenZone>(zoneObject);
            var collider = zoneObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                throw new InvalidOperationException(
                    "SwingNotAllowedZone must keep its existing BoxCollider bounds.");
            }

            zoneObject.name = "SwingForbiddenZone";
            zoneObject.transform.SetParent(null, true);
            collider.isTrigger = true;
            collider.enabled = true;
            zone.Configure(collider);
            return zone;
        }

        private static void ClearGeneratedCourse(Transform courseRoot)
        {
            var children = new List<Transform>();
            for (var index = 0; index < courseRoot.childCount; index++)
            {
                children.Add(courseRoot.GetChild(index));
            }

            foreach (var child in children)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void BuildPrefabCourse(
            Scene scene,
            Transform courseRoot,
            GameObject platformPrefab,
            float hubTopY,
            float hubEndZ)
        {
            var previousPlatformEndZ = hubEndZ;

            for (var index = 0; index < PlatformCount; index++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(platformPrefab, scene);
                instance.name = $"P{index + 1:00}";
                instance.transform.SetParent(courseRoot, true);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var rootCollider = instance.GetComponent<Collider>();
                if (rootCollider == null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    throw new InvalidOperationException(
                        "Platform.prefab must have a solid root collider for course spacing.");
                }

                var gap = CourseLayoutRules.GapForIndex(index);
                var targetMinZ = previousPlatformEndZ + gap;
                var targetY = hubTopY - (rootCollider.bounds.max.y - instance.transform.position.y);
                instance.transform.position = new Vector3(0f, targetY, 0f);
                instance.transform.position += Vector3.forward * (targetMinZ - rootCollider.bounds.min.z);

                var platform = EnsureComponent<CoursePlatform>(instance);
                var savePoint = FindChild(instance.transform, "Save point");
                var returnObject = FindChild(instance.transform, "Return point");
                if (savePoint == null || returnObject == null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    throw new InvalidOperationException(
                        "Platform.prefab must contain Save point and Return point children.");
                }

                var returnCollider = EnsureComponent<BoxCollider>(returnObject.gameObject);
                returnCollider.isTrigger = true;
                returnCollider.enabled = true;
                var returnPoint = EnsureComponent<CourseReturnPoint>(returnObject.gameObject);
                returnPoint.Configure(platform, index + 1);
                platform.Configure(instance.name, savePoint, returnPoint, index + 1);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(returnObject.gameObject);

                previousPlatformEndZ = rootCollider.bounds.max.z;
            }
        }

        private static void ConfigureUpgradeInstance(
            GameObject upgrade,
            Transform parent,
            Vector3 position,
            string objectName,
            string upgradeId,
            int cost,
            Color labelColor)
        {
            upgrade.name = objectName;
            upgrade.transform.SetParent(parent, true);
            upgrade.transform.SetPositionAndRotation(position, Quaternion.identity);

            var collider = EnsureComponent<BoxCollider>(upgrade);
            collider.isTrigger = true;
            collider.enabled = true;

            var pad = GetSingleUpgradePad(upgrade);
            pad.Configure(upgradeId, cost, 2f, 3, labelColor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pad);
            PrefabUtility.RecordPrefabInstancePropertyModifications(upgrade);
            EditorUtility.SetDirty(upgrade);
        }

        private static void RepairUpgradePad(
            GameObject upgrade,
            string upgradeId,
            int cost,
            Color labelColor)
        {
            if (upgrade == null)
            {
                throw new InvalidOperationException($"Upgrade-pad repair could not find {upgradeId}.");
            }

            var pad = GetSingleUpgradePad(upgrade);
            pad.Configure(upgradeId, cost, 2f, 3, labelColor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(pad);
            PrefabUtility.RecordPrefabInstancePropertyModifications(upgrade);
            EditorUtility.SetDirty(upgrade);
        }

        private static UpgradePad GetSingleUpgradePad(GameObject upgrade)
        {
            var pads = upgrade.GetComponents<UpgradePad>();
            if (pads.Length == 0)
            {
                return upgrade.AddComponent<UpgradePad>();
            }

            var keeper = Array.Find(
                pads,
                pad => PrefabUtility.GetCorrespondingObjectFromSource(pad) != null) ?? pads[0];
            foreach (var pad in pads)
            {
                if (pad != keeper)
                {
                    Undo.DestroyObjectImmediate(pad);
                }
            }

            return keeper;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == childName)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindChild(root.GetChild(index), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform EnsureWebOrigin(Transform lowerArm, Transform upperArm)
        {
            var origin = FindChild(lowerArm, "RightWebOrigin");
            if (origin == null)
            {
                var socket = new GameObject("RightWebOrigin");
                origin = socket.transform;
                origin.SetParent(lowerArm, false);
            }

            var worldLength = GetLowerArmLength(lowerArm, upperArm);
            var localScale = Mathf.Max(0.001f, Mathf.Abs(lowerArm.lossyScale.y));
            origin.localPosition = Vector3.up * Mathf.Max(0.005f, worldLength / localScale);
            origin.localRotation = Quaternion.identity;
            origin.localScale = Vector3.one;
            return origin;
        }

        private static float GetLowerArmLength(Transform lowerArm, Transform upperArm)
        {
            for (var index = 0; lowerArm != null && index < lowerArm.childCount; index++)
            {
                var child = lowerArm.GetChild(index);
                if (child.name == "RightWebOrigin")
                {
                    continue;
                }

                var childLength = Vector3.Distance(lowerArm.position, child.position);
                if (childLength > 0.0001f)
                {
                    return childLength;
                }
            }

            return upperArm != null && lowerArm != null
                ? Vector3.Distance(upperArm.position, lowerArm.position)
                : 0.6f;
        }

        private static GameObject EnsureObject(string objectName, Transform parent)
        {
            var target = GameObject.Find(objectName);
            if (target == null)
            {
                target = new GameObject(objectName);
            }

            if (parent != null)
            {
                target.transform.SetParent(parent, true);
            }

            return target;
        }

        private static void ConfigureWebLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.04f;
            line.enabled = false;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}

#pragma warning restore 0618
