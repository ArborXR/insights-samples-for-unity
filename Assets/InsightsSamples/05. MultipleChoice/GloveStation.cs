using System;
using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.MultipleChoice
{
    /// <summary>
    /// A maintenance bench: degrease a pump housing with mineral spirits, after choosing hand protection from
    /// the glove rack. The safety data sheet on the board says which gloves to wear.
    /// </summary>
    public class GloveStation : MonoBehaviour
    {
        private static readonly (string label, Color box, string feedback)[] Gloves =
        {
            ("NITRILE", UiKit.FromHex(0x2F6FDE), "Right choice: nitrile resists petroleum solvents."),
            ("LATEX", UiKit.FromHex(0xF1E3C2), "Latex swells and breaks down in petroleum solvents."),
            ("COTTON", UiKit.FromHex(0xF5F5F5), "Cotton soaks up the solvent and holds it against your skin."),
        };

        [SerializeField] private Material material;
        [SerializeField] private GameObject washerModel;
        [SerializeField] private GameObject gloveBoxModel;
        [SerializeField] private GameObject voluteModel;
        [SerializeField] private GameObject dispenserModel;

        private readonly List<StationInteractable> _options = new List<StationInteractable>();
        private TextMeshPro _board;
        private bool _choosing;
        private int _chosen = -1;
        private string _result;

        public event Action TaskStarted;
        public event Action<int> OptionChosen;

        public StationInteractable BeginButton { get; private set; }

        /// <summary>0 nitrile, 1 latex, 2 cotton, 3 no gloves.</summary>
        public StationInteractable Option(int index) => _options[index];

        private void Awake()
        {
            BuildBench(StationBuilder.Group(transform, "Bench", new Vector3(0.2f, 0, 0.55f)));
            // The parts washer's open lid stands taller than the bench, so the rack and board sit higher above it.
            var raised = washerModel != null;
            BuildRack(StationBuilder.Group(transform, "Glove Rack", new Vector3(0.2f, raised ? 1.55f : 1.32f, 0.82f)));
            BuildBoard(StationBuilder.Group(transform, "Task Board", new Vector3(0.2f, raised ? 2.02f : 1.9f, 0.86f)));
            for (var index = 0; index < _options.Count; index++)
            {
                var option = index;
                _options[index].Activated += _ => Choose(option);
            }
            Refresh();
        }

        public void ShowResult(bool passed)
        {
            var feedback = _chosen < Gloves.Length ? Gloves[_chosen].feedback : "Mineral spirits dry out and crack bare skin, and some is absorbed.";
            _result = $"{feedback}\n<b>{(passed ? "<color=#05DA98>Passed</color>" : "<color=#F5B841>Not passed</color>")}</b>";
            StationBuilder.SetLabel(BeginButton, "Try again");
            Refresh();
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Begin()
        {
            _choosing = true;
            _chosen = -1;
            _result = null;
            Refresh();
            TaskStarted?.Invoke();
        }

        private void Choose(int option)
        {
            if (!_choosing) return;
            _choosing = false;
            _chosen = option;
            Refresh();
            OptionChosen?.Invoke(option);
        }

        private void Refresh()
        {
            foreach (var option in _options) option.Interactable = _choosing;
            BeginButton.gameObject.SetActive(!_choosing);
            var prompt = _choosing ? "<b>Choose your gloves from the rack.</b>" : _result ?? "Click <b>Begin task</b>, then choose your gloves.";
            _board.text = "<b>TASK</b>  Degrease the pump housing with mineral spirits.\n" +
                          "<size=80%><b>SDS, section 8, hand protection:</b> chemical-resistant gloves (nitrile). " +
                          "Not latex or fabric.</size>\n" + $"<size=85%>{prompt}</size>";
        }

        private void BuildBench(Transform root)
        {
            if (washerModel != null)
            {
                // A solvent parts washer, with the greasy pump housing on its grate.
                StationBuilder.Model(root, "Washer Model", washerModel, Vector3.zero, Vector3.one);
                if (voluteModel != null)
                    StationBuilder.Model(root, "Pump Volute Model", voluteModel, new Vector3(0.12f, 0.86f, -0.12f), Vector3.one);
                else
                {
                    Block(root, "Pump Housing", new Vector3(0.12f, 0.93f, -0.12f), new Vector3(0.3f, 0.14f, 0.22f), UiKit.FromHex(0x6B7C8F));
                    Block(root, "Grease", new Vector3(0.07f, 1.002f, -0.15f), new Vector3(0.14f, 0.006f, 0.1f), UiKit.FromHex(0x2A2418));
                }
                StationBuilder.Text(root, "<b>MINERAL SPIRITS</b>", new Vector3(0, 0.38f, -0.418f), 0.26f, UiKit.Ink, 0.3f, 0.06f);
                return;
            }

            Block(root, "Cabinet", new Vector3(0, 0.43f, 0), new Vector3(1.6f, 0.86f, 0.58f), PropColors.Panel);
            Block(root, "Top", new Vector3(0, 0.89f, 0), new Vector3(1.66f, 0.05f, 0.64f), PropColors.DarkSteel);
            Block(root, "Tray", new Vector3(0.35f, 0.925f, -0.02f), new Vector3(0.6f, 0.02f, 0.4f), PropColors.Steel);
            Block(root, "Pump Housing", new Vector3(0.35f, 1.0f, -0.02f), new Vector3(0.3f, 0.14f, 0.22f), UiKit.FromHex(0x6B7C8F));
            Block(root, "Grease", new Vector3(0.3f, 1.072f, -0.05f), new Vector3(0.14f, 0.006f, 0.1f), UiKit.FromHex(0x2A2418));
            StationBuilder.Cylinder(root, "Solvent Can", new Vector3(-0.3f, 1.07f, 0.05f), new Vector3(0.2f, 0.32f, 0.2f), material, PropColors.SafetyRed);
            Block(root, "Can Label", new Vector3(-0.3f, 1.07f, -0.052f), new Vector3(0.16f, 0.12f, 0.004f), Color.white);
            StationBuilder.Text(root, "<b>MINERAL\nSPIRITS</b>", new Vector3(-0.3f, 1.07f, -0.056f), 0.22f, UiKit.Ink, 0.15f, 0.11f);
        }

        private void BuildRack(Transform root)
        {
            if (dispenserModel != null)
            {
                // A wall dispenser holds the three boxes; the No gloves button gets its own small plate.
                StationBuilder.Model(root, "Dispenser Model", dispenserModel, new Vector3(-0.04f, 0, 0), Vector3.one);
                Block(root, "Button Plate", new Vector3(0.7f, 0, 0.06f), new Vector3(0.36f, 0.24f, 0.03f), PropColors.Steel);
            }
            else
                Block(root, "Backboard", new Vector3(0.18f, 0, 0.06f), new Vector3(1.5f, 0.34f, 0.03f), PropColors.Steel);
            for (var index = 0; index < Gloves.Length; index++)
            {
                var (label, color, _) = Gloves[index];
                var box = StationBuilder.Group(root, $"{label} Gloves", new Vector3(-0.4f + index * 0.36f, 0, 0));
                GameObject body;
                Color bodyColor;
                if (gloveBoxModel != null)
                {
                    // White carton with a colour band and a glove cuff in the glove's colour; the label names the material.
                    var model = StationBuilder.Model(box, "Glove Box Model", gloveBoxModel, Vector3.zero, Vector3.one).transform;
                    body = model.Find("Glove Box").gameObject;
                    StationBuilder.Tint(model.Find("Glove Box Band").gameObject, color);
                    StationBuilder.Tint(model.Find("Glove").gameObject, Color.Lerp(color, Color.white, 0.25f));
                    StationBuilder.Text(box, $"<b>{label}</b>", new Vector3(0, -0.02f, -0.065f), 0.3f, UiKit.Ink, 0.16f, 0.06f);
                    bodyColor = new Color(0.96f, 0.96f, 0.95f);
                }
                else
                {
                    body = Block(box, "Box", Vector3.zero, new Vector3(0.28f, 0.16f, 0.12f), color);
                    Block(box, "Glove", new Vector3(0, 0.09f, -0.01f), new Vector3(0.08f, 0.04f, 0.05f), Color.Lerp(color, Color.white, 0.4f));
                    var textColor = index == 0 ? Color.white : UiKit.Ink;
                    StationBuilder.Text(box, $"<b>{label}</b>", new Vector3(0, 0, -0.065f), 0.32f, textColor, 0.26f, 0.08f);
                    bodyColor = color;
                }
                _options.Add(StationBuilder.Interactable(box.gameObject, $"{label} gloves", new Vector3(0.32f, 0.26f, 0.16f), body, bodyColor, new Vector3(0, 0.03f, 0)));
            }
            _options.Add(StationBuilder.Button(root, "No gloves", new Vector3(0.7f, 0, -0.01f), new Vector2(0.3f, 0.16f), material, PropColors.DarkSteel, 0.36f));
        }

        private void BuildBoard(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.74f, 0.52f), material, stand: false);
            _board = StationBuilder.Text(root, "", new Vector3(0, 0.04f, -0.025f), 0.58f, Color.white, 1.66f, 0.48f, TextAlignmentOptions.TopLeft);
            BeginButton = StationBuilder.Button(root, "Begin task", new Vector3(0.6f, -0.2f, -0.03f), new Vector2(0.44f, 0.12f), material, UiKit.Purple, 0.4f);
            BeginButton.Activated += _ => Begin();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
