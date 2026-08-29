using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace SpiderSwing.Editor
{
    public static class BuildWebDemo
    {
        private const string OutputPath = "Build/WebGL/Milestone0";

        [MenuItem("Spider Swing/Build Web Demo")]
        public static void Run()
        {
            var webTarget = NamedBuildTarget.WebGL;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(webTarget, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCodeGeneration(webTarget, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;

            // This folder is generated output only. Clearing it avoids stale files
            // from a previous Web build inflating the itch.io archive.
            if (Directory.Exists(OutputPath))
            {
                Directory.Delete(OutputPath, true);
            }

            Directory.CreateDirectory(OutputPath);

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Web build failed: {report.summary.result}");
            }

            UnityEngine.Debug.Log($"Web build completed: {report.summary.totalSize / (1024f * 1024f):0.00} MB");
        }
    }
}
