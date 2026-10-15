using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Draho.EditorTools
{
    /// <summary>
    /// Entry points for headless builds (GameCI: buildMethod: Draho.EditorTools.CiBuild.BuildWebGL).
    /// The repository has no hand-made scene, so the build first runs the playground builder.
    /// </summary>
    public static class CiBuild
    {
        public static void BuildWebGL()
        {
            PlaygroundBuilder.Build();

            // GitHub Pages cannot send compressed-file headers, so ship uncompressed files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { PlaygroundBuilder.ScenePath },
                locationPathName = Argument("-customBuildPath") ?? "build/WebGL/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"Draho CI: build {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, " +
                      $"{report.summary.totalErrors} errors -> {options.locationPathName}");
            if (report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
