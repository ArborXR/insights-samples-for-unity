using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The shell's dark palette. Colours are given in OKLCH, so each series can tint the same shades with its
    /// own hue. Stations keep the brand colours in <see cref="UiKit"/>.
    /// </summary>
    public static class Theme
    {
        public const float BrandHue = 279f;

        public static readonly Color Background = Oklch(0.17f, 0.035f, 268f);
        public static readonly Color Bar = Oklch(0.15f, 0.032f, 268f);
        public static readonly Color Surface = Oklch(0.225f, 0.04f, 268f);
        public static readonly Color Popover = Oklch(0.19f, 0.035f, 268f);
        public static readonly Color Console = Oklch(0.13f, 0.022f, 266f);
        // Panels over the 3D view. All but opaque: the canvas can't blur what's behind them, and it blends in
        // linear space, where a bright scene shows through a dark panel far more than the alpha suggests.
        public static readonly Color Glass = Oklch(0.15f, 0.032f, 268f, 0.985f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.5f);

        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color LineStrong = new Color(1f, 1f, 1f, 0.14f);
        public static readonly Color Hover = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Pressed = new Color(1f, 1f, 1f, 0.1f);

        public static readonly Color Text = Oklch(0.97f, 0.005f, 268f);
        public static readonly Color TextMuted = Oklch(0.80f, 0.02f, 268f);
        public static readonly Color TextFaint = Oklch(0.62f, 0.025f, 268f);

        public static readonly Color Accent = Oklch(0.56f, 0.21f, BrandHue);
        public static readonly Color AccentHover = Oklch(0.62f, 0.20f, BrandHue);
        // A control that is switched on, such as the header's Stream toggle.
        public static readonly Color Selected = Oklch(0.56f, 0.21f, BrandHue, 0.22f);
        public static readonly Color SelectedHover = Oklch(0.56f, 0.21f, BrandHue, 0.32f);
        public static readonly Color SelectedInk = Oklch(0.86f, 0.09f, BrandHue);
        public static readonly Color Mint = Oklch(0.82f, 0.15f, 166f);
        public static readonly Color Amber = Oklch(0.84f, 0.14f, 80f);
        public static readonly Color Red = Oklch(0.70f, 0.18f, 25f);

        // Code: API names in prose, and the values in a Stream record.
        public static readonly Color Code = Oklch(0.90f, 0.05f, 270f);
        public static readonly Color Method = Oklch(0.93f, 0.04f, 270f);
        public static readonly Color StringValue = Oklch(0.83f, 0.12f, 155f);
        public static readonly Color NumberValue = Oklch(0.83f, 0.12f, 60f);
        public static readonly Color Keyword = Oklch(0.78f, 0.12f, 310f);
        public static readonly Color Key = Oklch(0.74f, 0.04f, 250f);

        /// <summary>A colour for TextMesh Pro's rich text: <c>&lt;color=#RRGGBBAA&gt;</c>.</summary>
        public static string Tag(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);

        /// <summary>Wraps text in a rich-text colour.</summary>
        public static string Paint(string text, Color color) => $"<color={Tag(color)}>{text}</color>";

        /// <summary>OKLCH (lightness 0-1, chroma, hue in degrees) as an sRGB colour, clipped to the sRGB gamut.</summary>
        public static Color Oklch(float lightness, float chroma, float hue, float alpha = 1f)
        {
            var radians = hue * Mathf.Deg2Rad;
            float a = chroma * Mathf.Cos(radians), b = chroma * Mathf.Sin(radians);

            var l = Cube(lightness + 0.3963377774f * a + 0.2158037573f * b);
            var m = Cube(lightness - 0.1055613458f * a - 0.0638541728f * b);
            var s = Cube(lightness - 0.0894841775f * a - 1.2914855480f * b);

            return new Color(
                Encode(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s),
                Encode(-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s),
                Encode(-0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s),
                alpha);
        }

        private static float Cube(float value) => value * value * value;

        private static float Encode(float linear)
        {
            linear = Mathf.Clamp01(linear);
            return linear <= 0.0031308f ? 12.92f * linear : 1.055f * Mathf.Pow(linear, 1f / 2.4f) - 0.055f;
        }
    }
}
