using System;
using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.TrueFalse
{
    /// <summary>
    /// A workshop corner to inspect: a frayed cable, a blocked exit, an oil spill, and a correctly guarded
    /// bench grinder. The learner flags what's unsafe; flags can't be taken back.
    /// </summary>
    public class Workshop : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject benchModel;
        [SerializeField] private GameObject grinderModel;
        [SerializeField] private GameObject exitModel;
        [SerializeField] private GameObject cartonModel;
        [SerializeField] private GameObject cordModel;
        [SerializeField] private GameObject oilCanModel;

        private readonly List<StationInteractable> _items = new List<StationInteractable>();
        private readonly List<GameObject> _flags = new List<GameObject>();
        private TextMeshPro _board;
        private bool _inspecting;
        private string _status = "Click <b>Start inspection</b>, flag every hazard you see, then click <b>Finish</b>.";

        public event Action InspectionStarted;
        public event Action<int> ItemFlagged;
        public event Action InspectionFinished;

        public StationInteractable StartButton { get; private set; }
        public StationInteractable FinishButton { get; private set; }
        public int ItemCount => _items.Count;

        /// <summary>0 frayed cable, 1 blocked exit, 2 oil spill, 3 guarded grinder.</summary>
        public StationInteractable Item(int index) => _items[index];

        private void Awake()
        {
            BuildBench(StationBuilder.Group(transform, "Workbench", new Vector3(-0.35f, 0, 0.75f)));
            AddItem(BuildCable(StationBuilder.Group(transform, "Extension Cable", new Vector3(0.25f, 0, -0.15f))), new Vector3(0.12f, 0, -0.35f));
            AddItem(BuildExit(StationBuilder.Group(transform, "Fire Exit", new Vector3(1.75f, 0, 1.1f))), new Vector3(-0.2f, 0.78f, -0.55f));
            AddItem(BuildSpill(StationBuilder.Group(transform, "Oil Spill", new Vector3(1.15f, 0, -0.35f))), new Vector3(0.3f, 0, 0));
            AddItem(BuildGrinder(StationBuilder.Group(transform, "Bench Grinder", new Vector3(-0.75f, 0.91f, 0.65f))), new Vector3(0, 0.22f, 0));
            BuildBoard(StationBuilder.Group(transform, "Inspection Board", new Vector3(-0.35f, 1.85f, 1.05f)));
            Refresh();
        }

        public void ShowResult(bool passed, int score, int found, int falseFlags)
        {
            _status = $"Found {found} of 3 hazards, {falseFlags} false {(falseFlags == 1 ? "flag" : "flags")}. " +
                      $"<b>Score {score}: {(passed ? "<color=#05DA98>passed</color>" : "<color=#F5B841>not passed</color>")}</b>";
            StationBuilder.SetLabel(StartButton, "Inspect again");
            Refresh();
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Begin()
        {
            foreach (var flag in _flags) flag.SetActive(false);
            _inspecting = true;
            _status = "Flag every hazard you see. Flags can't be removed.";
            Refresh();
            InspectionStarted?.Invoke();
        }

        private void Flag(int index)
        {
            if (!_inspecting) return;
            _flags[index].SetActive(true);
            _items[index].Interactable = false;
            ItemFlagged?.Invoke(index);
        }

        private void Finish()
        {
            if (!_inspecting) return;
            _inspecting = false;
            Refresh();
            InspectionFinished?.Invoke();
        }

        private void Refresh()
        {
            for (var index = 0; index < _items.Count; index++)
                _items[index].Interactable = _inspecting && !_flags[index].activeSelf;
            StartButton.gameObject.SetActive(!_inspecting);
            FinishButton.gameObject.SetActive(_inspecting);
            _board.text = $"<b>WORKSHOP SAFETY INSPECTION</b>\n<size=80%>{_status}</size>";
        }

        private void AddItem(StationInteractable item, Vector3 flagOffset)
        {
            var index = _items.Count;
            item.Activated += _ => Flag(index);
            _items.Add(item);

            var flag = StationBuilder.Group(item.transform, "Flag", flagOffset).gameObject;
            Block(flag.transform, "Pole", new Vector3(0, 0.2f, 0), new Vector3(0.015f, 0.4f, 0.015f), PropColors.DarkSteel);
            Block(flag.transform, "Pennant", new Vector3(0.07f, 0.34f, 0), new Vector3(0.13f, 0.1f, 0.01f), PropColors.SafetyYellow);
            flag.SetActive(false);
            _flags.Add(flag);
        }

        private void BuildBench(Transform root)
        {
            if (benchModel != null)
            {
                StationBuilder.Model(root, "Workbench Model", benchModel, Vector3.zero, Vector3.one);
                return;
            }

            Block(root, "Top", new Vector3(0, 0.88f, 0), new Vector3(1.6f, 0.06f, 0.65f), PropColors.Wood);
            foreach (var x in new[] { -0.74f, 0.74f })
            foreach (var z in new[] { -0.27f, 0.27f })
                Block(root, "Leg", new Vector3(x, 0.43f, z), new Vector3(0.06f, 0.85f, 0.06f), PropColors.DarkSteel);
            Block(root, "Pegboard", new Vector3(0, 1.3f, 0.3f), new Vector3(1.6f, 0.8f, 0.03f), PropColors.Panel);
            Block(root, "Toolbox", new Vector3(0.45f, 0.99f, 0.05f), new Vector3(0.42f, 0.16f, 0.22f), PropColors.SafetyRed);
        }

        private StationInteractable BuildCable(Transform root)
        {
            if (cordModel != null)
            {
                // The jacket is split open, with the conductors bowed out and bare copper showing.
                // Drawn a little larger than life so the damage reads from the station camera.
                var cord = StationBuilder.Model(root, "Cord Model", cordModel, Vector3.zero, Vector3.one * 1.25f);
                root.localRotation = Quaternion.Euler(0, 28f, 0);
                return StationBuilder.Interactable(root.gameObject, "Extension cable", new Vector3(0.3f, 0.12f, 1.55f), cord, PropColors.Textured);
            }

            var orange = UiKit.FromHex(0xFF7A1A);
            var copper = UiKit.FromHex(0xD9853B);
            var cable = Block(root, "Cord", new Vector3(0, 0.016f, 0), new Vector3(0.032f, 0.032f, 1.1f), orange);
            Block(root, "Exposed Wires", new Vector3(0, 0.016f, -0.2f), new Vector3(0.04f, 0.036f, 0.14f), copper);
            Block(root, "Strand", new Vector3(0.05f, 0.03f, -0.17f), new Vector3(0.08f, 0.01f, 0.01f), copper);
            Block(root, "Strand", new Vector3(-0.045f, 0.03f, -0.24f), new Vector3(0.07f, 0.01f, 0.01f), copper);
            Block(root, "Strand", new Vector3(0.035f, 0.035f, -0.25f), new Vector3(0.05f, 0.01f, 0.01f), UiKit.FromHex(0x2E9E5B));
            Block(root, "Plug", new Vector3(0, 0.03f, -0.58f), new Vector3(0.07f, 0.05f, 0.08f), PropColors.Rubber);
            root.localRotation = Quaternion.Euler(0, 28f, 0);
            return StationBuilder.Interactable(root.gameObject, "Extension cable", new Vector3(0.3f, 0.12f, 1.25f), cable, orange);
        }

        private StationInteractable BuildExit(Transform root)
        {
            if (exitModel != null)
            {
                StationBuilder.Model(root, "Exit Model", exitModel, Vector3.zero, Vector3.one);
                StationBuilder.Text(root, "<b>EXIT</b>", new Vector3(0, 2.25f, -0.03f), 0.9f, Color.white, 0.4f, 0.16f);
                GameObject first = null;
                foreach (var (position, size) in new[]
                {
                    (new Vector3(-0.15f, 0.25f, -0.45f), new Vector3(0.55f, 0.5f, 0.45f)),
                    (new Vector3(0.28f, 0.2f, -0.4f), new Vector3(0.4f, 0.4f, 0.4f)),
                    (new Vector3(-0.1f, 0.68f, -0.42f), new Vector3(0.4f, 0.36f, 0.38f)),
                })
                {
                    var box = cartonModel != null
                        ? StationBuilder.Model(root, "Carton", cartonModel, position, size / Conveyor.CartonModelSize)
                        : Block(root, "Carton", position, size, PropColors.Cardboard);
                    if (first == null) first = box;
                }
                return StationBuilder.Interactable(root.gameObject, "Fire exit", new Vector3(1.1f, 2.4f, 0.7f), first,
                    cartonModel != null ? PropColors.Textured : PropColors.Cardboard, new Vector3(0, 1.2f, -0.25f));
            }

            Block(root, "Frame", new Vector3(0, 1.05f, 0.03f), new Vector3(1.05f, 2.1f, 0.04f), PropColors.DarkSteel);
            Block(root, "Door", new Vector3(0, 1.0f, 0), new Vector3(0.9f, 2.0f, 0.05f), PropColors.Steel);
            Block(root, "Push Bar", new Vector3(0, 1.0f, -0.04f), new Vector3(0.75f, 0.05f, 0.04f), PropColors.DarkSteel);
            Block(root, "Exit Sign", new Vector3(0, 2.25f, 0), new Vector3(0.45f, 0.18f, 0.05f), PropColors.SafetyGreen);
            StationBuilder.Text(root, "<b>EXIT</b>", new Vector3(0, 2.25f, -0.03f), 0.9f, Color.white, 0.4f, 0.16f);

            var carton = Block(root, "Carton", new Vector3(-0.15f, 0.25f, -0.45f), new Vector3(0.55f, 0.5f, 0.45f), PropColors.Cardboard);
            Block(root, "Carton", new Vector3(0.28f, 0.2f, -0.4f), new Vector3(0.4f, 0.4f, 0.4f), PropColors.Cardboard);
            Block(root, "Carton", new Vector3(-0.1f, 0.68f, -0.42f), new Vector3(0.4f, 0.36f, 0.38f), UiKit.FromHex(0xB98D5F));
            return StationBuilder.Interactable(root.gameObject, "Fire exit", new Vector3(1.1f, 2.4f, 0.7f), carton,
                PropColors.Cardboard, new Vector3(0, 1.2f, -0.25f));
        }

        private StationInteractable BuildSpill(Transform root)
        {
            var oil = UiKit.FromHex(0x16110B);
            var puddle = StationBuilder.Cylinder(root, "Puddle", new Vector3(0, 0.004f, 0), new Vector3(0.85f, 0.008f, 0.55f), material, oil);
            StationBuilder.Cylinder(root, "Puddle", new Vector3(-0.38f, 0.004f, 0.12f), new Vector3(0.35f, 0.008f, 0.28f), material, oil);
            if (oilCanModel != null)
            {
                // The can that spilled it lies on its side, spout toward the puddle.
                StationBuilder.Model(root, "Oil Can Model", oilCanModel, new Vector3(0.5f, 0, 0.3f), Vector3.one)
                    .transform.localRotation = Quaternion.Euler(0, -31f, 0);
            }
            else
                StationBuilder.Cylinder(root, "Oil Can", new Vector3(0.45f, 0.13f, 0.32f), new Vector3(0.16f, 0.26f, 0.16f), material, UiKit.FromHex(0x2F5DA8));
            return StationBuilder.Interactable(root.gameObject, "Oil spill", new Vector3(1.2f, 0.1f, 0.7f), puddle, oil);
        }

        private StationInteractable BuildGrinder(Transform root)
        {
            if (grinderModel != null)
            {
                // Correctly guarded: wheel guards, tongue guards, tool rests and eye shields all in place.
                var grinder = StationBuilder.Model(root, "Grinder Model", grinderModel, Vector3.zero, Vector3.one).transform;
                return StationBuilder.Interactable(root.gameObject, "Bench grinder", new Vector3(0.5f, 0.36f, 0.3f),
                    grinder.Find("Bench Grinder").gameObject, PropColors.Textured, new Vector3(0, 0.15f, 0));
            }

            Block(root, "Base", new Vector3(0, 0.03f, 0), new Vector3(0.3f, 0.06f, 0.16f), PropColors.DarkSteel);
            var motor = Block(root, "Motor", new Vector3(0, 0.13f, 0), new Vector3(0.2f, 0.15f, 0.15f), UiKit.FromHex(0x4C6E91));
            foreach (var side in new[] { -1f, 1f })
            {
                var wheel = StationBuilder.Cylinder(root, "Wheel", new Vector3(side * 0.16f, 0.13f, 0), new Vector3(0.16f, 0.04f, 0.16f), material, PropColors.Steel);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90f);
                Block(root, "Guard", new Vector3(side * 0.16f, 0.17f, 0), new Vector3(0.07f, 0.12f, 0.19f), PropColors.SafetyYellow);
                Block(root, "Eye Shield", new Vector3(side * 0.16f, 0.25f, -0.08f), new Vector3(0.12f, 0.08f, 0.01f), UiKit.FromHex(0xBFD9E6));
            }
            return StationBuilder.Interactable(root.gameObject, "Bench grinder", new Vector3(0.5f, 0.36f, 0.3f), motor,
                UiKit.FromHex(0x4C6E91), new Vector3(0, 0.15f, 0));
        }

        private void BuildBoard(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.54f, 0.56f), material, stand: false);
            _board = StationBuilder.Text(root, "", new Vector3(0, 0.07f, -0.025f), 0.62f, Color.white, 1.48f, 0.44f, TextAlignmentOptions.TopLeft);
            StartButton = StationBuilder.Button(root, "Start inspection", new Vector3(0.45f, -0.22f, -0.03f), new Vector2(0.56f, 0.12f), material, UiKit.Purple, 0.4f);
            StartButton.Activated += _ => Begin();
            FinishButton = StationBuilder.Button(root, "Finish inspection", new Vector3(0.45f, -0.22f, -0.03f), new Vector2(0.56f, 0.12f), material, UiKit.Purple, 0.4f);
            FinishButton.Activated += _ => Finish();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
