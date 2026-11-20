using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// A soft drop shadow behind a rounded UiKit card. It is drawn into the card's own mesh from UiKit's shape
    /// texture, so it follows the card through layout groups and costs no extra draw call.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SoftShadow : BaseMeshEffect
    {
        [Tooltip("The card's corner radius. The shadow's blur is twice this.")]
        public float radius = 12f;
        public Vector2 offset = new Vector2(0, -6);
        public Color color = new Color(0.09f, 0.08f, 0.32f, 0.22f);

        private static readonly List<UIVertex> Card = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            Card.Clear();
            vh.GetUIVertexStream(Card);
            vh.Clear();

            // A 4 x 4 grid of vertices: blur-wide falloff around the card, flat underneath it.
            var rect = ((RectTransform)transform).rect;
            var blur = radius * 2f;
            var uv = UiKit.ShadowUv;
            float[] xs = { rect.xMin - blur, rect.xMin + radius, rect.xMax - radius, rect.xMax + blur };
            float[] ys = { rect.yMin - blur, rect.yMin + radius, rect.yMax - radius, rect.yMax + blur };
            float[] us = { uv.xMin, uv.center.x, uv.center.x, uv.xMax };
            float[] vs = { uv.yMin, uv.center.y, uv.center.y, uv.yMax };
            for (var j = 0; j < 4; j++)
            for (var i = 0; i < 4; i++)
                vh.AddVert(new Vector3(xs[i] + offset.x, ys[j] + offset.y), color, new Vector2(us[i], vs[j]));
            for (var j = 0; j < 3; j++)
            for (var i = 0; i < 3; i++)
            {
                var a = j * 4 + i;
                vh.AddTriangle(a, a + 1, a + 5);
                vh.AddTriangle(a, a + 5, a + 4);
            }

            // The card itself goes on top.
            for (var k = 0; k < Card.Count; k += 3)
            {
                var first = vh.currentVertCount;
                vh.AddVert(Card[k]);
                vh.AddVert(Card[k + 1]);
                vh.AddVert(Card[k + 2]);
                vh.AddTriangle(first, first + 1, first + 2);
            }
        }
    }
}
