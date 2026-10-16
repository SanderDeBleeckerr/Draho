using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Draho.EditorTools
{
    /// <summary>
    /// Entry points for headless builds (GameCI: buildMethod: Draho.EditorTools.CiBuild.BuildWebGL).
    /// The repository has no hand-made scene, so the build first runs the scene builders.
    /// </summary>
    public static class CiBuild
    {
        /// <summary>Pass 1: create assets and save project settings. The editor must restart after it.</summary>
        public static void Prepare()
        {
            PlaygroundBuilder.Build();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Pass 2: build the player with the settings that pass 1 saved.</summary>
        public static void BuildWebGL()
        {
            PlaygroundBuilder.Build();

            // GitHub Pages cannot send compressed-file headers, so ship uncompressed files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = true;

            // Smaller download: strip unused code (our own scripts are kept by Assets/_Draho/link.xml),
            // compile IL2CPP for size, no debug symbols, ASTC textures (the format iPhones read directly).
            var webgl = UnityEditor.Build.NamedBuildTarget.WebGL;
            PlayerSettings.SetManagedStrippingLevel(webgl, ManagedStrippingLevel.Medium);
            PlayerSettings.SetIl2CppCodeGeneration(webgl, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;

            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path), // apartment first
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
