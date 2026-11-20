using System;
using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.Telemetry
{
    /// <summary>
    /// A welding table with two plates and a seam between them. The learner drags the torch along the seam from
    /// one yellow mark to the other; the weld passes when it covers the seam and the tip stays on it.
    /// </summary>
    public class WeldStation : MonoBehaviour
    {
        private const float SeamHalfLength = 0.55f;
        private const float PlateTop = 0.885f;
        private const float ToleranceMm = 3f;
        private const float BeadSpacing = 0.012f;
        private const int MaxBeads = 160;

        [SerializeField] private Material material;
        [SerializeField] private GameObject tableModel;
        [SerializeField] private GameObject torchModel;
        [SerializeField] private GameObject welderModel;
        [SerializeField] private GameObject screenModel;

        private readonly List<GameObject> _beads = new List<GameObject>();
        private Transform _torch;
        private Vector3 _torchRest;
        private TextMeshPro _board;
        private Vector3 _lastTip;
        private Vector3 _lastBead;
        private float _minX;
        private float _maxX;
        private int _updates;
        private int _onSeam;

        public event Action WeldStarted;

        public Transform Torch => _torch;
        public StationDraggable TorchHandle { get; private set; }
        public bool Welding => TorchHandle.Dragging;

        /// <summary>How far the torch leans along the direction of travel, in degrees.</summary>
        public float TravelAngle { get; private set; }

        /// <summary>How far the torch tip is from the seam, in millimetres.</summary>
        public float DistanceToSeamMm { get; private set; }

        private void Awake()
        {
            BuildTable(StationBuilder.Group(transform, "Welding Table", Vector3.zero));
            BuildBay();
            BuildTorch();
            BuildBoard(StationBuilder.Group(transform, "Instructions", new Vector3(0.3f, 1.13f, 0.42f)));
            Show("Drag the torch along the seam, from one yellow mark to the other. Keep the tip on the seam.");
        }

        /// <summary>World points along the seam: straight down it, or wandering off it by about 20 mm.</summary>
        public IReadOnlyList<Vector3> SeamPath(bool steady)
        {
            var points = new List<Vector3>();
            for (var i = 0; i <= 10; i++)
            {
                var x = Mathf.Lerp(-SeamHalfLength - 0.02f, SeamHalfLength + 0.02f, i / 10f);
                var z = steady ? 0f : (i % 2 == 0 ? 0.02f : -0.02f);
                points.Add(transform.TransformPoint(new Vector3(x, PlateTop, z)));
            }
            return points;
        }

        private void StartWeld(Ray ray)
        {
            foreach (var bead in _beads) Destroy(bead);
            _beads.Clear();
            _updates = 0;
            _onSeam = 0;
            _minX = float.MaxValue;
            _maxX = float.MinValue;
            TravelAngle = 0;
            _lastTip = _lastBead = TipFor(ray) ?? _torch.localPosition;
            Show("Welding…");
            WeldStarted?.Invoke();
            MoveTorch(ray);
        }

        private void MoveTorch(Ray ray)
        {
            var tip = TipFor(ray);
            if (!tip.HasValue) return;

            var velocity = Time.deltaTime > 0 ? (tip.Value.x - _lastTip.x) / Time.deltaTime : 0;
            TravelAngle = Mathf.Lerp(TravelAngle, Mathf.Clamp(velocity * 45f, -25f, 25f), 0.25f);
            DistanceToSeamMm = Mathf.Abs(tip.Value.z) * 1000f;
            _torch.localPosition = tip.Value;
            _torch.localRotation = Quaternion.Euler(0, 0, -TravelAngle);
            _lastTip = tip.Value;

            var onSeam = DistanceToSeamMm <= ToleranceMm;
            _updates++;
            if (onSeam) _onSeam++;
            // Past a yellow mark counts as reaching it, so coverage doesn't depend on how often the torch updates.
            var along = Mathf.Clamp(tip.Value.x, -SeamHalfLength, SeamHalfLength);
            _minX = Mathf.Min(_minX, along);
            _maxX = Mathf.Max(_maxX, along);
            LayBead(tip.Value, onSeam);
        }

        private void FinishWeld()
        {
            var coverage = _maxX > _minX ? (_maxX - _minX) / (2 * SeamHalfLength) : 0;
            var quality = _updates > 0 ? (float)_onSeam / _updates : 0;
            var passed = coverage >= 0.9f && quality >= 0.8f;
            _torch.localPosition = _torchRest;
            _torch.localRotation = Quaternion.identity;
            Show($"Coverage {coverage * 100:0}%, tip on the seam {quality * 100:0}% of the time.\n" +
                 $"<b>{(passed ? "<color=#05DA98>Good weld</color>" : "<color=#F5B841>Not acceptable: weld the whole seam with the tip on it</color>")}</b>");
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        /// <summary>Where the ray meets the plates, in the station's space.</summary>
        private Vector3? TipFor(Ray ray)
        {
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, PlateTop, 0)));
            if (!plane.Raycast(ray, out var distance)) return null;
            var local = transform.InverseTransformPoint(ray.GetPoint(distance));
            return new Vector3(Mathf.Clamp(local.x, -0.7f, 0.7f), PlateTop + 0.004f, Mathf.Clamp(local.z, -0.3f, 0.3f));
        }

        private void LayBead(Vector3 tip, bool onSeam)
        {
            if (_beads.Count >= MaxBeads || Vector3.Distance(tip, _lastBead) < BeadSpacing) return;
            _lastBead = tip;
            var bead = StationBuilder.Block(transform, "Bead", new Vector3(tip.x, PlateTop + 0.003f, tip.z),
                new Vector3(0.014f, 0.006f, 0.012f), material, onSeam ? UiKit.FromHex(0xC9CED6) : UiKit.FromHex(0x8A5A2B));
            _beads.Add(bead);
        }

        private void Show(string text) => _board.text = $"<b>WELD SEAM 4</b>\n<size=82%>{text}</size>";

        /// <summary>Set dressing that makes this a welding bay: the welder and its gas, and a screen behind the table.</summary>
        private void BuildBay()
        {
            if (welderModel != null)
                StationBuilder.Model(transform, "Welder Model", welderModel, new Vector3(-1.3f, 0, 1.0f), Vector3.one)
                    .transform.localRotation = Quaternion.Euler(0, 30f, 0);
            if (screenModel != null)
                StationBuilder.Model(transform, "Welding Screen Model", screenModel, new Vector3(0.1f, 0, 1.6f), Vector3.one);
        }

        private void BuildTable(Transform root)
        {
            if (tableModel != null)
                StationBuilder.Model(root, "Table Model", tableModel, Vector3.zero, Vector3.one);
            else
            {
                Block(root, "Top", new Vector3(0, 0.84f, 0), new Vector3(1.5f, 0.05f, 0.75f), PropColors.DarkSteel);
                foreach (var x in new[] { -0.7f, 0.7f })
                foreach (var z in new[] { -0.32f, 0.32f })
                    Block(root, "Leg", new Vector3(x, 0.41f, z), new Vector3(0.06f, 0.82f, 0.06f), PropColors.Steel);
            }
            Block(root, "Plate", new Vector3(0, 0.875f, -0.13f), new Vector3(1.25f, 0.02f, 0.25f), PropColors.Steel);
            Block(root, "Plate", new Vector3(0, 0.875f, 0.13f), new Vector3(1.25f, 0.02f, 0.25f), PropColors.Steel);
            Block(root, "Seam", new Vector3(0, PlateTop - 0.004f, 0), new Vector3(1.25f, 0.004f, 0.008f), PropColors.Rubber);
            foreach (var x in new[] { -SeamHalfLength, SeamHalfLength })
                Block(root, "Mark", new Vector3(x, PlateTop, -0.03f), new Vector3(0.01f, 0.004f, 0.04f), PropColors.SafetyYellow);
        }

        private void BuildTorch()
        {
            _torchRest = new Vector3(-0.5f, PlateTop + 0.03f, 0.27f);
            _torch = StationBuilder.Group(transform, "weld_torch", _torchRest);
            if (torchModel != null)
            {
                // The model's origin is the nozzle tip, like the torch's; its handle leans back from the gooseneck.
                var model = StationBuilder.Model(_torch, "Torch Model", torchModel, Vector3.zero, Vector3.one).transform;
                StationBuilder.Interactable(_torch.gameObject, "Weld torch", new Vector3(0.12f, 0.34f, 0.22f),
                    model.Find("Torch Handle").gameObject, PropColors.Textured, new Vector3(0, 0.17f, 0.06f));
            }
            else
            {
                StationBuilder.Cylinder(_torch, "Nozzle", new Vector3(0, 0.04f, 0), new Vector3(0.03f, 0.08f, 0.03f), material, UiKit.FromHex(0xB87333));
                Block(_torch, "Neck", new Vector3(0, 0.11f, 0), new Vector3(0.025f, 0.08f, 0.025f), PropColors.DarkSteel);
                var handle = Block(_torch, "Handle", new Vector3(0, 0.23f, 0), new Vector3(0.045f, 0.18f, 0.045f), PropColors.SafetyRed);
                StationBuilder.Interactable(_torch.gameObject, "Weld torch", new Vector3(0.14f, 0.36f, 0.14f), handle,
                    PropColors.SafetyRed, new Vector3(0, 0.18f, 0));
            }
            TorchHandle = _torch.gameObject.AddComponent<StationDraggable>();
            TorchHandle.DragStarted += StartWeld;
            TorchHandle.Dragged += MoveTorch;
            TorchHandle.DragEnded += FinishWeld;
        }

        private void BuildBoard(Transform root)
        {
            StationBuilder.Display(root, new Vector2(0.94f, 0.3f), material, stand: false);
            _board = StationBuilder.Text(root, "", new Vector3(0, 0, -0.02f), 0.4f, Color.white, 0.92f, 0.3f, TextAlignmentOptions.TopLeft);
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
