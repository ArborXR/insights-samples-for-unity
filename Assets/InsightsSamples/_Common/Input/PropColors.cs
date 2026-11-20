using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Colours for station props. Safety colours follow ANSI Z535: red for stop controls and fire equipment,
    /// yellow for caution, green for safe and first aid.
    /// </summary>
    public static class PropColors
    {
        public static readonly Color SafetyRed = UiKit.FromHex(0xD32F2F);
        public static readonly Color SafetyYellow = UiKit.FromHex(0xF2C200);
        public static readonly Color SafetyGreen = UiKit.FromHex(0x2E9E5B);
        public static readonly Color Steel = UiKit.FromHex(0xA3ACB6);
        public static readonly Color DarkSteel = UiKit.FromHex(0x4A525C);
        public static readonly Color Rubber = UiKit.FromHex(0x2B2F36);
        public static readonly Color Cardboard = UiKit.FromHex(0xC69C6D);
        public static readonly Color Wood = UiKit.FromHex(0xB58B5A);
        public static readonly Color Panel = UiKit.FromHex(0xE4E6EB);
        public static readonly Color Off = UiKit.FromHex(0x3A3F47);
        public static readonly Color Bezel = UiKit.FromHex(0x1C2027);

        /// <summary>Base tint for textured models: just under white, so hover can still brighten them.</summary>
        public static readonly Color Textured = new Color(0.88f, 0.88f, 0.88f);
    }
}
