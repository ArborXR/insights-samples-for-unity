using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InsightsSamples.Common.Editor
{
    /// <summary>
    /// Builds the samples. Environment values (App Token, REST URL, Pairing URL) come from environment
    /// variables and are written into the AbxrLib configuration only for the duration of the build, so the
    /// copy in git never holds a token.
    /// </summary>
    public static class SampleBuilds
    {
        private const string ConfigPath = "Assets/Resources/AbxrLib.asset";
        private const string LaunchScenePath = "Assets/InsightsSamples/_Common/Shell/Launch.unity";
        private const string HomeScenePath = "Assets/InsightsSamples/_Common/SampleBrowser/Home.unity";

        private static readonly (string variable, string property)[] EnvironmentValues =
        {
            ("ABXR_APP_TOKEN", "appToken"),
            ("ABXR_REST_URL", "restUrl"),
            ("ABXR_PAIRING_URL", "pairingUrl"),
        };

        [MenuItem("Insights Samples/Build/WebGL (Development)")]
        private static void BuildWebGLDevelopment() => Build(BuildTarget.WebGL, development: true, EnvironmentName());

        [MenuItem("Insights Samples/Build/WebGL (Release)")]
        private static void BuildWebGLRelease() => Build(BuildTarget.WebGL, development: false, EnvironmentName());

        // Leaves the App Token out whatever the environment sets, for hosting one public copy: a WebGL build without
        // one asks each visitor for theirs (InsightsSamples.Hosting).
        [MenuItem("Insights Samples/Build/WebGL (Hosted)")]
        private static void BuildWebGLHosted() => Build(BuildTarget.WebGL, development: false, EnvironmentName(), hosted: true);

        [MenuItem("Insights Samples/Build/Android Flat (Development)")]
        private static void BuildAndroidFlatDevelopment() => Build(BuildTarget.Android, development: true, EnvironmentName());

        [MenuItem("Insights Samples/Build/Android Flat (Release)")]
        private static void BuildAndroidFlatRelease() => Build(BuildTarget.Android, development: false, EnvironmentName());

        /// <summary>
        /// Batchmode entry point: -executeMethod InsightsSamples.Common.Editor.SampleBuilds.BuildFromCommandLine
        /// [-target webgl|android] [-development] [-hosted]. The environment name comes from ABXR_ENV (default: local).
        /// </summary>
        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var targetIndex = Array.IndexOf(args, "-target");
            var target = targetIndex >= 0 && targetIndex + 1 < args.Length && args[targetIndex + 1] == "android"
                ? BuildTarget.Android
                : BuildTarget.WebGL;
            var succeeded = Build(target, args.Contains("-development"), EnvironmentName(), args.Contains("-hosted"));
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        [MenuItem("Insights Samples/Sync Build Scenes")]
        public static void SyncBuildScenes()
        {
            var scenes = new List<string> { LaunchScenePath, HomeScenePath };
            foreach (var sample in SampleCatalog.Instance.samples.Where(s => s != null))
            {
                var path = AssetDatabase.FindAssets($"t:Scene {sample.sceneName}", new[] { "Assets/InsightsSamples" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sample.sceneName);
                if (path != null) scenes.Add(path);
                else Debug.LogError($"No scene named '{sample.sceneName}' for sample {sample.id}.");
            }
            EditorBuildSettings.scenes = scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        }

        public static bool Build(BuildTarget target, bool development, string environment, bool hosted = false)
        {
            // Saving first avoids the "save changes?" dialog, which would block an unattended build.
            EditorSceneManager.SaveOpenScenes();
            SyncBuildScenes();

            var folder = target == BuildTarget.Android ? "Android-Flat" : "WebGL";
            var output = $"Builds/{folder}-{environment}" + (hosted ? "-hosted" : "");
            if (target == BuildTarget.Android) output += "/InsightsSamples.apk";
            // WebGL build files are named by content hash, so a rebuild adds files beside the old ones.
            else FileUtil.DeleteFileOrDirectory($"{output}/Build");

            var restore = ApplyEnvironmentValues(hosted);
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = output,
                    target = target,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                });
                Debug.Log($"Insights Samples build ({folder}, {environment}, {(development ? "development" : "release")}{(hosted ? ", hosted" : "")}): {report.summary.result} → {output}");
                return report.summary.result == BuildResult.Succeeded;
            }
            finally
            {
                restore();
            }
        }

        private static string EnvironmentName() => Value("ABXR_ENV") ?? "local";

        /// <summary>
        /// An environment variable, else the same line in the project's git-ignored .env: the editor started from the
        /// Hub doesn't see the shell's variables.
        /// </summary>
        private static string Value(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value) || !File.Exists(".env")) return value;
            var line = File.ReadAllLines(".env").FirstOrDefault(entry => entry.StartsWith(name + "="));
            return line?.Substring(name.Length + 1).Trim().Trim('"', '\'');
        }

        /// <summary>
        /// Writes any ABXR_* environment values into the configuration, and empties the App Token for a hosted build;
        /// returns how to undo it.
        /// </summary>
        private static Action ApplyEnvironmentValues(bool hosted)
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);
            if (config == null) return () => { };

            var serialized = new SerializedObject(config);
            var originals = new Dictionary<string, string>();
            foreach (var (variable, property) in EnvironmentValues)
            {
                var emptied = hosted && property == "appToken";
                var value = emptied ? "" : Value(variable);
                var field = serialized.FindProperty(property);
                if ((!emptied && string.IsNullOrEmpty(value)) || field == null) continue;
                originals[property] = field.stringValue;
                field.stringValue = value;
            }

            if (originals.Count == 0) return () => { };
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"Applied {originals.Count} environment value(s) to the AbxrLib configuration for this build.");

            return () =>
            {
                foreach (var pair in originals) serialized.FindProperty(pair.Key).stringValue = pair.Value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
            };
        }
    }
}
