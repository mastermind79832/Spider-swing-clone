using SpiderSwing.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSwing.Editor
{
    public static class MilestoneOneSceneSetup
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("Spider Swing/Apply Milestone 1 - Local Movement")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("LocalPlayerMarker");
            var camera = Camera.main;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

            if (player == null || camera == null || actions == null)
            {
                throw new System.InvalidOperationException("Milestone 1 setup requires Gameplay, Main Camera, LocalPlayerMarker, and InputSystem_Actions.");
            }

            var orbitCamera = camera.GetComponent<OrbitCamera>();
            if (orbitCamera == null)
            {
                orbitCamera = camera.gameObject.AddComponent<OrbitCamera>();
            }

            var playerController = player.GetComponent<LocalPlayerController>();
            if (playerController == null)
            {
                playerController = player.AddComponent<LocalPlayerController>();
            }

            orbitCamera.Configure(actions, player.transform);
            playerController.Configure(actions, orbitCamera);

            EditorSceneManager.SaveScene(scene, GameplayScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Spider Swing Milestone 1 local movement setup completed.");
        }
    }
}
