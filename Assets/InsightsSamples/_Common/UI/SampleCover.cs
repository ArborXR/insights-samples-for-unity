using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// A sample's cover on its browser card: its icon on a station panel, in a small room tinted with its series' hue.
    /// </summary>
    public static class SampleCover
    {
        public const float Aspect = 16f / 10f;

        private static readonly Color Frame = Theme.Oklch(0.12f, 0.01f, 268f);
        private static readonly Color Screen = Theme.Oklch(0.18f, 0.033f, 268f);
        private static readonly Color Stand = Theme.Oklch(0.32f, 0.01f, 268f);
        private static readonly Color Skirting = Theme.Oklch(0.15f, 0.04f, 268f);

        /// <summary>Builds a cover <paramref name="width"/> wide. Its top corners take the card's radius.</summary>
        public static RectTransform Build(Transform parent, SampleInfo sample, SeriesStyle style, float width, float radius)
        {
            var root = UiKit.Rect(parent, "Cover");
            root.sizeDelta = new Vector2(width, width / Aspect);

            // The floor is square, so it squares off the wall's lower corners where the card's body begins.
            var wall = UiKit.Card(root, "Wall", Color.white, radius);
            wall.raycastTarget = false;
            UiKit.Stretch(wall.rectTransform);
            Shade(wall, style.Tint(0.38f, 0.06f), style.Tint(0.27f, 0.045f));

            var floor = UiKit.Panel(root, "Floor", Color.white);
            floor.raycastTarget = false;
            Place(floor.rectTransform, 0f, 1f, 0f, 0.3f);
            Shade(floor, style.Tint(0.24f, 0.025f), style.Tint(0.18f, 0.02f));
            var skirting = UiKit.Panel(root, "Skirting", Skirting);
            skirting.raycastTarget = false;
            Place(skirting.rectTransform, 0f, 1f, 0.3f, 0.3f);
            skirting.rectTransform.sizeDelta = new Vector2(0f, width * 0.012f);

            Place(UiKit.Glow(root, "Glow", style.Tint(0.72f, 0.16f, 0.38f)).rectTransform, 0.14f, 0.86f, 0.2f, 1f);

            Place(UiKit.Glow(root, "Stand Shadow", new Color(0f, 0f, 0f, 0.5f)).rectTransform, 0.36f, 0.64f, 0.13f, 0.21f);
            var stand = UiKit.Panel(root, "Stand", Stand);
            stand.raycastTarget = false;
            Place(stand.rectTransform, 0.488f, 0.512f, 0.17f, 0.5f);

            var frame = UiKit.Card(root, "Panel", Frame, width * 0.032f, shadow: true);
            frame.raycastTarget = false;
            frame.GetComponent<SoftShadow>().color = new Color(0f, 0f, 0f, 0.45f);
            Place(frame.rectTransform, 0.27f, 0.73f, 0.39f, 0.85f);

            var border = width * 0.008f;
            var corner = width * 0.032f - border;
            var screen = UiKit.Card(frame.transform, "Screen", Screen, corner);
            screen.raycastTarget = false;
            UiKit.Stretch(screen.rectTransform, border, border, border, border);

            // A title bar across the top fifth of the screen. It is a rounded card, so a patch in the screen's own
            // colour squares off its lower corners.
            var bar = UiKit.Card(screen.transform, "Title Bar", style.Tint(0.5f, 0.09f, 0.55f), corner);
            bar.raycastTarget = false;
            Place(bar.rectTransform, 0f, 1f, 0.8f, 1f);
            bar.rectTransform.offsetMin = new Vector2(0f, -corner);
            var patch = UiKit.Panel(screen.transform, "Patch", Screen);
            patch.raycastTarget = false;
            Place(patch.rectTransform, 0f, 1f, 0.8f, 0.8f);
            patch.rectTransform.offsetMin = new Vector2(0f, -corner);

            var icon = UiKit.Icon(screen.transform, sample.icon, width * 0.115f, style.Tint(0.85f, 0.13f)).rectTransform;
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.4f);
            icon.anchoredPosition = Vector2.zero;

            var number = UiKit.Label(root, sample.number.ToString("00"), width * 0.05f, new Color(1f, 1f, 1f, 0.6f), FontStyles.Bold);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.rectTransform.anchorMin = number.rectTransform.anchorMax = number.rectTransform.pivot = new Vector2(0f, 1f);
            number.rectTransform.anchoredPosition = new Vector2(width * 0.045f, -width * 0.035f);
            number.rectTransform.sizeDelta = new Vector2(width * 0.2f, width * 0.07f);
            return root;
        }

        private static void Shade(Graphic graphic, Color top, Color bottom)
        {
            var gradient = graphic.gameObject.AddComponent<UiGradient>();
            gradient.top = top;
            gradient.bottom = bottom;
        }

        /// <summary>Anchors a rect to a part of its parent, as fractions from its left and bottom edges.</summary>
        private static void Place(RectTransform rect, float xMin, float xMax, float yMin, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
