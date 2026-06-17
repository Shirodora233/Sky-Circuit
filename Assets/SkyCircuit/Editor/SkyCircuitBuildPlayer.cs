using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SkyCircuit.EditorTools
{
    public static class SkyCircuitBuildPlayer
    {
        private const string BuildFolderName = "Builds";
        private const string BuildExecutableName = "Sky Circuit.exe";

        [MenuItem("Sky Circuit/Build Windows Player")]
        public static void BuildWindows64()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Cannot build the Windows player while Unity is in Play Mode.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Could not resolve the Unity project root.");
            }

            BuildWindows64(Path.Combine(projectRoot, BuildFolderName, BuildExecutableName));
        }

        public static void BuildWindows64(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Build output path is empty.", nameof(outputPath));
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
            }

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"Building Windows player to {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows player build failed: {summary.result}. Errors: {summary.totalErrors}. Warnings: {summary.totalWarnings}.");
            }

            Debug.Log($"Windows player build succeeded: {outputPath} ({summary.totalSize} bytes)");
        }
    }
}
