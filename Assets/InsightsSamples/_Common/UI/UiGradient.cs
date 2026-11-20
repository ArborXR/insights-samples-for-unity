using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Shades a graphic from one colour at its top to another at its bottom, through its vertex colours, so a
    /// rounded UiKit card keeps its corners.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiGradient : BaseMeshEffect
    {
        public Color top = Color.white;
        public Color bottom = Color.white;

        private static readonly List<UIVertex> Vertices = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var rect = ((RectTransform)transform).rect;
            if (rect.height <= 0f) return;

            Vertices.Clear();
            vh.GetUIVertexStream(Vertices);
            for (var i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i];
                vertex.color = Color.Lerp(bottom, top, Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y));
                Vertices[i] = vertex;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Vertices);
        }
    }
}
