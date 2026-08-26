using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpiderSwing.Editor
{
    public static class DemoProjectSetup
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";

        [MenuItem("Spider Swing/Setup Foundation")]
        public static void Run()
        {
            EnsureBuiltInRenderPipeline();
            ConfigureWebPlayerSettings();
            CreateGameplayScene();
            Debug.Log("Spider Swing foundation setup completed.");
        }

        private static void EnsureBuiltInRenderPipeline()
        {
            GraphicsSettings.defaultRenderPipeline = null;

            var previousQuality = QualitySettings.GetQualityLevel();
            var qualityCount = QualitySettings.names.Length;

            for (var index = 0; index < qualityCount; index++)
            {
                QualitySettings.SetQualityLevel(index, false);
                QualitySettings.renderPipeline = null;
            }

            if (qualityCount > 0)
            {
                QualitySettings.SetQualityLevel(Mathf.Clamp(previousQuality, 0, qualityCount - 1), false);
            }

            if (GraphicsSettings.currentRenderPipeline != null)
            {
                throw new InvalidOperationException("Built-In Render Pipeline setup failed.");
            }
        }

        private static void ConfigureWebPlayerSettings()
        {
            PlayerSettings.SetApiCompatibilityLevel(
                NamedBuildTarget.WebGL,
                ApiCompatibilityLevel.NET_Standard);
        }

        private static void CreateGameplayScene()
        {
            Directory.CreateDirectory("Assets/Game/Scenes");
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            CreateCamera();
            CreateLight();
            CreateFloor();
            var localPlayerMarker = CreatePlayerMarker();
            var networkRoot = new GameObject("NetworkRoot");
            networkRoot.AddComponent<SpiderSwing.Network.ColyseusClient>()
                .SetLocalPlayerMarker(localPlayerMarker.transform);

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 7f, -10f),
                Quaternion.Euler(25f, 0f, 0f));

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.16f);
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
        }

        private static void CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "HubFloor";
            floor.transform.SetPositionAndRotation(
                new Vector3(0f, -0.5f, 0f),
                Quaternion.identity);
            floor.transform.localScale = new Vector3(12f, 1f, 12f);
        }

        private static GameObject CreatePlayerMarker()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "LocalPlayerMarker";
            player.transform.position = new Vector3(0f, 1f, 0f);
            return player;
        }
    }
}
