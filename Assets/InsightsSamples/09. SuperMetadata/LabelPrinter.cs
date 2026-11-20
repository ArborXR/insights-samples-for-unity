using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.SuperMetadata
{
    /// <summary>
    /// The end of an outbound line, rolled out at two sites in two languages: a print-and-apply labeller tamps a
    /// shipping label onto the carton under it, then the conveyor indexes the next carton in. Labels print from the
    /// station's own site and language settings. The panel shows the context the app has registered for its records.
    /// </summary>
    public class LabelPrinter : MonoBehaviour
    {
        private static readonly (string id, string name)[] Sites = { ("denver_dc", "Denver DC"), ("leeds_dc", "Leeds DC") };
        private static readonly (string code, string name)[] Languages = { ("en", "English"), ("fr", "Français") };
        private static readonly Vector3 CartonSize = new Vector3(0.34f, 0.25f, 0.3f);
        private const float BeltTop = 0.725f;
        private const float HeadX = 0.3f;
        private const float Spacing = 0.6f;
        private const float TampTravel = 0.083f;

        [SerializeField] private Material material;
        [SerializeField] private GameObject conveyorModel;
        [SerializeField] private GameObject cartonModel;
        [SerializeField] private GameObject labellerModel;

        private readonly Dictionary<string, StationInteractable> _siteButtons = new Dictionary<string, StationInteractable>();
        private readonly Dictionary<string, StationInteractable> _languageButtons = new Dictionary<string, StationInteractable>();
        private readonly List<Transform> _cartons = new List<Transform>();
        private Conveyor _conveyor;
        private Transform _tamp;
        private Vector3 _tampRest;
        private TextMeshPro _context;
        private TextMeshPro _status;
        private bool _busy;

        public event Action<string> SiteChosen;
        public event Action<string> LanguageChosen;
        public event Action PrintRequested;
        public event Action UnregisterSiteRequested;
        public event Action ResetRequested;

        public string Site { get; private set; } = Sites[0].id;
        public string Language { get; private set; } = Languages[0].code;
        public StationInteractable PrintButton { get; private set; }
        public StationInteractable UnregisterSiteButton { get; private set; }
        public StationInteractable ResetButton { get; private set; }

        public StationInteractable SiteButton(string site) => _siteButtons[site];

        public StationInteractable LanguageButton(string languageCode) => _languageButtons[languageCode];

        private void Awake()
        {
            _conveyor = Conveyor.Build(transform, new Vector3(0.1f, 0, 0.55f), 2.6f, material, conveyorModel);
            _conveyor.Running = false;
            for (var i = 0; i < 3; i++) _cartons.Add(Carton(HeadX - (2 - i) * Spacing, i));
            BuildLabeller(StationBuilder.Group(transform, "Labeller", new Vector3(0.1f + HeadX, 0, 1.0f)));
            BuildPanel(StationBuilder.Group(transform, "Line Panel", new Vector3(-0.62f, 1.9f, 1.05f)));
            Show("Choose the site and language, then click <b>Print label</b>.");
            Highlight();
        }

        /// <summary>Shows the registered context: GetSuperMetaData's copy, or null when nothing is registered.</summary>
        public void ShowContext(IReadOnlyDictionary<string, string> context)
        {
            var text = new StringBuilder("<b>REGISTERED CONTEXT</b>  <size=75%><color=#FFFFFF99>from GetSuperMetaData()</color></size>\n<size=80%>");
            if (context == null || context.Count == 0) text.Append("<color=#FFFFFF99>(nothing registered)</color>");
            else foreach (var pair in context.OrderBy(pair => pair.Key, StringComparer.Ordinal)) text.Append($"{pair.Key}  <b>{pair.Value}</b>\n");
            _context.text = text.Append("</size>").ToString();
        }

        /// <summary>
        /// Prints and applies a label from the station's settings. <paramref name="recordHasSite"/> says whether the
        /// label_printed record carried a site: without one, reports can't split this carton by site.
        /// </summary>
        public void ApplyLabel(bool recordHasSite)
        {
            if (_busy) return;
            StartCoroutine(Apply(recordHasSite));
        }

        private IEnumerator Apply(bool recordHasSite)
        {
            _busy = true;
            PrintButton.Interactable = false;
            var carton = _cartons.FirstOrDefault(c => Mathf.Abs(c.localPosition.x - HeadX) < 0.01f);

            yield return MoveTamp(-TampTravel, 0.25f);
            if (carton != null) AttachLabel(carton);
            yield return MoveTamp(0, 0.25f);

            var site = Sites.First(entry => entry.id == Site).name;
            Show(recordHasSite
                ? $"Labelled for <b>{site}</b>. <color=#05DA98><b>The record carries site, so reports split it by site.</b></color>"
                : $"Labelled for <b>{site}</b>, <color=#F5B841><b>but the record has no site: reports can't split this carton by site.</b></color>");
            SampleEvents.ReportFinished(recordHasSite ? SampleOutcome.Pass : SampleOutcome.Fail);

            yield return Index();
            PrintButton.Interactable = true;
            _busy = false;
        }

        private IEnumerator MoveTamp(float offset, float seconds)
        {
            if (_tamp == null) yield break;
            var from = _tamp.localPosition;
            var to = _tampRest + new Vector3(0, offset, 0);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                _tamp.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds));
                yield return null;
            }
            _tamp.localPosition = to;
        }

        /// <summary>Moves every carton one pitch down the line; the one leaving the end comes back as a fresh carton.</summary>
        private IEnumerator Index()
        {
            _conveyor.Running = true;
            var starts = _cartons.Select(c => c.localPosition).ToList();
            const float seconds = 0.9f;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                for (var i = 0; i < _cartons.Count; i++)
                    _cartons[i].localPosition = starts[i] + new Vector3(Spacing * Mathf.SmoothStep(0, 1, t / seconds), 0, 0);
                yield return null;
            }
            _conveyor.Running = false;
            for (var i = 0; i < _cartons.Count; i++)
            {
                var position = starts[i] + new Vector3(Spacing, 0, 0);
                if (position.x > HeadX + Spacing * 1.5f)
                {
                    position.x -= Spacing * _cartons.Count;
                    var label = _cartons[i].Find("Shipping Label");
                    if (label != null) Destroy(label.gameObject);
                }
                _cartons[i].localPosition = position;
            }
        }

        private Transform Carton(float x, int index)
        {
            var group = StationBuilder.Group(_conveyor.transform, $"Carton {index + 1}", new Vector3(x, BeltTop + CartonSize.y / 2f, 0));
            if (cartonModel != null)
            {
                var carton = StationBuilder.Model(group, "Carton", cartonModel, Vector3.zero, CartonSize / Conveyor.CartonModelSize);
                Conveyor.ShowCartonVariant(carton.GetComponent<Renderer>(), index);
            }
            else
                Block(group, "Carton", Vector3.zero, CartonSize, PropColors.Cardboard);
            return group;
        }

        private void AttachLabel(Transform carton)
        {
            var site = Sites.First(entry => entry.id == Site).name;
            // The carton's tape and flaps stand a few millimetres proud of its nominal top.
            var label = StationBuilder.Group(carton, "Shipping Label", new Vector3(0, CartonSize.y / 2f + 0.006f, 0));
            Block(label, "Label", Vector3.zero, new Vector3(0.2f, 0.002f, 0.14f), Color.white);
            var text = StationBuilder.Text(label, $"<size=60%>SHIP FROM</size>\n<b>{site.ToUpperInvariant()}</b>\n<size=60%>{Language.ToUpperInvariant()}</size>",
                new Vector3(0, 0.0015f, 0), 0.3f, UiKit.Ink, 0.18f, 0.12f);
            text.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        }

        private void Choose(string site)
        {
            // Choosing the current site again registers it again, after Unregister site or Reset.
            if (_busy) return;
            Site = site;
            Highlight();
            SiteChosen?.Invoke(site);
        }

        private void ChooseLanguage(string languageCode)
        {
            if (_busy) return;
            Language = languageCode;
            Highlight();
            LanguageChosen?.Invoke(languageCode);
        }

        private void Highlight()
        {
            foreach (var pair in _siteButtons) pair.Value.SetBaseColor(pair.Key == Site ? UiKit.Purple : UiKit.NavyLighter);
            foreach (var pair in _languageButtons) pair.Value.SetBaseColor(pair.Key == Language ? UiKit.Purple : UiKit.NavyLighter);
        }

        private void Show(string message) => _status.text = $"<b>OUTBOUND LINE 2 · LABELLER</b>\n<size=80%>{message}</size>";

        private void BuildLabeller(Transform root)
        {
            if (labellerModel != null)
            {
                var model = StationBuilder.Model(root, "Labeller Model", labellerModel, Vector3.zero, Vector3.one).transform;
                _tamp = model.Find("Tamp Pad");
            }
            else
            {
                Block(root, "Post", new Vector3(0, 0.6f, 0), new Vector3(0.1f, 1.2f, 0.1f), PropColors.DarkSteel);
                Block(root, "Print Engine", new Vector3(0, 1.24f, 0.03f), new Vector3(0.4f, 0.36f, 0.42f), PropColors.DarkSteel);
                Block(root, "Arm", new Vector3(0, 1.12f, -0.3f), new Vector3(0.16f, 0.04f, 0.4f), PropColors.Steel);
                _tamp = StationBuilder.Group(root, "Tamp Pad", new Vector3(0, 1.06f, -0.45f));
                Block(_tamp, "Pad", new Vector3(0, 0.006f, 0), new Vector3(0.12f, 0.012f, 0.1f), PropColors.Rubber);
            }
            _tampRest = _tamp.localPosition;
        }

        private void BuildPanel(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.6f, 0.98f), material);
            _status = StationBuilder.Text(root, "", new Vector3(-0.08f, 0.4f, -0.025f), 0.42f, Color.white, 1.36f, 0.14f, TextAlignmentOptions.TopLeft);
            StationBuilder.Text(root, "<b>SITE</b>", new Vector3(-0.62f, 0.2f, -0.025f), 0.4f, Color.white, 0.3f, 0.08f, TextAlignmentOptions.Left);
            StationBuilder.Text(root, "<b>LANGUAGE</b>", new Vector3(-0.62f, 0.05f, -0.025f), 0.4f, Color.white, 0.3f, 0.08f, TextAlignmentOptions.Left);
            for (var i = 0; i < Sites.Length; i++)
            {
                var (id, name) = Sites[i];
                var button = StationBuilder.Button(root, name, new Vector3(-0.22f + i * 0.38f, 0.2f, -0.03f), new Vector2(0.34f, 0.11f), material, UiKit.Purple, 0.36f);
                button.Activated += _ => Choose(id);
                _siteButtons[id] = button;
            }
            for (var i = 0; i < Languages.Length; i++)
            {
                var (code, name) = Languages[i];
                var button = StationBuilder.Button(root, name, new Vector3(-0.22f + i * 0.38f, 0.05f, -0.03f), new Vector2(0.34f, 0.11f), material, UiKit.Purple, 0.36f);
                button.Activated += _ => ChooseLanguage(code);
                _languageButtons[code] = button;
            }

            _context = StationBuilder.Text(root, "", new Vector3(-0.2f, -0.27f, -0.025f), 0.42f, Color.white, 1.1f, 0.36f, TextAlignmentOptions.TopLeft);
            PrintButton = StationBuilder.Button(root, "Print label", new Vector3(0.6f, 0.2f, -0.03f), new Vector2(0.32f, 0.12f), material, PropColors.SafetyGreen, 0.36f);
            PrintButton.Activated += _ => { if (!_busy) PrintRequested?.Invoke(); };
            UnregisterSiteButton = StationBuilder.Button(root, "Unregister site", new Vector3(0.6f, 0.03f, -0.03f), new Vector2(0.32f, 0.11f), material, UiKit.NavyLighter, 0.3f);
            UnregisterSiteButton.Activated += _ => UnregisterSiteRequested?.Invoke();
            ResetButton = StationBuilder.Button(root, "Reset", new Vector3(0.6f, -0.13f, -0.03f), new Vector2(0.32f, 0.11f), material, UiKit.NavyLighter, 0.3f);
            ResetButton.Activated += _ => ResetRequested?.Invoke();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
