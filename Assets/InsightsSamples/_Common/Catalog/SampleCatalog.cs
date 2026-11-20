using System;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The ordered list of samples. The single source for the sample browser, Prev/Next, and deep links.
    /// </summary>
    [CreateAssetMenu(menuName = "Insights Samples/Sample Catalog")]
    public class SampleCatalog : ScriptableObject
    {
        public const string ResourcePath = "SampleCatalog";

        public SampleInfo[] samples;

        [Tooltip("How each series looks in the browser and intro. A series not listed gets the brand hue.")]
        public SeriesStyle[] series;

        private static SampleCatalog _instance;

        public static SampleCatalog Instance =>
            _instance != null ? _instance : _instance = Resources.Load<SampleCatalog>(ResourcePath);

        public SampleInfo Find(string id) => Array.Find(samples, s => s != null && s.id == id);

        public SampleInfo FindByScene(string sceneName) => Array.Find(samples, s => s != null && s.sceneName == sceneName);

        public SeriesStyle StyleFor(string seriesName)
        {
            if (series != null)
                foreach (var style in series)
                    if (style.name == seriesName) return style;
            return new SeriesStyle { name = seriesName, icon = "layers", hue = Theme.BrandHue };
        }

        public SampleInfo Neighbor(SampleInfo current, int step)
        {
            var index = Array.IndexOf(samples, current) + step;
            return current != null && index >= 0 && index < samples.Length ? samples[index] : null;
        }
    }

    [Serializable]
    public struct SeriesStyle
    {
        public string name;
        [Tooltip("A name from UiIcons (ArtSource/Icons/lucide).")]
        public string icon;
        [Tooltip("The OKLCH hue, in degrees, that tints the series' shelf, covers, and intros.")]
        public float hue;
        [Tooltip("Optional: who the series is for, shown beside its name in the browser.")]
        public string note;

        /// <summary>A shade of the series' hue.</summary>
        public Color Tint(float lightness, float chroma, float alpha = 1f) => Theme.Oklch(lightness, chroma, hue, alpha);
    }
}
