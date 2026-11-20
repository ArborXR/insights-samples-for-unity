using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InsightsSamples.Common.Editor
{
    /// <summary>
    /// Enforces the structure rules in CONTRIBUTING.md before every build: only lesson scripts (*Sample.cs), the
    /// Insights Stream, and the hosted build's token plumbing reference the SDK, and every sample has its scene, lesson
    /// script, autopilot, and icon.
    /// </summary>
    public class SampleValidator : IPreprocessBuildWithReport
    {
        private const string Root = "Assets/InsightsSamples";
        private static readonly Regex SdkCall = new Regex(@"\bAbxr\s*\.", RegexOptions.Compiled);
        private static readonly Regex Comments = new Regex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = Validate();
            if (errors.Count > 0)
                throw new BuildFailedException("Insights Samples validation failed:\n" + string.Join("\n", errors));
        }

        [MenuItem("Insights Samples/Validate")]
        private static void ValidateFromMenu()
        {
            var errors = Validate();
            if (errors.Count == 0) Debug.Log("Insights Samples validation passed.");
            foreach (var error in errors) Debug.LogError(error);
        }

        public static List<string> Validate()
        {
            var errors = new List<string>();

            foreach (var path in Directory.GetFiles(Root, "*.cs", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
            {
                if (path.EndsWith("Sample.cs") || path.Contains("/_Common/InsightsStream/") || path.Contains("/_Hosting/") ||
                    path.Contains("/Editor/")) continue;
                if (SdkCall.IsMatch(Comments.Replace(File.ReadAllText(path), "")))
                    errors.Add($"{path} calls Abxr. Only lesson scripts (*Sample.cs) may call the SDK (CONTRIBUTING.md).");
            }

            var catalog = SampleCatalog.Instance;
            if (catalog == null)
            {
                errors.Add("Missing Resources/SampleCatalog.asset.");
                return errors;
            }

            foreach (var style in catalog.series ?? new SeriesStyle[0])
            {
                if (!UiIcons.Has(style.icon))
                    errors.Add($"Series '{style.name}': no icon '{style.icon}' in ArtSource/Icons/lucide. Add its SVG and run build_icons.py.");
            }

            foreach (var sample in catalog.samples)
            {
                if (sample == null)
                {
                    errors.Add("The sample catalog has an empty entry.");
                    continue;
                }
                if (!UiIcons.Has(sample.icon))
                    errors.Add($"{sample.id}: no icon '{sample.icon}' in ArtSource/Icons/lucide. Add its SVG and run build_icons.py.");
                if (catalog.series == null || !catalog.series.Any(style => style.name == sample.series))
                    errors.Add($"{sample.id}: its series '{sample.series}' has no entry in the catalog's Series list.");

                var folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(sample))?.Replace('\\', '/');
                if (folder == null) continue;
                if (!File.Exists($"{folder}/{sample.sceneName}.unity"))
                    errors.Add($"{sample.id}: no scene {sample.sceneName}.unity in {folder}.");
                if (Directory.GetFiles(folder, "*Sample.cs").Length != 1)
                    errors.Add($"{sample.id}: {folder} needs exactly one lesson script (*Sample.cs).");
                if (Directory.GetFiles(folder, "*Autopilot.cs").Length == 0)
                    errors.Add($"{sample.id}: {folder} has no autopilot (*Autopilot.cs).");
            }

            return errors;
        }
    }
}
