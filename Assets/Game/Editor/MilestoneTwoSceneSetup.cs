using System;
using System.IO;
using SpiderSwing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSwing.Editor
{
    public static class MilestoneTwoSceneSetup
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string BalanceConfigPath = "Assets/Game/Gameplay/Config/GameBalanceConfig.asset";

        [MenuItem("Spider Swing/Apply Milestone 2 - Local Traversal")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            var camera = Camera.main;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (player == null || camera == null || actions == null)
            {
                throw new InvalidOperationException(
                    "Milestone 2 setup requires Gameplay, Main Camera, LocalPlayerMarker, and InputSystem_Actions.");
            }

            var config = EnsureBalanceConfig();
            var courseRoot = EnsureObject("Course", null);
            var orbitCamera = EnsureComponent<OrbitCamera>(camera.gameObject);
            var playerController = EnsureComponent<LocalPlayerController>(player);
            orbitCamera.Configure(actions, player.transform);
            playerController.Configure(actions, orbitCamera, config);

            EnsureCoursePlatform(courseRoot.transform, "P01", new Vector3(-8f, 4f, 14f));
            EnsureCoursePlatform(courseRoot.transform, "P02", new Vector3(8f, 4f, 27f));
            EnsureCoursePlatform(courseRoot.transform, "P03", new Vector3(-8f, 4f, 40f));

            var swingZone = EnsureSwingZone(courseRoot.transform);
            var bottomFloor = EnsureBottomFloor(courseRoot.transform);
            var bounds = EnsureCourseBounds(courseRoot.transform);
            var hubSpawn = EnsureObject("HubSpawnPoint", null);
            hubSpawn.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);

            var webLine = EnsureComponent<LineRenderer>(player);
            ConfigureWebLine(webLine);
            var deathController = EnsureComponent<PlayerDeathController>(player);
            deathController.Configure(config, bounds, hubSpawn.transform);
            playerController.ConfigureWorld(swingZone, deathController, webLine);

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Spider Swing Milestone 2 local traversal setup completed.");
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
            config.maxSwings = 2;
            config.swingDuration = 1.25f;
            config.swingForwardMultiplier = 1f;
            config.swingVerticalCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, -2f),
                new Keyframe(1f, 6f));
            config.webAnchorForwardOffset = 6f;
            config.webAnchorHeightOffset = 10f;
            config.respawnDelay = 1f;
            config.releaseVelocityMinimum = -12f;
            config.releaseVelocityMaximum = 12f;
            EditorUtility.SetDirty(config);
            return config;
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

        private static GameObject EnsureCoursePlatform(
            Transform parent,
            string platformId,
            Vector3 position)
        {
            var platform = GameObject.Find(platformId);
            if (platform == null)
            {
                platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = platformId;
            }

            platform.transform.SetParent(parent, true);
            platform.transform.SetPositionAndRotation(position, Quaternion.identity);
            platform.transform.localScale = new Vector3(8f, 1f, 8f);
            EnsureComponent<CoursePlatform>(platform).Configure(platformId);
            EnsureSolidCollider(platform);
            SetColor(platform, PlatformColor(platformId));
            return platform;
        }

        private static SwingAllowedZone EnsureSwingZone(Transform parent)
        {
            var zoneObject = EnsureObject("SwingAllowedZone", parent);
            zoneObject.transform.SetPositionAndRotation(new Vector3(0f, 20f, 26f), Quaternion.identity);
            var collider = EnsureComponent<BoxCollider>(zoneObject);
            collider.isTrigger = true;
            collider.size = new Vector3(44f, 68f, 40f);
            EnsureComponent<SwingAllowedZone>(zoneObject).Configure(collider);
            return zoneObject.GetComponent<SwingAllowedZone>();
        }

        private static GameObject EnsureBottomFloor(Transform parent)
        {
            var floor = GameObject.Find("BottomFloor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "BottomFloor";
            }

            floor.transform.SetParent(parent, true);
            floor.transform.SetPositionAndRotation(new Vector3(0f, -14f, 21f), Quaternion.identity);
            floor.transform.localScale = new Vector3(50f, 1f, 70f);
            EnsureComponent<DeathSurface>(floor);
            EnsureSolidCollider(floor);
            SetColor(floor, new Color(0.22f, 0.08f, 0.12f));
            return floor;
        }

        private static CourseBounds EnsureCourseBounds(Transform parent)
        {
            var boundsObject = EnsureObject("OutOfBoundsVolume", parent);
            boundsObject.transform.SetPositionAndRotation(new Vector3(0f, -20f, 21f), Quaternion.identity);
            var collider = EnsureComponent<BoxCollider>(boundsObject);
            collider.isTrigger = true;
            collider.size = new Vector3(44f, 160f, 58f);
            EnsureComponent<CourseBounds>(boundsObject).Configure(collider);
            return boundsObject.GetComponent<CourseBounds>();
        }

        private static void ConfigureWebLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.04f;
            line.enabled = false;
        }

        private static void EnsureSolidCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = false;
                collider.enabled = true;
            }
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void SetColor(GameObject target, Color color)
        {
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }

        private static Color PlatformColor(string platformId)
        {
            switch (platformId)
            {
                case "P01":
                    return new Color(0.2f, 0.55f, 0.85f);
                case "P02":
                    return new Color(0.4f, 0.8f, 0.45f);
                default:
                    return new Color(0.9f, 0.55f, 0.2f);
            }
        }
    }
}
