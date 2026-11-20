using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.ModuleTargets
{
    /// <summary>
    /// A forklift's pre-shift check in three short modules: tires, forks, and controls. Each module shows one
    /// finding; the learner decides whether the forklift is safe to use or must be tagged out.
    /// </summary>
    public class ForkliftCheck : MonoBehaviour
    {
        private static readonly Dictionary<string, (string title, string finding, bool safe)> Modules = new Dictionary<string, (string, string, bool)>
        {
            ["preshift_tires"] = ("Tires", "The front left tire has a chunk missing from the tread.", false),
            ["preshift_forks"] = ("Forks", "Both forks are straight, level, and free of cracks.", true),
            ["preshift_controls"] = ("Controls", "The horn makes no sound when pressed.", false),
        };

        [SerializeField] private Material material;
        [SerializeField] private GameObject forkliftModel;

        private readonly Dictionary<string, StationInteractable> _moduleButtons = new Dictionary<string, StationInteractable>();
        private readonly HashSet<string> _done = new HashSet<string>();
        private TextMeshPro _panel;
        private string _current;
        private string _lastResult;

        public StationInteractable SafeButton { get; private set; }
        public StationInteractable TagOutButton { get; private set; }

        public StationInteractable ModuleButton(string moduleId) => _moduleButtons[moduleId];

        private static ModuleTargetsSample Lesson => ModuleTargetsSample.Instance;

        private void Awake()
        {
            BuildForklift(StationBuilder.Group(transform, "Forklift", new Vector3(1.0f, 0, 0.9f)));
            BuildPanel(StationBuilder.Group(transform, "Check Panel", new Vector3(-0.35f, 1.45f, 0.7f)));
        }

        private void Start()
        {
            Show("Choose a module, or open one with ?module=preshift_forks.");
            if (Lesson != null) Lesson.Attach(this);
        }

        private void OnDestroy()
        {
            if (Lesson != null) Lesson.Detach(this);
        }

        public void Begin(string moduleId)
        {
            _current = moduleId;
            var module = Modules[moduleId];
            Show($"{TakeLastResult()}<b>{module.title}</b>\n{module.finding}\n<size=80%>Is the forklift safe to use?</size>");
            Refresh();
        }

        public void ShowAllDone() => Show($"{TakeLastResult()}<color=#05DA98><b>All assigned modules are done.</b></color>");

        private void Decide(bool safe)
        {
            if (_current == null) return;
            var correct = safe == Modules[_current].safe;
            var title = Modules[_current].title;
            _done.Add(_current);
            _current = null;
            _lastResult = correct ? $"<color=#05DA98><b>{title}: right call.</b></color>" : $"<color=#F5B841><b>{title}: wrong call. It should have been {(safe ? "tagged out" : "used")}.</b></color>";
            Show(_lastResult);
            Refresh();
            // In an LMS sequence the next module can begin during this call, so the result is shown first.
            Lesson.CompleteModule(correct);
            SampleEvents.ReportFinished(correct ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private string TakeLastResult()
        {
            var line = _lastResult != null ? _lastResult + "\n" : "";
            _lastResult = null;
            return line;
        }

        private void Show(string text) => _panel.text = $"<b>FORKLIFT 2 · PRE-SHIFT CHECK</b>\n<size=85%>{text}</size>";

        private void Refresh()
        {
            SafeButton.gameObject.SetActive(_current != null);
            TagOutButton.gameObject.SetActive(_current != null);
            foreach (var pair in _moduleButtons)
            {
                pair.Value.Interactable = _current == null;
                StationBuilder.SetLabel(pair.Value, _done.Contains(pair.Key) ? $"{Modules[pair.Key].title} · done" : Modules[pair.Key].title);
            }
        }

        private void BuildPanel(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.44f, 0.89f), material, stand: true);
            _panel = StationBuilder.Text(root, "", new Vector3(0, 0.16f, -0.025f), 0.52f, Color.white, 1.38f, 0.5f, TextAlignmentOptions.TopLeft);

            var order = Lesson != null ? Lesson.ModuleOrder() : new List<string>(Modules.Keys);
            for (var i = 0; i < order.Count; i++)
            {
                var moduleId = order[i];
                if (!Modules.ContainsKey(moduleId)) continue;
                var button = StationBuilder.Button(root, Modules[moduleId].title, new Vector3(-0.47f + i * 0.47f, -0.36f, -0.03f), new Vector2(0.42f, 0.11f), material, UiKit.NavyLighter, 0.34f);
                button.Activated += _ => Lesson.StartModule(moduleId);
                _moduleButtons[moduleId] = button;
            }

            SafeButton = StationBuilder.Button(root, "Safe to use", new Vector3(-0.3f, -0.16f, -0.03f), new Vector2(0.44f, 0.12f), material, PropColors.SafetyGreen, 0.34f);
            SafeButton.Activated += _ => Decide(true);
            TagOutButton = StationBuilder.Button(root, "Tag out", new Vector3(0.26f, -0.16f, -0.03f), new Vector2(0.44f, 0.12f), material, PropColors.SafetyRed, 0.34f);
            TagOutButton.Activated += _ => Decide(false);
            SafeButton.gameObject.SetActive(false);
            TagOutButton.gameObject.SetActive(false);
        }

        private void BuildForklift(Transform root)
        {
            if (forkliftModel != null)
            {
                // This truck's tyre finding: the front-left tyre has a gouged tread.
                var truck = StationBuilder.Model(root, "Forklift Model", forkliftModel, Vector3.zero, Vector3.one).transform;
                truck.Find("Wheel FL").gameObject.SetActive(false);
                // Real size, so it stands further back, with the front-left tyre toward the learner.
                root.localPosition = new Vector3(0.55f, 0, 1.4f);
                root.localRotation = Quaternion.Euler(0, -42f, 0);
                return;
            }

            Block(root, "Body", new Vector3(0, 0.42f, 0.1f), new Vector3(0.7f, 0.5f, 1.0f), PropColors.SafetyYellow);
            Block(root, "Counterweight", new Vector3(0, 0.45f, 0.62f), new Vector3(0.72f, 0.5f, 0.16f), PropColors.Rubber);
            Block(root, "Overhead Guard", new Vector3(0, 1.32f, 0.2f), new Vector3(0.66f, 0.04f, 0.7f), PropColors.DarkSteel);
            Block(root, "Seat", new Vector3(0, 0.74f, 0.3f), new Vector3(0.36f, 0.12f, 0.3f), PropColors.Rubber);
            foreach (var x in new[] { -0.31f, 0.31f })
            {
                Block(root, "Guard Post", new Vector3(x, 0.98f, -0.12f), new Vector3(0.04f, 0.7f, 0.04f), PropColors.DarkSteel);
                Block(root, "Guard Post", new Vector3(x, 0.98f, 0.52f), new Vector3(0.04f, 0.7f, 0.04f), PropColors.DarkSteel);
                Block(root, "Mast", new Vector3(x * 0.8f, 0.85f, -0.45f), new Vector3(0.06f, 1.6f, 0.06f), PropColors.DarkSteel);
                Block(root, "Fork", new Vector3(x * 0.6f, 0.08f, -0.75f), new Vector3(0.08f, 0.04f, 0.6f), PropColors.DarkSteel);
            }
            foreach (var z in new[] { -0.22f, 0.45f })
            foreach (var x in new[] { -0.36f, 0.36f })
                StationBuilder.Cylinder(root, "Wheel", new Vector3(x, 0.17f, z), new Vector3(0.34f, 0.14f, 0.34f), material, PropColors.Rubber).transform.localRotation = Quaternion.Euler(0, 0, 90f);
            Block(root, "Damaged Tread", new Vector3(-0.44f, 0.24f, -0.22f), new Vector3(0.02f, 0.08f, 0.1f), UiKit.FromHex(0x9A9A9A));
            root.localRotation = Quaternion.Euler(0, -30f, 0);
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
