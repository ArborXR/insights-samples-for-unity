using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The persistent frame around every sample: header, intro, Insights Stream column, navigation,
    /// deep links, and autopilot runs. Created automatically when any scene plays, so you can open a sample
    /// scene and press Play.
    /// </summary>
    public class SampleShell : MonoBehaviour
    {
        public const float StreamWidth = 600f;
        public const float HeaderHeight = 72f;
        // Below this canvas width (reference pixels), the Stream starts hidden so the station keeps most of
        // the screen, as on a phone in portrait.
        private const float NarrowWidth = 1400f;
        // Stations are framed for a view at least this wide for its height; taller views widen the field of view.
        private const float MinViewAspect = 1f;
        public const string HomeScene = "Home";
        public const string LaunchScene = "Launch";
        private const string PrefabPath = "SampleShell";
        private const string PairingSampleId = "18-pairing";

        public static SampleShell Instance { get; private set; }

        /// <summary>Raised after a sample (or the browser, with null) has opened.</summary>
        public static event Action<SampleInfo> SampleOpened;

        /// <summary>Raised when the Stream column is shown or hidden.</summary>
        public static event Action LayoutChanged;

        public RectTransform StreamColumn { get; private set; }
        public SampleInfo Current { get; private set; }
        public bool StreamVisible { get; private set; } = true;

        /// <summary>The width the Stream column takes from the right of the screen, in reference pixels.</summary>
        public float StreamSpace => StreamVisible ? StreamWidth : 0f;

        /// <summary>A rect over the whole screen and above the rest of the shell, for menus and popovers.</summary>
        public RectTransform PopoverLayer { get; private set; }

        private Canvas _canvas;
        private RectTransform _header;
        private RectTransform _brand;
        private RectTransform _crumbs;
        private RectTransform _currentSample;
        private RectTransform _stepper;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _position;
        private Button _previous;
        private Button _next;
        private Button _soundToggle;
        private Button _introToggle;
        private Button _streamToggle;
        private GameObject _unseenBadge;
        private TextMeshProUGUI _unseenCount;
        private bool _streamToggled;
        private Camera _fittedCamera;
        private float _baseFieldOfView;
        private TextMeshProUGUI _connection;
        private Image _connectionDot;
        private Image _connectionHalo;
        private Image _connectionOutline;
        private Hover _connectionHover;
        private Button _connectionButton;
        private string _pillLabel;
        private bool _pillLabelStandsAlone;
        private Action _pillClick;
        private RectTransform _view;
        private SampleIntro _intro;
        private SampleSwitcher _switcher;
        private SoundMenu _soundMenu;
        private RectTransform _cursor;
        private bool _switching;
        private bool _suiteRunning;
        private Coroutine _autopilot;
        private SampleOutcome? _reportedOutcome;
        private bool _lastRunPassed;
        private string _lastRunProblem;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureShell()
        {
            if (Instance != null) return;
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab != null) Instantiate(prefab).name = "Sample Shell";
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            // The on-screen development console would cover the Run sample buttons. Logs still reach the browser
            // console (or logcat), where automation reads them.
            Debug.developerConsoleEnabled = false;
            UiKit.EnsureEventSystem(transform);
            BuildUi();
            CreateAppWideObjects();

            ShellSignals.Changed += Refresh;
            SampleEvents.Finished += HandleFinished;
        }

        private void Start()
        {
            var linked = DeepLinks.Get(DeepLinks.Sample);
            var suite = DeepLinks.Get(DeepLinks.Suite);
            var activeScene = SceneManager.GetActiveScene().name;
            var playingSample = SampleCatalog.Instance.FindByScene(activeScene);

            if (!string.IsNullOrEmpty(suite)) StartCoroutine(RunSuite(suite));
            else if (!string.IsNullOrEmpty(linked)) Open(linked, DeepLinks.Get(DeepLinks.Autopilot));
            else if (playingSample != null) StartCoroutine(Enter(playingSample, null));
            else if (activeScene == LaunchScene) OpenHome();
            else StartCoroutine(Enter(null, null));
        }

        private void RefreshSoundToggle() => UiKit.SetToggled(_soundToggle, _soundMenu.IsOpen, SoundMenu.Icon);

        private void OnDestroy()
        {
            if (Instance != this) return;
            UiSounds.Changed -= RefreshSoundToggle;
            ShellSignals.Changed -= Refresh;
            SampleEvents.Finished -= HandleFinished;
            Instance = null;
        }

        private void LateUpdate()
        {
            FitStreamToScreen();
            FitCameraToViewport();
            _intro.FitToView();
        }

        public void OpenHome()
        {
            if (!_suiteRunning) StartCoroutine(Switch(null, null));
        }

        /// <summary>Opens a sample, optionally running its autopilot. Ignored while a suite runs: it owns the shell.</summary>
        public void Open(string sampleId, string autopilot = null)
        {
            if (_suiteRunning) return;
            var sample = SampleCatalog.Instance.Find(sampleId);
            if (sample == null)
            {
                Debug.LogWarning($"Unknown sample '{sampleId}'. Opening the sample browser instead.");
                OpenHome();
                return;
            }
            StartCoroutine(Switch(sample, autopilot));
        }

        public void RunAutopilot(SampleOutcome outcome)
        {
            if (Current == null || _suiteRunning) return;
            if (_autopilot != null) StopCoroutine(_autopilot);
            _autopilot = StartCoroutine(RunAutopilotRoutine(outcome));
        }

        private IEnumerator Switch(SampleInfo sample, string autopilot)
        {
            if (_switching) yield break;
            _switching = true;
            StopAutopilot();

            foreach (var lifecycle in FindLifecycles()) lifecycle.OnSampleExit();
            yield return SceneManager.LoadSceneAsync(sample != null ? sample.sceneName : HomeScene);

            _switching = false;
            yield return Enter(sample, autopilot);
        }

        private IEnumerator Enter(SampleInfo sample, string autopilot)
        {
            Current = sample;
            _intro.Show(sample);
            Refresh();
            SampleLog.Opened(sample);
            SampleOpened?.Invoke(sample);
            foreach (var lifecycle in FindLifecycles()) lifecycle.OnSampleEnter();

            yield return null;
            if (sample != null && TryParseOutcome(autopilot, out var outcome)) RunAutopilot(outcome);
        }

        private IEnumerator RunAutopilotRoutine(SampleOutcome outcome)
        {
            var autopilot = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISampleAutopilot>().FirstOrDefault();
            if (autopilot == null)
            {
                Debug.LogWarning($"Sample '{Current.id}' has no autopilot.");
                yield break;
            }

            _reportedOutcome = null;
            SampleLog.Started(Current, outcome);
            RunCheck.Begin();
            _cursor.gameObject.SetActive(true);
            _cursor.position = new Vector2(Screen.width * 0.3f, Screen.height * 0.4f);

            // The intro steps aside when the autopilot reaches for the station.
            yield return autopilot.Run(outcome, new AutopilotDriver(_cursor, fast: DeepLinks.Get(DeepLinks.Pace) == "fast", onFirstAction: _intro.Dismiss));

            var deadline = Time.realtimeSinceStartup + 15f;
            while (_reportedOutcome == null && Time.realtimeSinceStartup < deadline) yield return null;

            _cursor.gameObject.SetActive(false);
            var problems = RunCheck.End(outcome == SampleOutcome.Pass ? Current.expectedOnPass : Current.expectedOnFail);
            SampleLog.Finished(Current, outcome, _reportedOutcome, problems);
            _lastRunPassed = _reportedOutcome == outcome && problems.Count == 0;
            _lastRunProblem = _reportedOutcome != outcome
                ? $"result {(_reportedOutcome.HasValue ? _reportedOutcome.Value.ToString().ToLowerInvariant() : "timeout")}"
                : string.Join("; ", problems);
            _autopilot = null;
        }

        /// <summary>
        /// Plays each listed sample (comma-separated IDs, or "all") to a pass and then a fail, reopening the
        /// sample before each run, and logs one summary line at the end.
        /// </summary>
        private IEnumerator RunSuite(string list)
        {
            var samples = list == "all"
                ? SampleCatalog.Instance.samples.Where(sample => sample != null).ToList()
                : list.Split(',').Select(id => SampleCatalog.Instance.Find(id.Trim())).ToList();
            if (samples.Contains(null))
            {
                Debug.LogWarning($"Unknown sample in suite '{list}'. Opening the sample browser instead.");
                OpenHome();
                yield break;
            }

            _suiteRunning = true;
            var failures = new List<string>();
            foreach (var sample in samples)
            foreach (var outcome in new[] { SampleOutcome.Pass, SampleOutcome.Fail })
            {
                yield return Switch(sample, null);
                yield return RunAutopilotRoutine(outcome);
                if (!_lastRunPassed) failures.Add($"{sample.id} {outcome.ToString().ToLowerInvariant()}: {_lastRunProblem}");
            }
            _suiteRunning = false;
            SampleLog.SuiteFinished(samples.Count * 2, failures);
        }

        private void StopAutopilot()
        {
            if (_autopilot != null)
            {
                StopCoroutine(_autopilot);
                RunCheck.Cancel();
            }
            _autopilot = null;
            if (_cursor != null) _cursor.gameObject.SetActive(false);
        }

        private void HandleFinished(SampleOutcome outcome) => _reportedOutcome = outcome;

        private static bool TryParseOutcome(string value, out SampleOutcome outcome)
        {
            outcome = value == "fail" ? SampleOutcome.Fail : SampleOutcome.Pass;
            return value == "pass" || value == "fail";
        }

        private static IEnumerable<ISampleLifecycle> FindLifecycles() =>
            FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISampleLifecycle>();

        private void CreateAppWideObjects()
        {
            foreach (var sample in SampleCatalog.Instance.samples)
            {
                if (sample == null || sample.appWidePrefab == null) continue;
                var instance = Instantiate(sample.appWidePrefab);
                instance.name = sample.appWidePrefab.name;
                DontDestroyOnLoad(instance);
            }
        }

        /// <summary>The 3D view fills the area left of the Stream column (when shown) and below the header.</summary>
        private void FitCameraToViewport()
        {
            var camera = Camera.main;
            if (camera == null || _canvas == null || Screen.width == 0 || Screen.height == 0) return;

            var scale = _canvas.scaleFactor;
            var rect = new Rect(0, 0,
                Mathf.Clamp01(1f - StreamSpace * scale / Screen.width),
                Mathf.Clamp01(1f - HeaderHeight * scale / Screen.height));
            if (camera.rect != rect) camera.rect = rect;

            // On a tall view, such as a phone in portrait, widen the field of view to keep the station's width.
            if (camera != _fittedCamera)
            {
                _fittedCamera = camera;
                _baseFieldOfView = camera.fieldOfView;
            }
            var fieldOfView = _baseFieldOfView;
            if (camera.aspect < MinViewAspect)
            {
                var halfWidth = Mathf.Tan(_baseFieldOfView * 0.5f * Mathf.Deg2Rad) * MinViewAspect;
                fieldOfView = 2f * Mathf.Atan(halfWidth / camera.aspect) * Mathf.Rad2Deg;
            }
            if (!Mathf.Approximately(camera.fieldOfView, fieldOfView)) camera.fieldOfView = fieldOfView;
        }

        /// <summary>Hides the Stream on narrow screens until the learner chooses otherwise.</summary>
        private void FitStreamToScreen()
        {
            if (_streamToggled || _canvas == null || Screen.width == 0) return;
            var wide = Screen.width / _canvas.scaleFactor >= NarrowWidth;
            if (StreamVisible != wide) SetStreamVisible(wide);
        }

        /// <summary>Shows or hides the Stream column, and keeps it that way whatever the screen's width.</summary>
        public void ToggleStream()
        {
            _streamToggled = true;
            SetStreamVisible(!StreamVisible);
        }

        /// <summary>The number of records the Stream took in while hidden, for the badge on its toggle.</summary>
        public void SetUnseenRecords(int count)
        {
            _unseenCount.text = count > 99 ? "99+" : count.ToString();
            _unseenBadge.SetActive(count > 0 && !StreamVisible);
        }

        private void SetStreamVisible(bool visible)
        {
            StreamVisible = visible;
            StreamColumn.gameObject.SetActive(visible);
            _header.offsetMax = new Vector2(-StreamSpace, 0);
            _view.offsetMax = new Vector2(-StreamSpace, -HeaderHeight);
            _intro.FitToView();
            UiKit.SetToggled(_streamToggle, visible);
            if (visible) _unseenBadge.SetActive(false);
            LayoutChanged?.Invoke();
        }

        /// <summary>
        /// Lets an app-wide addition, such as a build that asks for its App Token, own the header's connection pill:
        /// a click runs <paramref name="onClick"/>, and <paramref name="label"/> follows the connection state, or
        /// replaces it when <paramref name="standsAlone"/>. A null <paramref name="onClick"/> gives the pill back.
        /// </summary>
        public void SetConnectionPill(string label, bool standsAlone, Action onClick)
        {
            _pillLabel = label;
            _pillLabelStandsAlone = standsAlone;
            _pillClick = onClick;
            Refresh();
        }

        private void Refresh()
        {
            if (_title == null) return;

            var home = Current == null;
            _brand.gameObject.SetActive(home);
            _crumbs.gameObject.SetActive(!home);
            _stepper.gameObject.SetActive(!home);
            _introToggle.gameObject.SetActive(!home);
            if (!home)
            {
                var catalog = SampleCatalog.Instance;
                var number = Theme.Paint(Current.number.ToString("00"), catalog.StyleFor(Current.series).Tint(0.8f, 0.12f));
                _title.text = $"{number}  {Current.title}";
                _position.text = $"{Array.IndexOf(catalog.samples, Current) + 1}{Theme.Paint($"/{catalog.samples.Length}", Theme.TextFaint)}";
                UiKit.Rigid(_position);
                UiKit.SetInteractable(_previous, catalog.Neighbor(Current, -1) != null);
                UiKit.SetInteractable(_next, catalog.Neighbor(Current, +1) != null);
            }

            var (dot, text) = ShellSignals.Connection switch
            {
                ConnectionState.SignedIn => (Theme.Mint, ShellSignals.ConnectionDetail ?? "Signed in"),
                ConnectionState.NotPaired => (Theme.Amber, ShellSignals.ConnectionDetail ?? "Not paired"),
                ConnectionState.NotSignedIn => (Theme.TextFaint, ShellSignals.ConnectionDetail ?? "Not signed in"),
                _ => (Theme.TextFaint, "Starting…"),
            };
            // Pairing is sample 18: from anywhere else, the pill opens it, unless an app-wide addition owns the pill.
            var owned = _pillClick != null;
            var opensPairing = !owned && ShellSignals.Connection == ConnectionState.NotPaired && Current?.id != PairingSampleId;
            var callsForAction = opensPairing || (owned && _pillLabelStandsAlone);
            if (owned && _pillLabelStandsAlone) (dot, text) = (Theme.Amber, _pillLabel);
            else if (owned) text += $"  ·  {_pillLabel}";
            else if (opensPairing) text += "  ·  Pair in 18";
            _connectionButton.enabled = owned || opensPairing;
            _connection.text = text;
            UiKit.Rigid(_connection);
            _connectionDot.color = dot;
            _connectionHalo.color = new Color(dot.r, dot.g, dot.b, 0.2f);
            var fill = callsForAction ? Theme.Oklch(0.56f, 0.21f, Theme.BrandHue, 0.14f) : new Color(1f, 1f, 1f, 0.04f);
            _connectionHover.SetColors(fill, callsForAction ? Theme.Selected : owned ? Theme.Hover : fill);
            _connectionOutline.color = callsForAction ? Theme.Oklch(0.56f, 0.21f, Theme.BrandHue, 0.6f) : Theme.LineStrong;

            _intro.SetHidden(ShellSignals.SdkPromptOpen);
        }

        private void BuildUi()
        {
            _canvas = UiKit.CreateScreenCanvas(transform, "Shell Canvas", sortingOrder: 50);
            var root = (RectTransform)_canvas.transform;

            StreamColumn = UiKit.Rect(root, "Stream Column");
            StreamColumn.anchorMin = new Vector2(1, 0);
            StreamColumn.anchorMax = new Vector2(1, 1);
            StreamColumn.pivot = new Vector2(1, 0.5f);
            StreamColumn.sizeDelta = new Vector2(StreamWidth, 0);
            StreamColumn.anchoredPosition = Vector2.zero;

            _header = UiKit.Panel(root, "Header", Theme.Bar).rectTransform;
            _header.anchorMin = new Vector2(0, 1);
            _header.anchorMax = new Vector2(1, 1);
            _header.pivot = new Vector2(0, 1);
            _header.offsetMin = new Vector2(0, -HeaderHeight);
            _header.offsetMax = new Vector2(-StreamWidth, 0);
            var edge = UiKit.Panel(_header, "Edge", Theme.Line);
            edge.raycastTarget = false;
            edge.rectTransform.anchorMin = Vector2.zero;
            edge.rectTransform.anchorMax = new Vector2(1, 0);
            edge.rectTransform.pivot = new Vector2(0.5f, 0);
            edge.rectTransform.sizeDelta = new Vector2(0, 1);

            var row = UiKit.Row(_header, "Row", 8f);
            UiKit.Stretch(row, left: 20, right: 14);
            BuildBrand(row);
            BuildCrumbs(row);
            BuildStepper(row);
            UiKit.Spacer(row);
            BuildConnection(row);
            UiKit.Separator(row, 22f);
            _soundToggle = UiKit.IconButton(row, SoundMenu.Icon, () => _soundMenu.Toggle((RectTransform)_soundToggle.transform));
            _introToggle = UiKit.IconButton(row, "book-open", () => _intro.Toggle());
            _streamToggle = UiKit.IconButton(row, "terminal", ToggleStream);
            UiKit.SetToggled(_streamToggle, StreamVisible);
            BuildUnseenBadge();

            _view = UiKit.Rect(root, "View");
            UiKit.Stretch(_view, right: StreamWidth, top: HeaderHeight);
            // Run sample reopens the sample first, as a suite does: its scene starts over and anything it left open is closed.
            _intro = new SampleIntro(_view, outcome => { if (Current != null) Open(Current.id, outcome == SampleOutcome.Fail ? "fail" : "pass"); });
            _intro.Changed += () => UiKit.SetToggled(_introToggle, _intro.Showing);

            PopoverLayer = UiKit.Rect(root, "Popovers");
            UiKit.Stretch(PopoverLayer);
            _switcher = new SampleSwitcher(PopoverLayer, id => Open(id));
            _soundMenu = new SoundMenu(PopoverLayer);
            _soundMenu.Changed += RefreshSoundToggle;
            UiSounds.Changed += RefreshSoundToggle;

            _cursor = UiKit.Pointer(root, "Autopilot Cursor", UiKit.Purple, 44f).rectTransform;
            _cursor.gameObject.SetActive(false);
        }

        /// <summary>On the sample browser, the header names the app.</summary>
        private void BuildBrand(Transform row)
        {
            _brand = UiKit.Row(row, "Brand", 14f);
            var company = UiKit.Label(_brand, "ArborXR", 23f, Theme.Text, FontStyles.Bold);
            company.textWrappingMode = TextWrappingModes.NoWrap;
            company.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.Rigid(company);
            UiKit.Separator(_brand, 22f);
            UiKit.Rigid(UiKit.Caption(_brand, "Insights Samples", 19f, Theme.TextMuted));
        }

        /// <summary>In a sample, the header says which one, with the way back and a menu of the others.</summary>
        private void BuildCrumbs(Transform row)
        {
            _crumbs = UiKit.Row(row, "Crumbs", 4f);
            UiKit.ShellButton(_crumbs, "Samples", "layout-grid", OpenHome, ButtonLook.Ghost, 40f);
            UiKit.Rigid(UiKit.Caption(_crumbs, "/", 17f, Theme.TextFaint));

            var current = UiKit.Card(_crumbs, "Current Sample", Color.clear, 11f);
            _currentSample = current.rectTransform;
            var button = current.gameObject.AddComponent<Button>();
            button.targetGraphic = current;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => _switcher.Show(_currentSample, Current));
            current.gameObject.AddComponent<Hover>().Init(current, Color.clear, Theme.Hover);
            var layout = current.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 10, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var size = current.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 44f;

            // The title is the one part of the header that gives way on a narrow screen.
            _title = UiKit.Label(current.transform, "", 21f, Theme.Text, FontStyles.Bold);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Ellipsis;
            _title.alignment = TextAlignmentOptions.MidlineLeft;
            UiKit.Icon(current.transform, "chevron-down", 16f, Theme.TextFaint);
        }

        private void BuildStepper(Transform row)
        {
            var stepper = UiKit.Card(row, "Stepper", Color.clear, 12f);
            stepper.raycastTarget = false;
            _stepper = stepper.rectTransform;
            var layout = stepper.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 3, 3);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _previous = UiKit.IconButton(_stepper, "chevron-left", () => OpenNeighbor(-1), 36f, 18f);
            _position = UiKit.Caption(_stepper, "", 15f, Theme.Text);
            _position.margin = new Vector4(8, 0, 8, 0);
            _next = UiKit.IconButton(_stepper, "chevron-right", () => OpenNeighbor(+1), 36f, 18f);
            UiKit.Outline(_stepper, 12f, Theme.Line);
        }

        private void BuildConnection(Transform row)
        {
            var pill = UiKit.Card(row, "Connection", Color.clear, 20f);
            _connectionButton = pill.gameObject.AddComponent<Button>();
            _connectionButton.targetGraphic = pill;
            _connectionButton.transition = Selectable.Transition.None;
            _connectionButton.onClick.AddListener(() =>
            {
                if (_pillClick != null) _pillClick();
                else Open(PairingSampleId);
            });
            _connectionHover = pill.gameObject.AddComponent<Hover>();
            _connectionHover.Init(pill, Color.clear, Color.clear);
            var layout = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(13, 15, 0, 0);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var size = pill.gameObject.AddComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 40f;

            _connectionHalo = UiKit.Card(pill.transform, "Status", Color.clear, 7f);
            _connectionHalo.raycastTarget = false;
            var haloSize = _connectionHalo.gameObject.AddComponent<LayoutElement>();
            haloSize.minWidth = haloSize.preferredWidth = haloSize.minHeight = haloSize.preferredHeight = 14f;
            _connectionDot = UiKit.Card(_connectionHalo.transform, "Dot", Theme.TextFaint, 4f);
            _connectionDot.raycastTarget = false;
            _connectionDot.rectTransform.sizeDelta = new Vector2(8f, 8f);

            _connection = UiKit.Caption(pill.transform, "Starting…", 15.5f, Theme.Text);
            _connectionOutline = UiKit.Outline(pill.transform, 20f, Theme.LineStrong);
        }

        /// <summary>A count on the Stream's toggle of the records that arrived while the Stream was hidden.</summary>
        private void BuildUnseenBadge()
        {
            var badge = UiKit.Card(_streamToggle.transform, "Unseen", Theme.Accent, 10f);
            badge.raycastTarget = false;
            _unseenBadge = badge.gameObject;
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(1f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(7f, 3f);
            var layout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 1, 1);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var size = badge.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.minHeight = 20f;
            var fitter = badge.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _unseenCount = UiKit.Label(badge.transform, "", 12.5f, Color.white, FontStyles.Bold);
            _unseenCount.textWrappingMode = TextWrappingModes.NoWrap;
            _unseenCount.alignment = TextAlignmentOptions.Center;
            _unseenBadge.SetActive(false);
        }

        private void OpenNeighbor(int step)
        {
            var neighbor = SampleCatalog.Instance.Neighbor(Current, step);
            if (neighbor != null) Open(neighbor.id);
        }
    }
}
