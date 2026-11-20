using System;
using System.Collections.Generic;
using System.Globalization;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.SequenceAndNumber
{
    /// <summary>
    /// A clinic room: put five hand hygiene step cards in order at the sink, then set the infusion pump's
    /// rate on its keypad.
    /// </summary>
    public class ClinicStation : MonoBehaviour
    {
        private const int MaxDigits = 3;

        // In the correct order. The cards are laid out shuffled.
        private static readonly string[] Steps = { "Wet hands", "Apply soap", "Scrub for 20 seconds", "Rinse", "Dry with a paper towel" };
        private static readonly int[] CardLayout = { 2, 4, 0, 3, 1 };

        private enum Stage
        {
            Idle,
            Sequencing,
            Pump,
            Done,
        }

        [SerializeField] private Material material;
        [SerializeField] private GameObject pumpModel;
        [SerializeField] private GameObject sinkModel;
        [SerializeField] private GameObject poleModel;

        private readonly StationInteractable[] _cards = new StationInteractable[Steps.Length];
        private readonly TextMeshPro[] _cardOrder = new TextMeshPro[Steps.Length];
        private readonly StationInteractable[] _keys = new StationInteractable[10];
        private readonly List<int> _order = new List<int>();
        private TextMeshPro _board;
        private TextMeshPro _display;
        private Stage _stage;
        private string _digits = "";
        private string _result;

        public event Action TaskStarted;
        public event Action<IReadOnlyList<int>> SequenceCompleted;
        public event Action<int> RateEntered;

        public StationInteractable BeginButton { get; private set; }
        public StationInteractable ClearButton { get; private set; }
        public StationInteractable StartInfusionButton { get; private set; }

        /// <summary>The card for a step, by its place in the correct order (0 = wet hands).</summary>
        public StationInteractable StepCard(int step) => _cards[step];

        public StationInteractable Key(int digit) => _keys[digit];

        private void Awake()
        {
            BuildSink(StationBuilder.Group(transform, "Sink", new Vector3(-0.4f, 0, 0.75f)));
            BuildCards(StationBuilder.Group(transform, "Step Cards", new Vector3(-0.4f, 1.52f, 1.01f)));
            BuildPump(StationBuilder.Group(transform, "Infusion Pump", new Vector3(1.0f, 0, 0.55f)));
            BuildBoard(StationBuilder.Group(transform, "Task Board", new Vector3(-0.4f, 2.17f, 1.06f)));
            Refresh();
        }

        public void ShowResult(bool passed, int score)
        {
            _stage = Stage.Done;
            _result = $"<b>Score {score}: {(passed ? "<color=#05DA98>passed</color>" : "<color=#F5B841>not passed</color>")}</b>";
            StationBuilder.SetLabel(BeginButton, "Start again");
            Refresh();
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Begin()
        {
            _order.Clear();
            _digits = "";
            _result = null;
            _stage = Stage.Sequencing;
            Refresh();
            TaskStarted?.Invoke();
        }

        private void PlaceCard(int step)
        {
            if (_stage != Stage.Sequencing || _order.Contains(step)) return;
            _order.Add(step);
            Refresh();
            if (_order.Count < Steps.Length) return;

            _stage = Stage.Pump;
            Refresh();
            SequenceCompleted?.Invoke(_order.ToArray());
        }

        private void Press(int digit)
        {
            if (_stage != Stage.Pump || _digits.Length >= MaxDigits) return;
            _digits = _digits == "0" ? digit.ToString(CultureInfo.InvariantCulture) : _digits + digit;
            Refresh();
        }

        private void ClearDigits()
        {
            if (_stage != Stage.Pump) return;
            _digits = "";
            Refresh();
        }

        private void StartInfusion()
        {
            if (_stage != Stage.Pump || _digits.Length == 0) return;
            _stage = Stage.Done;
            Refresh();
            RateEntered?.Invoke(int.Parse(_digits, CultureInfo.InvariantCulture));
        }

        private void Refresh()
        {
            for (var step = 0; step < Steps.Length; step++)
            {
                var place = _order.IndexOf(step);
                _cards[step].Interactable = _stage == Stage.Sequencing && place < 0;
                _cardOrder[step].text = place >= 0 ? (place + 1).ToString(CultureInfo.InvariantCulture) : "";
            }
            foreach (var key in _keys) key.Interactable = _stage == Stage.Pump;
            ClearButton.Interactable = _stage == Stage.Pump;
            StartInfusionButton.Interactable = _stage == Stage.Pump && _digits.Length > 0;
            BeginButton.gameObject.SetActive(_stage == Stage.Idle || _stage == Stage.Done && _result != null);

            _display.text = $"<size=60%>RATE</size>\n{(_digits.Length > 0 ? _digits : "---")} <size=60%>mL/h</size>";
            var prompt = _stage switch
            {
                Stage.Sequencing => "<b>1.</b> Click the hand hygiene cards in the order you'd do them.",
                Stage.Pump => "<b>2.</b> Set the pump rate on its keypad, then click <b>Start</b>.",
                _ => _result ?? "Click <b>Begin</b>: clean your hands, then set up the infusion.",
            };
            _board.text = "<b>WARD 4 · BED 2</b>  Prescription: 0.9% sodium chloride, <b>500 mL over 10 hours</b>.\n" +
                          $"<size=85%>{prompt}</size>";
        }

        private void BuildSink(Transform root)
        {
            if (sinkModel != null)
            {
                // A clinical hand-wash station; the step cards hang on its splashback.
                StationBuilder.Model(root, "Sink Model", sinkModel, Vector3.zero, Vector3.one);
                return;
            }

            Block(root, "Cabinet", new Vector3(0, 0.42f, 0), new Vector3(1.0f, 0.84f, 0.55f), PropColors.Panel);
            Block(root, "Counter", new Vector3(0, 0.87f, 0), new Vector3(1.05f, 0.05f, 0.6f), UiKit.FromHex(0xCFD6DE));
            Block(root, "Basin", new Vector3(0, 0.88f, -0.02f), new Vector3(0.5f, 0.04f, 0.36f), PropColors.Steel);
            Block(root, "Tap", new Vector3(0, 1.0f, 0.18f), new Vector3(0.04f, 0.2f, 0.04f), PropColors.Steel);
            Block(root, "Spout", new Vector3(0, 1.08f, 0.11f), new Vector3(0.04f, 0.04f, 0.14f), PropColors.Steel);
            Block(root, "Splashback", new Vector3(0, 1.33f, 0.29f), new Vector3(1.6f, 0.86f, 0.03f), UiKit.FromHex(0xF4F6F8));
            Block(root, "Soap", new Vector3(-0.42f, 1.08f, 0.23f), new Vector3(0.1f, 0.16f, 0.08f), UiKit.FromHex(0xDCE7F2));
            Block(root, "Towels", new Vector3(0.45f, 1.1f, 0.22f), new Vector3(0.24f, 0.22f, 0.1f), UiKit.FromHex(0xDCE7F2));
        }

        private void BuildCards(Transform root)
        {
            for (var slot = 0; slot < CardLayout.Length; slot++)
            {
                var step = CardLayout[slot];
                var card = StationBuilder.Group(root, Steps[step], new Vector3(-0.6f + slot * 0.3f, 0, 0));
                var body = Block(card, "Card", Vector3.zero, new Vector3(0.27f, 0.22f, 0.02f), Color.white);
                StationBuilder.Text(card, $"<b>{Steps[step]}</b>", new Vector3(0, -0.025f, -0.012f), 0.34f, UiKit.Ink, 0.25f, 0.13f);
                _cardOrder[step] = StationBuilder.Text(card, "", new Vector3(0, 0.07f, -0.012f), 0.5f, UiKit.Purple, 0.24f, 0.06f);
                _cardOrder[step].fontStyle = FontStyles.Bold;
                var index = step;
                _cards[step] = StationBuilder.Interactable(card.gameObject, Steps[step], new Vector3(0.28f, 0.23f, 0.04f), body, Color.white);
                _cards[step].Activated += _ => PlaceCard(index);
            }
        }

        private void BuildPump(Transform root)
        {
            if (poleModel != null)
            {
                // The saline bag hangs from the pole's left hook, just above where the pump's tubing starts.
                StationBuilder.Model(root, "IV Pole Model", poleModel, Vector3.zero, Vector3.one);
                StationBuilder.Text(root, "0.9% NaCl\n500 mL", new Vector3(-0.13f, 1.86f, 0.058f), 0.14f, UiKit.Ink, 0.13f, 0.1f);
            }
            else
            {
                Block(root, "Base", new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.05f, 0.5f), PropColors.DarkSteel);
                Block(root, "Pole", new Vector3(0, 1.0f, 0.08f), new Vector3(0.03f, 1.95f, 0.03f), PropColors.Steel);
                Block(root, "Bag", new Vector3(0, 1.82f, 0.08f), new Vector3(0.16f, 0.24f, 0.04f), UiKit.FromHex(0xE8F1F8));
                StationBuilder.Text(root, "0.9% NaCl\n500 mL", new Vector3(0, 1.82f, 0.055f), 0.14f, UiKit.Ink, 0.14f, 0.1f);
            }

            var pump = StationBuilder.Group(root, "Pump", new Vector3(0, 1.18f, 0));
            if (pumpModel != null)
                StationBuilder.Model(pump, "Pump Model", pumpModel, Vector3.zero, Vector3.one);
            else
                Block(pump, "Body", Vector3.zero, new Vector3(0.42f, 0.62f, 0.14f), UiKit.FromHex(0xF4F6F8));
            Block(pump, "Display", new Vector3(0, 0.2f, -0.075f), new Vector3(0.34f, 0.15f, 0.01f), UiKit.Navy);
            _display = StationBuilder.Text(pump, "", new Vector3(0, 0.2f, -0.082f), 0.5f, UiKit.Green, 0.32f, 0.14f);

            for (var digit = 0; digit < 10; digit++)
            {
                var column = digit == 0 ? 1 : (digit - 1) % 3;
                var row = digit == 0 ? 3 : (digit - 1) / 3;
                var position = new Vector3(-0.1f + column * 0.1f, 0.08f - row * 0.075f, -0.075f);
                _keys[digit] = StationBuilder.Button(pump, digit.ToString(CultureInfo.InvariantCulture), position, new Vector2(0.085f, 0.06f), material, PropColors.DarkSteel, 0.4f);
                var value = digit;
                _keys[digit].Activated += _ => Press(value);
            }
            ClearButton = StationBuilder.Button(pump, "C", new Vector3(-0.1f, -0.145f, -0.075f), new Vector2(0.085f, 0.06f), material, PropColors.Steel, 0.4f);
            ClearButton.Activated += _ => ClearDigits();
            StartInfusionButton = StationBuilder.Button(pump, "Start", new Vector3(0.1f, -0.25f, -0.075f), new Vector2(0.17f, 0.07f), material, PropColors.SafetyGreen, 0.36f);
            StartInfusionButton.Activated += _ => StartInfusion();
        }

        private void BuildBoard(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.64f, 0.44f), material, stand: false);
            _board = StationBuilder.Text(root, "", new Vector3(-0.17f, 0.01f, -0.025f), 0.56f, Color.white, 1.26f, 0.44f, TextAlignmentOptions.TopLeft);
            BeginButton = StationBuilder.Button(root, "Begin", new Vector3(0.64f, -0.15f, -0.03f), new Vector2(0.3f, 0.12f), material, UiKit.Purple, 0.4f);
            BeginButton.Activated += _ => Begin();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
