using System.Collections.Generic;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// A belt conveyor along X with a motor and running lamp. The belt and anything riding on it move while
    /// it runs.
    /// </summary>
    public class Conveyor : MonoBehaviour
    {
        /// <summary>Carton.fbx is a cube this size; carton models are scaled to the size asked for.</summary>
        public const float CartonModelSize = 0.3f;

        // Carton.fbx's texture holds this many print variants side by side; each carton shows the next one.
        private const int CartonVariants = 3;

        private const float Speed = 0.4f;

        // Conveyor.fbx is authored at this length, with one belt texture tile per half metre.
        private const float ModelLength = 2.6f;
        private const float BeltTilesPerMetre = 2f;
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<Transform> _riding = new List<Transform>();
        private Material _material;
        private readonly List<GameObject> _lamps = new List<GameObject>();
        private Renderer _belt;
        private MaterialPropertyBlock _beltProperties;
        private float _beltOffset;
        private float _length;
        private int _cartons;
        private bool _running = true;

        public bool Running
        {
            get => _running;
            set
            {
                _running = value;
                foreach (var lamp in _lamps) StationBuilder.Glow(lamp, PropColors.SafetyGreen, value);
            }
        }

        /// <summary>
        /// Builds the conveyor from <paramref name="model"/> (Conveyor.fbx) when given, otherwise from blocks.
        /// </summary>
        public static Conveyor Build(Transform parent, Vector3 localPosition, float length, Material material, GameObject model = null)
        {
            var root = StationBuilder.Group(parent, "Conveyor", localPosition);
            var conveyor = root.gameObject.AddComponent<Conveyor>();
            conveyor._material = material;
            conveyor._length = length;
            if (model != null) conveyor.BuildFromModel(model);
            else conveyor.BuildFrame();
            return conveyor;
        }

        /// <summary>A carton on the belt at <paramref name="x"/>; it rides along while the conveyor runs.</summary>
        public GameObject AddCarton(float x, Vector3 size, bool riding = true, GameObject model = null)
        {
            var position = new Vector3(x, 0.725f + size.y / 2f, 0);
            var carton = model != null
                ? StationBuilder.Model(transform, "Carton", model, position, size / CartonModelSize)
                : StationBuilder.Block(transform, "Carton", position, size, _material, PropColors.Cardboard);
            if (model != null) ShowCartonVariant(carton.GetComponent<Renderer>(), _cartons++);
            if (riding) _riding.Add(carton.transform);
            return carton;
        }

        /// <summary>Shows print variant <paramref name="index"/> of Carton.fbx's texture, shading every other carton a little darker.</summary>
        public static void ShowCartonVariant(Renderer carton, int index)
        {
            var properties = new MaterialPropertyBlock();
            carton.GetPropertyBlock(properties);
            properties.SetVector(BaseMapSt, new Vector4(1f, 1f, (float)(index % CartonVariants) / CartonVariants, 0f));
            // Every other carton a shade darker, so a row of them doesn't look cloned.
            var shade = index % 2 == 0 ? 1f : 0.93f;
            var tint = PropColors.Textured;
            properties.SetColor(BaseColorId, new Color(tint.r * shade, tint.g * shade, tint.b * shade));
            carton.SetPropertyBlock(properties);
        }

        /// <summary>Also shows the running state on <paramref name="lamp"/>, such as a stack light's green lens.</summary>
        public void AddRunningLamp(GameObject lamp)
        {
            _lamps.Add(lamp);
            Running = _running;
        }

        private void Update()
        {
            if (!_running) return;
            if (_belt != null) ScrollBelt();
            foreach (var item in _riding)
            {
                var position = item.localPosition;
                position.x += Speed * Time.deltaTime;
                if (position.x > _length / 2f) position.x -= _length;
                item.localPosition = position;
            }
        }

        private void BuildFromModel(GameObject model)
        {
            var instance = Instantiate(model, transform, false).transform;
            instance.name = "Conveyor Model";
            var stretch = new Vector3(_length / ModelLength, 1f, 1f);
            instance.Find("Conveyor Frame").localScale = stretch;
            instance.Find("Conveyor Belt").localScale = stretch;
            // The drive and its control box hang off the head drum, so they move with the length instead of stretching.
            var head = new Vector3(_length / 2f, 0, 0);
            instance.Find("Conveyor Drive").localPosition = head;
            var lamp = instance.Find("Conveyor Running Lamp");
            lamp.localPosition = head;

            _belt = instance.Find("Conveyor Belt").GetComponent<Renderer>();
            _beltProperties = new MaterialPropertyBlock();
            AddRunningLamp(lamp.gameObject);
        }

        private void ScrollBelt()
        {
            // The belt's U runs toward +X along its top, so a falling offset carries the ribs the way cartons ride.
            _beltOffset = Mathf.Repeat(_beltOffset - Speed * BeltTilesPerMetre * ModelLength / _length * Time.deltaTime, 1f);
            _belt.GetPropertyBlock(_beltProperties);
            _beltProperties.SetVector(BaseMapSt, new Vector4(1f, 1f, _beltOffset, 0f));
            _belt.SetPropertyBlock(_beltProperties);
        }

        private void BuildFrame()
        {
            Block("Belt", new Vector3(0, 0.7f, 0), new Vector3(_length, 0.05f, 0.5f), PropColors.Rubber);
            Block("Rail Front", new Vector3(0, 0.72f, -0.27f), new Vector3(_length + 0.1f, 0.09f, 0.04f), PropColors.Steel);
            Block("Rail Back", new Vector3(0, 0.72f, 0.27f), new Vector3(_length + 0.1f, 0.09f, 0.04f), PropColors.Steel);
            foreach (var x in new[] { -_length / 2f + 0.1f, _length / 2f - 0.1f })
            foreach (var z in new[] { -0.22f, 0.22f })
                Block("Leg", new Vector3(x, 0.34f, z), new Vector3(0.06f, 0.68f, 0.06f), PropColors.DarkSteel);

            var slats = Mathf.RoundToInt(_length / 0.22f);
            for (var i = 0; i < slats; i++)
                _riding.Add(Block("Slat", new Vector3(-_length / 2f + i * _length / slats, 0.73f, 0), new Vector3(0.05f, 0.012f, 0.46f), PropColors.DarkSteel).transform);

            var motor = new Vector3(_length / 2f + 0.2f, 0.55f, 0);
            Block("Motor", motor, new Vector3(0.34f, 0.32f, 0.38f), UiKit.FromHex(0x3D5A80));
            _lamps.Add(Block("Running Lamp", motor + new Vector3(0, 0.2f, 0), new Vector3(0.08f, 0.08f, 0.08f), PropColors.SafetyGreen));
        }

        private GameObject Block(string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(transform, name, localPosition, size, _material, color);
    }
}
