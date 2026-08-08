using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Headless entry point for the Windows player build.
    ///
    /// The normal loop builds through the MCP bridge, which needs the editor open. When it is not —
    /// and the batchmode fallback in CLAUDE.md only works when it is not — there was no way to
    /// produce a build at all. This closes that gap:
    ///
    /// <code>
    /// Unity.exe -batchmode -nographics -quit -projectPath &lt;project&gt;
    ///           -executeMethod Game.EditorTools.BuildCommand.BuildWindows64
    ///           -logFile &lt;log&gt;
    /// </code>
    ///
    /// Settings match the interactive build exactly — development build, windowed, same output
    /// path — so a build made either way is the same build.
    /// </summary>
    public static class BuildCommand
    {
        const string OutputPath = "Builds/Win64/RogueMonk.exe";

        [MenuItem("Monk/Build Windows64")]
        public static void BuildWindows64()
        {
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("no enabled scenes in the build settings");
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };

            Debug.Log($"[Monk] building {OutputPath} from {scenes.Length} scene(s)");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"build {summary.result} with {summary.totalErrors} error(s)");
                return;
            }

            Debug.Log($"[Monk] BUILD SUCCEEDED  {summary.totalSize / (1024f * 1024f):0.0} MB  " +
                      $"in {summary.totalTime.TotalSeconds:0.0}s  warnings {summary.totalWarnings}");

            // Batchmode ignores a plain return code, so say so explicitly. Without this a failed
            // build would look identical to a successful one from the shell.
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static string[] GetEnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    scenes.Add(scene.path);
            }

            return scenes.ToArray();
        }

        static void Fail(string reason)
        {
            Debug.LogError($"[Monk] BUILD FAILED: {reason}");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
