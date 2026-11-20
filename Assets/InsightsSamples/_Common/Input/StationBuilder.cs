using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Builds simple station parts at runtime: labelled buttons, panels, and text. Stations face -Z,
    /// toward the station camera.
    /// </summary>
    public static class StationBuilder
    {
        public static StationInteractable Button(Transform parent, string label, Vector3 localPosition, Vector2 size,
            Material material, Color color, float fontSize = 0.6f)
        {
            var root = new GameObject($"{label} Button").transform;
            root.SetParent(parent, false);
            root.localPosition = localPosition;

            var radius = Mathf.Min(0.025f, size.y / 3f);
            var body = RoundedBlock(root, "Body", Vector3.zero, new Vector3(size.x, size.y, 0.06f), radius, material);
            // A darker skirt just behind and below the key shows a lip even head-on, where the key's own sides are hidden.
            RoundedBlock(root, "Skirt", new Vector3(0, -0.005f, 0.012f), new Vector3(size.x + 0.01f, size.y + 0.01f, 0.04f), radius + 0.005f, material,
                Color.Lerp(color, Color.black, 0.6f));
            var interactable = Interactable(root.gameObject, label, new Vector3(size.x, size.y, 0.08f), body, color, dimWhenInactive: true);

            var text = Text(root, label, new Vector3(0, 0, -0.035f), fontSize, Color.white, size.x * 0.92f, size.y);
            text.fontStyle = FontStyles.Bold;
            return interactable;
        }

        /// <summary>Makes a prop clickable. Hover and flash tint the <paramref name="tinted"/> block.</summary>
        public static StationInteractable Interactable(GameObject prop, string label, Vector3 colliderSize, GameObject tinted,
            Color color, Vector3 colliderCenter = default, bool dimWhenInactive = false)
        {
            // The collider has to exist first: StationInteractable requires one, and Unity can't add the
            // abstract Collider type on its own.
            var collider = prop.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = colliderCenter;
            Tint(tinted, color);
            var interactable = prop.AddComponent<StationInteractable>();
            interactable.Configure(label, tinted.GetComponent<Renderer>(), color, dimWhenInactive);
            return interactable;
        }

        public static void SetLabel(StationInteractable button, string label) =>
            button.GetComponentInChildren<TextMeshPro>().text = label;

        public static Transform Group(Transform parent, string name, Vector3 localPosition)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = localPosition;
            return group;
        }

        public static GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Color color)
        {
            var block = Block(parent, name, localPosition, size, material);
            Tint(block, color);
            return block;
        }

        public static GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material) =>
            Shape(PrimitiveType.Cube, parent, name, localPosition, size, material);

        /// <summary>A cylinder standing on its axis: size.y is its full height, x and z its diameters.</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Color color)
        {
            // Unity's cylinder primitive is 2 units tall.
            var cylinder = Shape(PrimitiveType.Cylinder, parent, name, localPosition, new Vector3(size.x, size.y / 2f, size.z), material);
            Tint(cylinder, color);
            return cylinder;
        }

        private static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(type);
            block.name = name;
            Object.Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        /// <summary>
        /// A display on a two-post floor stand: a navy screen inset in a dark rounded bezel, centred on
        /// <paramref name="root"/>. Put text and buttons in front of it, at z -0.02 or less.
        /// </summary>
        public static GameObject Display(Transform root, Vector2 size, Material material, bool stand = true)
        {
            // A deep dark bezel on a thin steel rim, with a slimmer housing behind, so the edges read as a solid screen.
            RoundedBlock(root, "Bezel", new Vector3(0, 0, 0.011f), new Vector3(size.x + 0.06f, size.y + 0.06f, 0.05f), 0.035f, material, PropColors.Bezel);
            RoundedBlock(root, "Rim", new Vector3(0, 0, 0.022f), new Vector3(size.x + 0.072f, size.y + 0.072f, 0.034f), 0.041f, material, PropColors.DarkSteel);
            RoundedBlock(root, "Housing", new Vector3(0, 0, 0.055f), new Vector3(size.x - 0.04f, size.y - 0.04f, 0.04f), 0.05f, material, PropColors.DarkSteel);
            var screen = RoundedBlock(root, "Screen", new Vector3(0, 0, -0.016f), new Vector3(size.x, size.y, 0.004f), 0.02f, material, UiKit.Navy);
            if (!stand) return screen;

            var height = root.position.y;
            foreach (var x in new[] { -size.x * 0.3f, size.x * 0.3f })
            {
                Block(root, "Stand Post", new Vector3(x, -height / 2f - 0.05f, 0.05f), new Vector3(0.05f, height - 0.1f, 0.05f), material, PropColors.DarkSteel);
                Block(root, "Stand Foot", new Vector3(x, -height + 0.015f, 0.05f), new Vector3(0.08f, 0.03f, 0.5f), material, PropColors.DarkSteel);
            }
            Block(root, "Stand Rail", new Vector3(0, -size.y / 2f - 0.12f, 0.05f), new Vector3(size.x * 0.6f + 0.05f, 0.04f, 0.04f), material, PropColors.DarkSteel);
            return screen;
        }

        /// <summary>
        /// A freestanding kiosk: a dark rounded head with an inset navy screen, on a light pedestal with a navy stripe
        /// and a floor plate. Each station gives its own head and screen size; put text and buttons in front of the head.
        /// </summary>
        public static GameObject Kiosk(Transform root, Vector3 headCentre, Vector3 headSize, Vector3 screenCentre, Vector2 screenSize, Material material)
        {
            RoundedBlock(root, "Kiosk Head", headCentre, headSize, 0.035f, material, PropColors.Bezel);
            // As on Display: a steel rim set back from the head's face, and a slimmer housing behind, so its edges read.
            var rimDepth = headSize.z * 0.7f;
            RoundedBlock(root, "Kiosk Rim", headCentre + new Vector3(0, 0, (headSize.z - rimDepth) / 2f + 0.002f),
                new Vector3(headSize.x + 0.012f, headSize.y + 0.012f, rimDepth), 0.041f, material, PropColors.DarkSteel);
            RoundedBlock(root, "Kiosk Housing", headCentre + new Vector3(0, 0, headSize.z / 2f + 0.02f),
                new Vector3(headSize.x - 0.08f, headSize.y - 0.08f, 0.04f), 0.05f, material, PropColors.DarkSteel);
            var screen = RoundedBlock(root, "Screen", screenCentre, new Vector3(screenSize.x, screenSize.y, 0.004f), 0.015f, material, UiKit.Navy);

            var top = headCentre.y - headSize.y / 2f + 0.02f;
            var width = Mathf.Clamp(headSize.x * 0.3f, 0.22f, 0.4f);
            var z = headCentre.z + 0.06f;
            Block(root, "Kiosk Pedestal", new Vector3(headCentre.x, (top + 0.02f) / 2f, z), new Vector3(width, top - 0.02f, 0.2f), material, PropColors.Panel);
            Block(root, "Kiosk Stripe", new Vector3(headCentre.x, (top + 0.02f) / 2f, z - 0.101f), new Vector3(0.035f, top - 0.06f, 0.002f), material, UiKit.Navy);
            Block(root, "Kiosk Base", new Vector3(headCentre.x, 0.011f, z), new Vector3(width + 0.24f, 0.022f, 0.46f), material, PropColors.Panel);
            foreach (var x in new[] { -1f, 1f })
            foreach (var dz in new[] { -0.19f, 0.19f })
                Cylinder(root, "Kiosk Foot", new Vector3(headCentre.x + x * (width / 2f + 0.09f), 0.004f, z + dz), new Vector3(0.04f, 0.008f, 0.04f), material, PropColors.DarkSteel);
            return screen;
        }

        /// <summary>A box whose corners are rounded as seen from the front (-Z), like a screen bezel or a key cap.</summary>
        public static GameObject RoundedBlock(Transform parent, string name, Vector3 localPosition, Vector3 size, float radius, Material material, Color color)
        {
            var block = RoundedBlock(parent, name, localPosition, size, radius, material);
            Tint(block, color);
            return block;
        }

        public static GameObject RoundedBlock(Transform parent, string name, Vector3 localPosition, Vector3 size, float radius, Material material)
        {
            var block = new GameObject(name);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.AddComponent<MeshFilter>().sharedMesh = RoundedBoxMesh(size, radius);
            block.AddComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        private static readonly Dictionary<(Vector3, float), Mesh> RoundedMeshes = new Dictionary<(Vector3, float), Mesh>();

        private static Mesh RoundedBoxMesh(Vector3 size, float radius)
        {
            if (RoundedMeshes.TryGetValue((size, radius), out var cached) && cached != null) return cached;

            const int cornerSegments = 6;
            var r = Mathf.Min(radius, size.x / 2f, size.y / 2f);
            var inner = new Vector2(size.x / 2f - r, size.y / 2f - r);
            var centres = new[] { inner, new Vector2(-inner.x, inner.y), -inner, new Vector2(inner.x, -inner.y) };
            var outline = new List<Vector2>();
            var outward = new List<Vector2>();
            for (var c = 0; c < 4; c++)
            for (var i = 0; i <= cornerSegments; i++)
            {
                var angle = (c * 90f + 90f * i / cornerSegments) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                outline.Add(centres[c] + direction * r);
                outward.Add(direction);
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            void Triangle(int a, int b, int c, Vector3 facing)
            {
                // Unity treats a triangle as front-facing where Cross(b - a, c - a) points at the viewer.
                if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), facing) < 0) (b, c) = (c, b);
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }

            var n = outline.Count;
            var depth = size.z / 2f;
            foreach (var (z, normal) in new[] { (-depth, Vector3.back), (depth, Vector3.forward) })
            {
                var centre = vertices.Count;
                vertices.Add(new Vector3(0, 0, z));
                normals.Add(normal);
                foreach (var p in outline)
                {
                    vertices.Add(new Vector3(p.x, p.y, z));
                    normals.Add(normal);
                }
                for (var i = 0; i < n; i++) Triangle(centre, centre + 1 + i, centre + 1 + (i + 1) % n, normal);
            }
            var side = vertices.Count;
            for (var i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -depth));
                vertices.Add(new Vector3(outline[i].x, outline[i].y, depth));
                normals.Add(outward[i]);
                normals.Add(outward[i]);
            }
            for (var i = 0; i < n; i++)
            {
                var a = side + i * 2;
                var b = side + (i + 1) % n * 2;
                var facing = (Vector3)(outward[i] + outward[(i + 1) % n]);
                Triangle(a, a + 1, b + 1, facing);
                Triangle(a, b + 1, b, facing);
            }

            var mesh = new Mesh { name = $"Rounded Box {size.x:0.###}x{size.y:0.###}x{size.z:0.###}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            RoundedMeshes[(size, radius)] = mesh;
            return mesh;
        }

        public static void Tint(GameObject block, Color color)
        {
            var renderer = block.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
        }

        /// <summary>Lights or dims an indicator. The glow only shows on materials with emission enabled.</summary>
        public static void Glow(GameObject lamp, Color color, bool lit)
        {
            var renderer = lamp.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", lit ? color : Color.Lerp(PropColors.Off, color, 0.25f));
            properties.SetColor("_EmissionColor", lit ? color * 1.5f : Color.black);
            renderer.SetPropertyBlock(properties);
        }

        /// <summary>An imported model, placed like <see cref="Block"/>: centred on <paramref name="localPosition"/>.</summary>
        public static GameObject Model(Transform parent, string name, GameObject model, Vector3 localPosition, Vector3 scale)
        {
            var instance = Object.Instantiate(model, parent, false);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localScale = scale;
            return instance;
        }

        public static TextMeshPro Text(Transform parent, string text, Vector3 localPosition, float fontSize, Color color,
            float width, float height, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<TextMeshPro>();
            if (UiKit.Font != null) label.font = UiKit.Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return label;
        }
    }
}
