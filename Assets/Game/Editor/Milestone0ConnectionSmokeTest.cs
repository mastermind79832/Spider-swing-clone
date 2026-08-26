using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpiderSwing.Editor
{
    public static class Milestone0ConnectionSmokeTest
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const double TestDurationSeconds = 6d;
        private static double startTime;
        private static bool batchMode;

        [MenuItem("Spider Swing/Run Connection Smoke Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            batchMode = Application.isBatchMode;
            startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Poll;
            EditorApplication.isPlaying = true;
            Debug.Log("Spider Swing connection smoke test started.");
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup - startTime < TestDurationSeconds)
            {
                return;
            }

            EditorApplication.update -= Poll;
            EditorApplication.isPlaying = false;
            Debug.Log("Spider Swing connection smoke test completed.");
            if (batchMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
