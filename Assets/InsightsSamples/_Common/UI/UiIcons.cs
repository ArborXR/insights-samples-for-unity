using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The shell's icons, drawn at runtime into one texture from the outlines in UiIconData.cs. They are
    /// Lucide icons (see the README's License section); ArtSource/Icons/build_icons.py adds more.
    /// </summary>
    public static partial class UiIcons
    {
        private const int Cell = 96;
        private const int Columns = 8;
        private const int Grid = 24;
        private const float PixelsPerUnit = 3f;
        // Lucide strokes are 2 units wide.
        private const float HalfStroke = PixelsPerUnit;
        // Each sprite takes this much of its cell's margin, so a stroke on the grid's edge keeps its soft edge.
        private const int Bleed = 2;

        private static Texture2D _texture;
        private static Dictionary<string, Sprite> _sprites;

        public static bool Has(string name) => !string.IsNullOrEmpty(name) && Sprites.ContainsKey(name);

        public static Sprite Get(string name) =>
            !string.IsNullOrEmpty(name) && Sprites.TryGetValue(name, out var sprite) ? sprite : null;

        private static Dictionary<string, Sprite> Sprites
        {
            get
            {
                if (_texture == null) Build();
                return _sprites;
            }
        }

        private static void Build()
        {
            var count = Data.Length / 2;
            var rows = (count + Columns - 1) / Columns;
            int width = Cell * Columns, height = Cell * rows;
            var margin = (Cell - Grid * PixelsPerUnit) / 2f;

            // Distance from each pixel to the nearest stroke; the stroke's edge is antialiased from it.
            var distance = new float[width * height];
            for (var i = 0; i < distance.Length; i++) distance[i] = float.MaxValue;

            for (var icon = 0; icon < count; icon++)
            {
                // SVG's y runs down and a texture's runs up, so each cell is drawn from its top edge.
                var origin = new Vector2(icon % Columns * Cell + margin, height - icon / Columns * Cell - margin);
                foreach (var stroke in Data[icon * 2 + 1].Split('/'))
                {
                    var numbers = stroke.Split(' ');
                    var from = Point(numbers, 0, origin);
                    if (numbers.Length == 2) Stamp(distance, width, height, from, from);
                    for (var n = 2; n < numbers.Length; n += 2)
                    {
                        var to = Point(numbers, n, origin);
                        Stamp(distance, width, height, from, to);
                        from = to;
                    }
                }
            }

            var pixels = new Color32[distance.Length];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(HalfStroke - distance[i] + 0.5f) * 255));

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, true)
            {
                name = "UiIcons", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(true, true);

            _sprites = new Dictionary<string, Sprite>(count);
            var size = Grid * PixelsPerUnit + 2 * Bleed;
            for (var icon = 0; icon < count; icon++)
            {
                var rect = new Rect(icon % Columns * Cell + margin - Bleed, height - (icon / Columns + 1) * Cell + margin - Bleed, size, size);
                var sprite = Sprite.Create(_texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = Data[icon * 2];
                _sprites[Data[icon * 2]] = sprite;
            }
        }

        private static Vector2 Point(string[] numbers, int index, Vector2 origin) => new Vector2(
            origin.x + int.Parse(numbers[index], CultureInfo.InvariantCulture) / 10f * PixelsPerUnit,
            origin.y - int.Parse(numbers[index + 1], CultureInfo.InvariantCulture) / 10f * PixelsPerUnit);

        /// <summary>Lowers the distance field around one segment of a stroke.</summary>
        private static void Stamp(float[] distance, int width, int height, Vector2 from, Vector2 to)
        {
            var reach = HalfStroke + 1f;
            var xMin = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.x, to.x) - reach));
            var xMax = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(from.x, to.x) + reach));
            var yMin = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(from.y, to.y) - reach));
            var yMax = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(from.y, to.y) + reach));

            var edge = to - from;
            var lengthSquared = edge.sqrMagnitude;
            for (var y = yMin; y <= yMax; y++)
            for (var x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f) - from;
                var t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(point, edge) / lengthSquared) : 0f;
                var d = (point - t * edge).magnitude;
                var index = y * width + x;
                if (d < distance[index]) distance[index] = d;
            }
        }
    }
}
