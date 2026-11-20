using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InsightsSamples.Common
{
    public enum ButtonLook
    {
        Primary,
        Secondary,
        Ghost,
    }

    /// <summary>
    /// Builds screen-space UI in code. Stations use the ArborXR brand palette here; the shell around them uses
    /// the dark <see cref="Theme"/>.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Navy = FromHex(0x0F233F);
        public static readonly Color NavyLight = FromHex(0x173763);
        public static readonly Color NavyLighter = FromHex(0x24497A);
        public static readonly Color Purple = FromHex(0x5A58EB);
        public static readonly Color PurpleLight = FromHex(0xEDEDFD);
        public static readonly Color Lavender = FromHex(0xC9C8FF);
        public static readonly Color Green = FromHex(0x05DA98);
        public static readonly Color Amber = FromHex(0xF5B841);
        public static readonly Color Red = FromHex(0xE5484D);
        public static readonly Color Gray = FromHex(0x959CA1);
        public static readonly Color Ink = FromHex(0x242A35);
        public static readonly Color Body = FromHex(0x394455);
        public static readonly Color White = Color.white;
        public static readonly Color MutedOnDark = new Color(1f, 1f, 1f, 0.72f);
        public static readonly Color FaintOnDark = new Color(1f, 1f, 1f, 0.5f);

        // Shape texture: a circle (9-sliced into rounded corners of any radius) and a soft-shadow falloff, side by side.
        private const int ShapeCell = 64;
        private const float ShapeCorner = ShapeCell / 2f;
        // A sliced image reports its sprite's borders to layout groups as its preferred size; a high pixels-per-unit
        // keeps that at 8 units, so rounded panels size to their content.
        private const float ShapePixelsPerUnit = 800f;
        private static Texture2D _shapes;
        private static Sprite _rounded;
        private static Sprite _glow;
        private static readonly Dictionary<int, Sprite> Rings = new Dictionary<int, Sprite>();
        private static TMP_FontAsset _font;
        private static TMP_FontAsset _fontMedium;

        /// <summary>Roboto, the ArborXR brand typeface, for screen and in-world text.</summary>
        public static TMP_FontAsset Font => _font != null ? _font : _font = Resources.Load<TMP_FontAsset>("Fonts/Roboto SDF");

        /// <summary>Roboto Medium, for the shell's names and buttons.</summary>
        public static TMP_FontAsset FontMedium =>
            _fontMedium != null ? _fontMedium : _fontMedium = Resources.Load<TMP_FontAsset>("Fonts/Roboto Medium SDF");

        /// <summary>Where the shadow falloff sits in the shape texture, for <see cref="SoftShadow"/>.</summary>
        public static Rect ShadowUv => new Rect(0.5f, 0f, 0.5f, 1f);

        public static Canvas CreateScreenCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>A panel with rounded corners and, optionally, a soft drop shadow.</summary>
        public static Image Card(Transform parent, string name, Color color, float radius, bool shadow = false)
        {
            var image = Panel(parent, name, color);
            Round(image, radius);
            if (shadow) image.gameObject.AddComponent<SoftShadow>().radius = radius;
            return image;
        }

        /// <summary>Rounds an image's corners. A radius of half its height or more makes a pill or a circle.</summary>
        public static void Round(Image image, float radius)
        {
            if (_rounded == null) BuildShapes();
            image.sprite = _rounded;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ShapeCorner * 100f / ShapePixelsPerUnit / Mathf.Max(radius, 0.5f);
        }

        private static void BuildShapes()
        {
            _shapes = new Texture2D(ShapeCell * 2, ShapeCell, TextureFormat.RGBA32, false)
            {
                name = "UiKit Shapes", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[ShapeCell * 2 * ShapeCell];
            for (var y = 0; y < ShapeCell; y++)
            for (var x = 0; x < ShapeCell; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var inside = ShapeCorner - Vector2.Distance(p, new Vector2(ShapeCorner, ShapeCorner));
                pixels[y * ShapeCell * 2 + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(inside + 0.5f) * 255));

                // SoftShadow maps each quarter of this cell onto a corner (blur + radius) square, with the blur twice
                // the card's radius. In units of the blur, the distance outside the card's rounded corner is:
                var t = new Vector2(1 - Mathf.Abs(p.x / ShapeCell - 0.5f) * 2, 1 - Mathf.Abs(p.y / ShapeCell - 0.5f) * 2);
                var outside = ((new Vector2(1 - t.x, 1 - t.y) * 3f).magnitude - 1f) / 2f;
                var alpha = outside <= 0 ? 1f : Mathf.Pow(1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(outside)), 1.6f);
                pixels[y * ShapeCell * 2 + ShapeCell + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            _shapes.SetPixels32(pixels);
            _shapes.Apply(false, true);
            var border = new Vector4(ShapeCorner, ShapeCorner, ShapeCorner, ShapeCorner);
            _rounded = Sprite.Create(_shapes, new Rect(0, 0, ShapeCell, ShapeCell), new Vector2(0.5f, 0.5f), ShapePixelsPerUnit, 0,
                SpriteMeshType.FullRect, border);
        }

        /// <summary>
        /// A mouse-pointer arrow, drawn at runtime: fill color with a white outline. Its pivot is the tip, so its
        /// position is the point it points at.
        /// </summary>
        public static Image Pointer(Transform parent, string name, Color fill, float size)
        {
            // The classic arrow, in a 26 x 42 box with y down.
            var shape = new[]
            {
                new Vector2(0, 0), new Vector2(0, 34), new Vector2(8, 27), new Vector2(14, 41),
                new Vector2(20, 38.5f), new Vector2(14, 25), new Vector2(25, 25),
            };
            const int scale = 4;
            const float outline = 2.2f;
            int columns = 30 * scale, rows = 46 * scale;
            var texture = new Texture2D(columns, rows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[columns * rows];
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f) / scale - new Vector2(2, 2);
                var inside = Inside(shape, point);
                var distance = EdgeDistance(shape, point);
                Color color = inside && distance > outline ? fill : (inside || distance <= outline) ? Color.white : Color.clear;
                // Texture rows run bottom-up.
                pixels[(rows - 1 - y) * columns + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = Sprite.Create(texture, new Rect(0, 0, columns, rows), new Vector2(2f / 30, 44f / 46));
            image.raycastTarget = false;
            image.rectTransform.pivot = new Vector2(2f / 30, 44f / 46);
            image.rectTransform.sizeDelta = new Vector2(size * 30 / 46, size);
            return image;
        }

        private static bool Inside(Vector2[] polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
                    && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                    inside = !inside;
            }
            return inside;
        }

        private static float EdgeDistance(Vector2[] polygon, Vector2 point)
        {
            var nearest = float.MaxValue;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var edge = polygon[i] - polygon[j];
                var t = Mathf.Clamp01(Vector2.Dot(point - polygon[j], edge) / edge.sqrMagnitude);
                nearest = Mathf.Min(nearest, Vector2.Distance(point, polygon[j] + t * edge));
            }
            return nearest;
        }

        public static RectTransform Column(Transform parent, string name, float spacing, RectOffset padding = null)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static RectTransform Row(Transform parent, string name, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static void FitHeight(RectTransform rect) =>
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var label = Rect(parent, "Label").gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null) label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.richText = true;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A one-line label in Roboto Medium.</summary>
        public static TextMeshProUGUI Caption(Transform parent, string text, float size, Color color)
        {
            var label = Label(parent, text, size, color);
            if (FontMedium != null) label.font = FontMedium;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        /// <summary>Stops a layout group from squeezing a one-line label. Call it again after changing the text.</summary>
        public static TextMeshProUGUI Rigid(TextMeshProUGUI label)
        {
            var element = label.GetComponent<LayoutElement>();
            if (element == null) element = label.gameObject.AddComponent<LayoutElement>();
            element.minWidth = label.GetPreferredValues(label.text).x;
            return label;
        }

        /// <summary>Limits a wrapping label to a number of lines at the given width, ending it with an ellipsis.</summary>
        public static void ClampLines(TextMeshProUGUI label, float width, int lines)
        {
            label.overflowMode = TextOverflowModes.Ellipsis;
            var tallest = "Ag";
            for (var line = 1; line < lines; line++) tallest += "\nAg";
            var limit = label.GetPreferredValues(tallest, width, 0f).y;
            // Measured last, so the label is left holding its own text.
            var full = label.GetPreferredValues(label.text, width, 0f).y;
            var element = label.GetComponent<LayoutElement>();
            if (element == null) element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = Mathf.Min(full, limit);
        }

        /// <summary>A hairline border around a rounded panel. Add it after the panel's content, so it draws on top.</summary>
        public static Image Outline(Transform parent, float radius, Color color, float width = 1f)
        {
            var image = Panel(parent, "Outline", color);
            Stretch(image.rectTransform);
            image.raycastTarget = false;
            image.sprite = Ring(radius, width);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = ShapeCorner * 100f / ShapePixelsPerUnit / Mathf.Max(radius, 0.5f);
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return image;
        }

        private static Sprite Ring(float radius, float width)
        {
            // The sprite's corner stretches from ShapeCorner texels to the radius, so the line is drawn that much thicker.
            var key = Mathf.RoundToInt(Mathf.Clamp(width * ShapeCorner / Mathf.Max(radius, 1f), 1f, ShapeCorner) * 8f);
            if (Rings.TryGetValue(key, out var ring) && ring != null) return ring;

            var texels = key / 8f;
            var texture = new Texture2D(ShapeCell, ShapeCell, TextureFormat.RGBA32, false)
            {
                name = "UiKit Ring", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[ShapeCell * ShapeCell];
            for (var y = 0; y < ShapeCell; y++)
            for (var x = 0; x < ShapeCell; x++)
            {
                var inside = ShapeCorner - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(ShapeCorner, ShapeCorner));
                var alpha = Mathf.Clamp01(inside + 0.5f) * Mathf.Clamp01(texels - inside + 0.5f);
                pixels[y * ShapeCell + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = new Vector4(ShapeCorner, ShapeCorner, ShapeCorner, ShapeCorner);
            ring = Sprite.Create(texture, new Rect(0, 0, ShapeCell, ShapeCell), new Vector2(0.5f, 0.5f), ShapePixelsPerUnit, 0,
                SpriteMeshType.FullRect, border);
            Rings[key] = ring;
            return ring;
        }

        /// <summary>A soft round glow that fades to nothing at its edge, stretched over its rect.</summary>
        public static Image Glow(Transform parent, string name, Color color)
        {
            if (_glow == null)
            {
                var texture = new Texture2D(ShapeCell, ShapeCell, TextureFormat.RGBA32, false)
                {
                    name = "UiKit Glow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[ShapeCell * ShapeCell];
                for (var y = 0; y < ShapeCell; y++)
                for (var x = 0; x < ShapeCell; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(ShapeCorner, ShapeCorner)) / ShapeCorner;
                    pixels[y * ShapeCell + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(Mathf.Clamp01(1f - distance), 1.5f) * 255));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _glow = Sprite.Create(texture, new Rect(0, 0, ShapeCell, ShapeCell), new Vector2(0.5f, 0.5f));
            }

            var image = Panel(parent, name, color);
            image.sprite = _glow;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>An icon from <see cref="UiIcons"/>, <paramref name="size"/> units across.</summary>
        public static Image Icon(Transform parent, string icon, float size, Color color)
        {
            var image = Panel(parent, $"{icon} Icon", color);
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = element.minHeight = element.preferredHeight = size;
            SetIcon(image, icon);
            return image;
        }

        public static void SetIcon(Image image, string icon)
        {
            image.sprite = UiIcons.Get(icon);
            image.enabled = image.sprite != null;
        }

        /// <summary>A thin vertical line between groups in a row.</summary>
        public static Image Separator(Transform parent, float height)
        {
            var image = Panel(parent, "Separator", Theme.LineStrong);
            image.raycastTarget = false;
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 1f;
            element.minHeight = element.preferredHeight = height;
            return image;
        }

        /// <summary>Takes up a row's spare width.</summary>
        public static void Spacer(Transform parent) =>
            Rect(parent, "Spacer").gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        /// <summary>A button in the shell's dark theme, with an optional icon before its label.</summary>
        public static Button ShellButton(Transform parent, string label, string icon, Action onClick, ButtonLook look,
            float height = 46f, float fontSize = 17f)
        {
            var (fill, hover, ink) = look switch
            {
                ButtonLook.Primary => (Theme.Accent, Theme.AccentHover, Color.white),
                ButtonLook.Secondary => (new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.11f), Theme.Text),
                _ => (Color.clear, Theme.Hover, Theme.TextMuted),
            };
            var radius = Mathf.Min(12f, height / 2f);
            var image = Card(parent, $"{label} Button", fill, radius);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            image.gameObject.AddComponent<Hover>().Init(image, fill, hover);

            var layout = image.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 0, 0);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;

            if (!string.IsNullOrEmpty(icon)) Icon(image.transform, icon, fontSize, ink);
            Rigid(Caption(image.transform, label, fontSize, ink));
            if (look == ButtonLook.Secondary) Outline(image.transform, radius, Theme.LineStrong);
            return button;
        }

        /// <summary>A square button that shows only an icon.</summary>
        public static Button IconButton(Transform parent, string icon, Action onClick, float size = 44f, float iconSize = 20f)
        {
            var image = Card(parent, $"{icon} Button", Color.clear, size * 0.25f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => onClick());
            image.gameObject.AddComponent<Hover>().Init(image, Color.clear, Theme.Hover);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = element.minHeight = element.preferredHeight = size;

            var glyph = Icon(image.transform, icon, iconSize, Theme.TextMuted).rectTransform;
            glyph.anchorMin = glyph.anchorMax = glyph.pivot = new Vector2(0.5f, 0.5f);
            glyph.anchoredPosition = Vector2.zero;
            return button;
        }

        /// <summary>Shows an icon button as switched on or off, optionally changing its icon.</summary>
        public static void SetToggled(Button button, bool on, string icon = null)
        {
            var glyph = button.transform.GetChild(0).GetComponent<Image>();
            if (icon != null) SetIcon(glyph, icon);
            glyph.color = on ? Theme.SelectedInk : Theme.TextMuted;
            button.GetComponent<Hover>().SetColors(on ? Theme.Selected : Color.clear, on ? Theme.SelectedHover : Theme.Hover);
        }

        /// <summary>Enables or disables a shell button, which dims while it is disabled.</summary>
        public static void SetInteractable(Button button, bool interactable)
        {
            button.interactable = interactable;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = interactable ? 1f : 0.35f;
        }

        /// <summary>A slider from 0 to 1 that takes a row's spare width.</summary>
        public static Slider Slider(Transform parent, float value, Action<float> onChanged)
        {
            const float handleSize = 16f;
            // A clear image, so a press anywhere in the band moves the handle there.
            var band = Panel(parent, "Slider", Color.clear);
            var element = band.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 28f;
            element.minWidth = 80f;
            element.flexibleWidth = 1;

            var track = Card(band.transform, "Track", new Color(1f, 1f, 1f, 0.12f), 3f);
            track.raycastTarget = false;
            Line(track.rectTransform, 0f);

            // The fill stops under the middle of the handle, which stays inside the band at both ends.
            var fillArea = Rect(band.transform, "Fill Area");
            Line(fillArea, handleSize);
            var fill = Card(fillArea, "Fill", Theme.AccentHover, 3f);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = new Vector2(handleSize / 2f, 0f);

            // The slider stretches the handle to its area's height, so the area is as tall as the handle.
            var handleArea = Rect(band.transform, "Handle Area");
            handleArea.anchorMin = new Vector2(0f, 0.5f);
            handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.offsetMin = new Vector2(handleSize / 2f, -handleSize / 2f);
            handleArea.offsetMax = new Vector2(-handleSize / 2f, handleSize / 2f);
            var handle = Card(handleArea, "Handle", Color.white, handleSize / 2f);
            handle.rectTransform.sizeDelta = new Vector2(handleSize, 0f);

            var slider = band.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(changed => onChanged(changed));
            return slider;
        }

        /// <summary>A thin horizontal band across the middle of its parent, stopping short on the right.</summary>
        private static void Line(RectTransform rect, float rightInset)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(0f, -3f);
            rect.offsetMax = new Vector2(-rightInset, 3f);
        }

        /// <summary>
        /// A floating panel over everything in <paramref name="layer"/>, hidden until its parent is activated.
        /// A click anywhere else calls <paramref name="close"/>.
        /// </summary>
        public static RectTransform Popover(Transform layer, string name, Action close)
        {
            var holder = Rect(layer, name);
            Stretch(holder);
            var backdrop = Panel(holder, "Backdrop", Color.clear);
            Stretch(backdrop.rectTransform);
            var button = backdrop.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => close());

            var panel = Card(holder, "Panel", Theme.Popover, 14f, shadow: true);
            panel.GetComponent<SoftShadow>().color = Theme.Shadow;
            holder.gameObject.SetActive(false);
            return panel.rectTransform;
        }

        /// <summary>A vertical scroll view filling its parent; returns the content to add children to.</summary>
        public static RectTransform ScrollList(Transform parent, string name, float spacing, RectOffset padding)
        {
            var viewport = Panel(parent, name, new Color(0, 0, 0, 0));
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var content = Column(viewport.transform, "Content", spacing, padding);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, content.offsetMin.y);
            content.offsetMax = new Vector2(0, 0);
            FitHeight(content);

            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            return content;
        }

        public static void EnsureEventSystem(Transform persistentParent)
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (persistentParent != null) go.transform.SetParent(persistentParent, false);
        }

        public static Color FromHex(uint rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
