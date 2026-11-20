using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Stream
{
    /// <summary>
    /// The Insights Stream column: one row per SDK record, newest first, which opens to show the record's data.
    /// It only observes the SDK; it never records data.
    /// </summary>
    [RequireComponent(typeof(SampleShell))]
    public class InsightsStream : MonoBehaviour
    {
        private const int MaxRows = 150;
        private const float HeaderHeight = 54f;
        private const float FiltersHeight = 50f;
        private const float FooterHeight = 40f;
        private const string Queued = "Queued";
        private const string Sent = "Sent";

        private static readonly Color RowHover = new Color(1f, 1f, 1f, 0.045f);
        private static readonly Color RowAlert = Theme.Oklch(0.6f, 0.2f, 25f, 0.12f);
        private static readonly Color RowFlash = Theme.Oklch(0.7f, 0.15f, Theme.BrandHue, 0.22f);
        private static readonly Regex CallTokens = new Regex(@"""(?:[^""\\]|\\.)*""|\b(?:true|false|null)\b|-?\b\d+(?:\.\d+)?\b");

        /// <summary>One line of the Stream: a record, or (with no record) a divider between samples.</summary>
        private sealed class Row
        {
            public StreamRecord Record;
            public GameObject Root;
            public Image Summary;
            public Hover Hover;
            public RectTransform Chevron;
            public TextMeshProUGUI Time;
            public TextMeshProUGUI Call;
            public TextMeshProUGUI Count;
            public Image StatusDot;
            public TextMeshProUGUI Status;
            public GameObject Detail;
            public bool Open;
            public bool PayloadOpen;
        }

        private static InsightsStream Instance { get; set; }

        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<StreamRecord, Row> _rowOf = new Dictionary<StreamRecord, Row>();
        private readonly Dictionary<string, Row> _liveTelemetry = new Dictionary<string, Row>();
        private readonly Dictionary<long, StreamRecord> _byRecordId = new Dictionary<long, StreamRecord>();
        private readonly HashSet<long> _queued = new HashSet<long>();
        private readonly List<StreamRecord> _pending = new List<StreamRecord>();
        private readonly List<StreamRecord> _held = new List<StreamRecord>();
        private readonly Dictionary<StreamFilter, (Hover hover, TextMeshProUGUI label, TextMeshProUGUI count)> _filters =
            new Dictionary<StreamFilter, (Hover, TextMeshProUGUI, TextMeshProUGUI)>();
        private SampleShell _shell;
        private RectTransform _list;
        private TextMeshProUGUI _empty;
        private Button _pauseButton;
        private Button _sdkToggle;
        private Image _live;
        private Image _liveHalo;
        private GameObject _info;
        private TextMeshProUGUI _batchNote;
        private Image _footerDot;
        private TextMeshProUGUI _footerText;
        private GameObject _resume;
        private StreamFilter _filter = StreamFilter.All;
        private bool _showSdk = true;
        private bool _paused;
        private int _unseen;
        private int _sequence;
        private SampleInfo _current;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            AbxrObserver.OnRecordCreated += HandleRecordCreated;
            AbxrObserver.OnRecordsSent += HandleRecordsSent;
            Abxr.OnAuthCompleted += HandleAuthCompleted;
            Abxr.OnPairingStateChanged += HandlePairingStateChanged;
            Abxr.OnUserDataSyncCompleted += HandleUserDataSynced;
            Abxr.OnModuleTarget += HandleModuleTarget;
            Abxr.OnAllModulesCompleted += HandleAllModulesCompleted;
            SampleShell.SampleOpened += HandleSampleOpened;
            SampleShell.LayoutChanged += HandleLayoutChanged;
        }

        private void Start()
        {
            _shell = GetComponent<SampleShell>();
            Build(_shell.StreamColumn);
            foreach (var record in _pending) Show(record, flash: true);
            _pending.Clear();
            RefreshFooter();
        }

        private void Update()
        {
            // The live dot breathes while records are being shown as they arrive.
            if (_live == null || _paused) return;
            var color = _live.color;
            color.a = Mathf.Lerp(0.4f, 1f, 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI));
            _live.color = color;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            AbxrObserver.OnRecordCreated -= HandleRecordCreated;
            AbxrObserver.OnRecordsSent -= HandleRecordsSent;
            Abxr.OnAuthCompleted -= HandleAuthCompleted;
            Abxr.OnPairingStateChanged -= HandlePairingStateChanged;
            Abxr.OnUserDataSyncCompleted -= HandleUserDataSynced;
            Abxr.OnModuleTarget -= HandleModuleTarget;
            Abxr.OnAllModulesCompleted -= HandleAllModulesCompleted;
            SampleShell.SampleOpened -= HandleSampleOpened;
            SampleShell.LayoutChanged -= HandleLayoutChanged;
            Instance = null;
        }

        private void HandleRecordCreated(AbxrObserver.Record source)
        {
            var method = source.Method ?? InferredMethod(source);
            var record = new StreamRecord
            {
                RecordId = source.Id,
                Kind = KindOf(method),
                Method = method,
                Name = source.Name,
                Automatic = source.Automatic,
            };

            // Your data: the metadata the app passed, plus the arguments the SDK copied into the record (a score, a
            // status). Everything else in the record is what the SDK added.
            var arguments = ArgumentKeys(method);
            if (source.CallerData != null) record.CallerData.AddRange(source.CallerData);
            foreach (var pair in source.Data)
            {
                if (source.CallerData != null && source.CallerData.TryGetValue(pair.Key, out var passed) && passed == pair.Value) continue;
                if (!source.Automatic && arguments.Contains(pair.Key)) record.CallerData.Add(pair);
                else record.SdkData.Add(pair);
            }
            if (method == "EventCritical") record.SdkData.Insert(0, new KeyValuePair<string, string>("recorded as", source.Name));

            record.Call = FormatCall(record, source);
            (record.Status, record.Tone) = source.Dropped ? (DropText(source.DropReason), StatusTone.Bad) : (Queued, StatusTone.Neutral);
            Add(record);
        }

        private void HandleRecordsSent(AbxrObserver.SendResult result)
        {
            var (status, tone) = result.Status switch
            {
                AbxrObserver.SendStatus.Sent => (Sent, StatusTone.Good),
                AbxrObserver.SendStatus.Failed => (result.HttpStatus > 0 ? $"Send failed: HTTP {result.HttpStatus}" : "Send failed", StatusTone.Warning),
                _ => (DropText(result.DropReason), StatusTone.Bad),
            };
            if (SampleLog.Enabled)
                Debug.Log("[insights-stream] " + new JsonText().Raw("ids", "[" + string.Join(",", result.RecordIds) + "]").Field("status", status));

            foreach (var id in result.RecordIds)
            {
                _queued.Remove(id);
                if (!_byRecordId.TryGetValue(id, out var record)) continue;
                record.Status = status;
                record.Tone = tone;
                if (!_rowOf.TryGetValue(record, out var row)) continue;
                ShowStatus(row);
                RefreshDetail(row);
            }
            if (_list != null) RefreshFooter();
        }

        private void Add(StreamRecord record)
        {
            record.Sequence = ++_sequence;
            record.Time = DateTime.Now;
            record.Feeds ??= _current != null ? _current.DashboardNoteFor(record.Method, record.Name) : null;
            if (SampleLog.Enabled) Debug.Log("[insights-stream] " + record.ToJson());
            RunCheck.Observe(record.Method, record.Name, record.CallerData.Concat(record.SdkData));
            StationInteractable.ActivatedThisFrame?.Flash();

            if (record.RecordId != 0)
            {
                _byRecordId[record.RecordId] = record;
                if (record.Status == Queued) _queued.Add(record.RecordId);
            }

            if (_list == null)
            {
                _pending.Add(record);
                return;
            }
            // While paused, records wait off screen until Resume.
            if (_paused)
            {
                _held.Add(record);
                if (_held.Count > MaxRows) _held.RemoveAt(0);
            }
            else Show(record, flash: true);
            RefreshFooter();
        }

        private void Show(StreamRecord record, bool flash)
        {
            if (!_shell.StreamVisible) _shell.SetUnseenRecords(++_unseen);
            if (record.Kind != StreamKind.Telemetry)
            {
                CreateRow(record, flash);
                return;
            }

            // High-frequency telemetry collapses into one live row per name.
            var name = record.Name ?? "";
            if (_liveTelemetry.TryGetValue(name, out var live) && live.Root != null)
            {
                record.Samples = live.Record.Samples + 1;
                record.FirstTime = live.Record.FirstTime;
                Forget(live.Record);
                live.Record = record;
                _rowOf[record] = live;
                Fill(live);
                RefreshDetail(live);
                return;
            }

            record.FirstTime = record.Time;
            _liveTelemetry[name] = CreateRow(record, flash);
        }

        private void HandleLayoutChanged()
        {
            if (_shell != null && _shell.StreamVisible) _unseen = 0;
        }

        private void HandleAuthCompleted(bool success, string errorMessage)
        {
            if (success && _batchNote != null)
            {
                _batchNote.text = BatchNote();
                RefreshFooter();
            }
            Add(new StreamRecord
            {
                Kind = StreamKind.Auth,
                Method = "OnAuthCompleted",
                Name = success ? "signed_in" : "sign_in_failed",
                Call = $"Abxr.OnAuthCompleted({(success ? "true" : "false")}, {FormatValue(errorMessage, quoted: true)})",
                Status = success ? "Signed in" : "Not signed in",
                Tone = success ? StatusTone.Good : StatusTone.Warning,
                Automatic = true,
            });
        }

        private void HandlePairingStateChanged(Abxr.PairingState state, Abxr.PairingChangeReason reason)
        {
            Add(new StreamRecord
            {
                Kind = StreamKind.Pairing,
                Method = "OnPairingStateChanged",
                Name = state.ToString().ToLowerInvariant(),
                Call = $"Abxr.OnPairingStateChanged(Abxr.PairingState.{state}, Abxr.PairingChangeReason.{reason})",
                Status = state.ToString(),
                Tone = state == Abxr.PairingState.Paired ? StatusTone.Good : StatusTone.Neutral,
                Automatic = true,
            });
        }

        private void HandleUserDataSynced(bool success, string errorMessage)
        {
            Add(new StreamRecord
            {
                Kind = StreamKind.Identity,
                Method = "OnUserDataSyncCompleted",
                Name = success ? "user_data_synced" : "user_data_sync_failed",
                Call = $"Abxr.OnUserDataSyncCompleted({(success ? "true" : "false")}, {FormatValue(errorMessage, quoted: true)})",
                Status = success ? "Synced" : "Sync failed",
                Tone = success ? StatusTone.Good : StatusTone.Warning,
                Automatic = true,
            });
        }

        private void HandleModuleTarget(string target) => Add(new StreamRecord
        {
            Kind = StreamKind.Module,
            Method = "OnModuleTarget",
            Name = target,
            Call = $"Abxr.OnModuleTarget({FormatValue(target, quoted: true)})",
            Status = "From the LMS",
            Tone = StatusTone.Neutral,
            Automatic = true,
        });

        private void HandleAllModulesCompleted() => Add(new StreamRecord
        {
            Kind = StreamKind.Module,
            Method = "OnAllModulesCompleted",
            Name = "all_modules_completed",
            Call = "Abxr.OnAllModulesCompleted()",
            Status = "Sequence finished",
            Tone = StatusTone.Good,
            Automatic = true,
        });

        private void HandleSampleOpened(SampleInfo sample)
        {
            _current = sample;
            _liveTelemetry.Clear();
            if (_list != null) ShowDivider(sample != null ? $"Opened {sample.Label}" : "Opened the sample browser");
        }

        private static StreamKind KindOf(string method)
        {
            if (method.StartsWith("EventAssessment", StringComparison.Ordinal)) return StreamKind.Assessment;
            if (method.StartsWith("EventObjective", StringComparison.Ordinal)) return StreamKind.Objective;
            if (method.StartsWith("EventInteraction", StringComparison.Ordinal)) return StreamKind.Interaction;
            if (method == "EventCritical") return StreamKind.Critical;
            if (method.StartsWith("Telemetry", StringComparison.Ordinal)) return StreamKind.Telemetry;
            if (method.StartsWith("Log", StringComparison.Ordinal)) return StreamKind.Log;
            if (method.StartsWith("Storage", StringComparison.Ordinal)) return StreamKind.Storage;
            return StreamKind.Event;
        }

        /// <summary>
        /// The SDK leaves Method empty on records it made itself. The event wrappers mark theirs with type and verb,
        /// so a session's auto-closed assessment still reads as EventAssessmentComplete.
        /// </summary>
        private static string InferredMethod(AbxrObserver.Record source)
        {
            switch (source.Kind)
            {
                case AbxrObserver.RecordKind.Telemetry: return "Telemetry";
                case AbxrObserver.RecordKind.Storage: return "StorageSetEntry";
                case AbxrObserver.RecordKind.Log: return "Log" + Capitalized(source.Level ?? "info");
            }
            source.Data.TryGetValue("type", out var type);
            source.Data.TryGetValue("verb", out var verb);
            var family = type switch { "assessment" => "Assessment", "objective" => "Objective", "interaction" => "Interaction", _ => null };
            if (family == null) return "Event";
            return verb == "completed" ? $"Event{family}Complete" : $"Event{family}Start";
        }

        /// <summary>Record fields that come from the method's arguments rather than from the SDK.</summary>
        private static HashSet<string> ArgumentKeys(string method) => method switch
        {
            "EventAssessmentComplete" or "EventObjectiveComplete" => new HashSet<string> { "score", "status" },
            "EventInteractionComplete" => new HashSet<string> { "interaction", "result", "response" },
            _ => new HashSet<string>(),
        };

        /// <summary>The call as code, rebuilt from the record. Metadata shows as its argument name; its entries are listed under Your data.</summary>
        private static string FormatCall(StreamRecord record, AbxrObserver.Record source)
        {
            string Value(string key) => source.Data.TryGetValue(key, out var value) ? value : null;
            var name = record.Method == "EventCritical" && source.Name.StartsWith(CriticalPrefix, StringComparison.Ordinal)
                ? source.Name.Substring(CriticalPrefix.Length)
                : source.Name;

            var arguments = new List<string> { FormatValue(name, quoted: true) };
            switch (record.Method)
            {
                case "EventAssessmentComplete":
                case "EventObjectiveComplete":
                    arguments.Add(Value("score"));
                    arguments.Add("Abxr.EventStatus." + Capitalized(Value("status")));
                    break;
                case "EventInteractionComplete":
                    arguments.Add("Abxr.InteractionType." + Capitalized(Value("interaction")));
                    arguments.Add("Abxr.InteractionResult." + Capitalized(Value("result")));
                    if (Value("response") != null) arguments.Add(FormatValue(Value("response"), quoted: true));
                    break;
            }
            if (source.CallerData != null && source.CallerData.Count > 0)
                arguments.Add(record.Kind == StreamKind.Telemetry ? "telemetryData" : record.Kind == StreamKind.Storage ? "entryData" : "meta");

            return $"Abxr.{record.Method}({string.Join(", ", arguments)})";
        }

        private const string CriticalPrefix = "CRITICAL_ABXR_";

        private static string Capitalized(string value) =>
            string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

        private static string DropText(AbxrObserver.DropReason reason) => reason switch
        {
            AbxrObserver.DropReason.NotRecording => "Not recorded: no organization identity",
            AbxrObserver.DropReason.QueueFull => "Dropped: queue full",
            AbxrObserver.DropReason.NotAuthenticated => "Not kept: needs sign-in",
            AbxrObserver.DropReason.NoUser => "Not kept: needs a signed-in user",
            AbxrObserver.DropReason.SessionEnded => "Dropped: the session ended first",
            _ => "Dropped",
        };

        private static string FormatValue(object value, bool quoted)
        {
            switch (value)
            {
                case null: return quoted ? "null" : "";
                case string text: return quoted ? $"\"{text}\"" : text;
                case bool flag: return flag ? "true" : "false";
                case Enum enumValue:
                    var type = enumValue.GetType();
                    var typeName = type.DeclaringType != null ? $"{type.DeclaringType.Name}.{type.Name}" : type.Name;
                    return quoted ? $"{typeName}.{enumValue}" : enumValue.ToString();
                case IFormattable number: return number.ToString(null, CultureInfo.InvariantCulture);
                case Delegate callback: return callback.Method.Name.Contains("<") ? "callback" : callback.Method.Name;
                case IEnumerable<string> items:
                    return quoted ? "{ " + string.Join(", ", items.Select(item => $"\"{item}\"")) + " }" : string.Join(", ", items);
                default: return value.ToString();
            }
        }

        private void Build(RectTransform column)
        {
            var background = UiKit.Panel(column, "Insights Stream", Theme.Console).rectTransform;
            UiKit.Stretch(background);
            var edge = Line(background, Theme.LineStrong);
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = new Vector2(0, 1);
            edge.pivot = new Vector2(0, 0.5f);
            edge.sizeDelta = new Vector2(1, 0);

            BuildHeader(background);
            BuildFilters(background);

            var area = UiKit.Rect(background, "Records");
            UiKit.Stretch(area, top: HeaderHeight + FiltersHeight, bottom: FooterHeight);
            _list = UiKit.ScrollList(area, "Rows", 0f, new RectOffset(0, 0, 4, 12));
            _empty = UiKit.Label(_list, "", 14.5f, Theme.TextFaint);
            _empty.alignment = TextAlignmentOptions.Center;
            _empty.margin = new Vector4(16, 28, 16, 16);

            BuildFooter(background);
            BuildInfo();
            SetFilter(StreamFilter.All);
        }

        private void BuildHeader(RectTransform background)
        {
            var header = Band(background, "Header", 0f, HeaderHeight, 4f, new RectOffset(18, 10, 0, 0));

            _liveHalo = UiKit.Card(header, "Live", Color.clear, 7f);
            _liveHalo.raycastTarget = false;
            var haloSize = _liveHalo.gameObject.AddComponent<LayoutElement>();
            haloSize.minWidth = haloSize.preferredWidth = haloSize.minHeight = haloSize.preferredHeight = 13f;
            _live = UiKit.Card(_liveHalo.transform, "Dot", Theme.Mint, 4f);
            _live.raycastTarget = false;
            _live.rectTransform.sizeDelta = new Vector2(7f, 7f);
            _liveHalo.color = new Color(Theme.Mint.r, Theme.Mint.g, Theme.Mint.b, 0.2f);

            var title = UiKit.Caption(header, "INSIGHTS STREAM", 14.5f, Theme.Text);
            title.characterSpacing = 5f;
            title.margin = new Vector4(6, 0, 4, 0);
            UiKit.Rigid(title);
            UiKit.IconButton(header, "info", () => _info.SetActive(true), 32f, 17f);
            UiKit.Spacer(header);
            _pauseButton = UiKit.IconButton(header, "pause", TogglePause, 32f, 17f);
            UiKit.IconButton(header, "trash-2", Clear, 32f, 17f);
            UiKit.Separator(header, 20f);
            UiKit.IconButton(header, "x", _shell.ToggleStream, 32f, 17f);
        }

        private void BuildFilters(RectTransform background)
        {
            var filters = Band(background, "Filters", -HeaderHeight, FiltersHeight, 8f, new RectOffset(12, 10, 0, 0));

            var segments = UiKit.Card(filters, "Segments", new Color(0f, 0f, 0f, 0.18f), 10f);
            segments.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var layout = segments.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 3, 3);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            foreach (StreamFilter filter in Enum.GetValues(typeof(StreamFilter)))
            {
                var value = filter;
                var image = UiKit.Card(segments.transform, filter.ToString(), Color.clear, 7f);
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SetFilter(value));
                var hover = image.gameObject.AddComponent<Hover>();
                hover.Init(image, Color.clear, Theme.Hover);
                var row = image.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.padding = new RectOffset(10, 10, 0, 0);
                row.spacing = 6f;
                row.childAlignment = TextAnchor.MiddleCenter;
                row.childControlWidth = true;
                row.childControlHeight = true;
                row.childForceExpandWidth = false;
                row.childForceExpandHeight = false;
                var size = image.gameObject.AddComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = 28f;

                if (filter != StreamFilter.All) Dot(image.transform, GroupColor(filter), 7f, 2f);
                var label = UiKit.Rigid(UiKit.Caption(image.transform, filter.ToString(), 14f, Theme.TextFaint));
                var count = UiKit.Caption(image.transform, "0", 12.5f, Theme.TextFaint);
                _filters[filter] = (hover, label, count);
            }
            UiKit.Outline(segments.transform, 10f, Theme.Line);

            _sdkToggle = UiKit.IconButton(filters, "cpu", ToggleSdkRecords, 32f, 17f);
            UiKit.SetToggled(_sdkToggle, _showSdk);
        }

        private void BuildFooter(RectTransform background)
        {
            var footer = UiKit.Row(background, "Footer", 8f);
            footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(16, 10, 0, 0);
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = new Vector2(1, 0);
            footer.pivot = new Vector2(0.5f, 0);
            footer.sizeDelta = new Vector2(0, FooterHeight);
            var rule = Line(footer, Theme.Line);
            rule.anchorMin = new Vector2(0, 1);
            rule.anchorMax = Vector2.one;
            rule.pivot = new Vector2(0.5f, 1);
            rule.sizeDelta = new Vector2(0, 1);

            _footerDot = Dot(footer, Theme.TextFaint, 8f, 4f);
            _footerText = UiKit.Caption(footer, "", 13.5f, Theme.TextFaint);
            _footerText.overflowMode = TextOverflowModes.Ellipsis;
            _footerText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _resume = UiKit.ShellButton(footer, "Resume", "play", TogglePause, ButtonLook.Ghost, 28f, 13.5f).gameObject;
        }

        /// <summary>What the Stream shows and leaves out, behind the header's info button.</summary>
        private void BuildInfo()
        {
            var panel = UiKit.Popover(_shell.PopoverLayer, "Stream Info", () => _info.SetActive(false));
            _info = panel.parent.gameObject;
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-20f, -(HeaderHeight - 4f));
            panel.sizeDelta = new Vector2(SampleShell.StreamWidth - 40f, 0);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 18, 18);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            UiKit.FitHeight(panel);

            UiKit.Label(panel, "Every record the SDK creates, newest first, from its observer API. Click a record to open it.", 15.5f, Theme.Text);
            var key = Theme.Tag(Theme.Key);
            UiKit.Label(panel,
                $"<color={key}>your data</color><indent=10em>What your code passed.</indent>\n" +
                $"<color={key}>added by the SDK</color><indent=10em>What the SDK attached, such as the scene, module, and timings.</indent>\n" +
                $"<color={key}>sdk</color><indent=10em>A record the SDK made on its own. The chip button beside the filters hides them.</indent>",
                14.5f, Theme.TextMuted).lineSpacing = 10f;

            InfoHeading(panel, "Batching");
            _batchNote = UiKit.Label(panel, BatchNote(), 14.5f, Theme.TextMuted);
            InfoHeading(panel, "Not shown");
            UiKit.Label(panel, "Calls that create no record, such as StartTimedEvent or Register.", 14.5f, Theme.TextMuted);

            var legend = UiKit.Row(panel, "Legend", 7f);
            legend.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 6, 0);
            foreach (var (tone, status, label) in new[]
                     {
                         (StatusTone.Neutral, Queued, "queued"), (StatusTone.Good, Sent, "sent"),
                         (StatusTone.Warning, "", "send failed"), (StatusTone.Bad, "", "dropped"),
                     })
            {
                Dot(legend, StatusColor(tone, status), 8f, 4f);
                UiKit.Rigid(UiKit.Caption(legend, label, 13.5f, Theme.TextFaint)).margin = new Vector4(0, 0, 10, 0);
            }
            UiKit.Outline(panel, 14f, Theme.LineStrong);
        }

        private static void InfoHeading(Transform parent, string text)
        {
            var heading = UiKit.Caption(parent, text.ToUpperInvariant(), 12.5f, Theme.TextFaint);
            heading.characterSpacing = 7f;
            heading.margin = new Vector4(0, 4, 0, -6);
        }

        // The batch settings come from the AbxrLib configuration, and your app's configuration in ArborXR can change
        // them at sign-in, so the note is rebuilt then.
        private static string BatchNote()
        {
            var config = AbxrLib.Runtime.Core.Configuration.Instance;
            return $"Queued records go out every {config.sendNextBatchWaitSeconds} s, or as soon as " +
                   $"{config.dataEntriesPerSendAttempt} are waiting ({config.storageEntriesPerSendAttempt} for storage saves).";
        }

        /// <summary>A row across the top of the column, with a rule under it.</summary>
        private static RectTransform Band(RectTransform background, string name, float top, float height, float spacing, RectOffset padding)
        {
            var band = UiKit.Row(background, name, spacing);
            band.GetComponent<HorizontalLayoutGroup>().padding = padding;
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = Vector2.one;
            band.pivot = new Vector2(0.5f, 1);
            band.anchoredPosition = new Vector2(0, top);
            band.sizeDelta = new Vector2(0, height);
            var rule = Line(band, Theme.Line);
            rule.anchorMin = Vector2.zero;
            rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.sizeDelta = new Vector2(0, 1);
            return band;
        }

        /// <summary>A hairline that its parent's layout group leaves alone; the caller anchors it to an edge.</summary>
        private static RectTransform Line(Transform parent, Color color)
        {
            var line = UiKit.Panel(parent, "Line", color);
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.anchoredPosition = Vector2.zero;
            return line.rectTransform;
        }

        private static Image Dot(Transform parent, Color color, float size, float radius)
        {
            var dot = UiKit.Card(parent, "Dot", color, radius);
            dot.raycastTarget = false;
            var element = dot.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = element.minHeight = element.preferredHeight = size;
            return dot;
        }

        private Row CreateRow(StreamRecord record, bool flash)
        {
            var root = UiKit.Column(_list, $"Record {record.Sequence}", 0f);
            var summary = UiKit.Panel(root, "Summary", Color.clear);
            var row = new Row { Record = record, Root = root.gameObject, Summary = summary };
            var button = summary.gameObject.AddComponent<Button>();
            button.targetGraphic = summary;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Toggle(row));
            row.Hover = summary.gameObject.AddComponent<Hover>();
            row.Hover.Init(summary, RestColor(record), RowHover);

            var layout = summary.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 14, 6, 6);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            row.Chevron = UiKit.Icon(summary.transform, "chevron-right", 13f, Theme.TextFaint).rectTransform;
            row.Time = Fixed(UiKit.Label(summary.transform, "", 13.5f, Theme.TextFaint), 62f);
            Fixed(UiKit.Label(summary.transform, Tag(record.Kind), 12.5f, TagColor(record), FontStyles.Bold), 42f);
            row.Call = UiKit.Label(summary.transform, "", 14.5f, Theme.Text);
            row.Call.textWrappingMode = TextWrappingModes.NoWrap;
            row.Call.overflowMode = TextOverflowModes.Ellipsis;
            row.Call.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            row.Count = UiKit.Caption(summary.transform, "", 13.5f, Theme.TextFaint);
            if (record.Automatic) SdkBadge(summary.transform);
            row.StatusDot = Dot(summary.transform, Color.clear, 8f, 4f);
            row.Status = UiKit.Caption(summary.transform, "", 13f, Theme.TextFaint);
            Fill(row);

            root.SetAsFirstSibling();
            _rows.Add(row);
            _rowOf[record] = row;
            root.gameObject.SetActive(Visible(record));
            if (flash) StartCoroutine(Flash(row));
            Trim();
            RefreshCounts();
            return row;
        }

        private static TextMeshProUGUI Fixed(TextMeshProUGUI label, float width)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            return label;
        }

        /// <summary>Marks a record the SDK made on its own rather than from a call in your code.</summary>
        private static void SdkBadge(Transform parent)
        {
            var badge = UiKit.Card(parent, "SDK", Color.clear, 4f);
            badge.raycastTarget = false;
            var layout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 1, 1);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            UiKit.Rigid(UiKit.Caption(badge.transform, "sdk", 12f, Theme.TextFaint));
            UiKit.Outline(badge.transform, 4f, Theme.LineStrong);
        }

        /// <summary>Writes a row's summary line from its record, which changes as telemetry merges into it.</summary>
        private void Fill(Row row)
        {
            var record = row.Record;
            row.Time.text = record.Time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            row.Call.text = Colorize(record.Call);
            row.Count.text = $"×{record.Samples}";
            row.Count.gameObject.SetActive(record.Samples > 1);
            UiKit.Rigid(row.Count);
            ShowStatus(row);
        }

        /// <summary>
        /// The summary line shows a dot for queued and sent, and the start of anything else ("Dropped", "Send failed");
        /// the open row has the whole status.
        /// </summary>
        private static void ShowStatus(Row row)
        {
            var record = row.Record;
            var color = StatusColor(record.Tone, record.Status);
            row.StatusDot.color = color;
            var quiet = record.Status == Queued || record.Status == Sent;
            row.Status.gameObject.SetActive(!quiet);
            if (quiet) return;
            var colon = record.Status.IndexOf(':');
            row.Status.text = colon > 0 ? record.Status.Substring(0, colon) : record.Status;
            row.Status.color = record.Tone == StatusTone.Neutral ? Theme.TextFaint : color;
            UiKit.Rigid(row.Status);
        }

        private void Toggle(Row row)
        {
            row.Open = !row.Open;
            row.Chevron.localEulerAngles = new Vector3(0, 0, row.Open ? -90f : 0f);
            if (row.Open && row.Detail == null) BuildDetail(row);
            else if (row.Detail != null) row.Detail.SetActive(row.Open);
        }

        /// <summary>Rebuilds an open row's detail after its record changed. A closed row builds its detail when opened.</summary>
        private void RefreshDetail(Row row)
        {
            if (row.Detail == null) return;
            row.Detail.SetActive(false);
            Destroy(row.Detail);
            row.Detail = null;
            if (row.Open) BuildDetail(row);
        }

        private void BuildDetail(Row row)
        {
            var record = row.Record;
            var detail = UiKit.Column(row.Root.transform, "Detail", 8f, new RectOffset(34, 14, 2, 12));
            row.Detail = detail.gameObject;

            UiKit.Label(detail, Colorize(record.Call), 14.5f, Theme.Text);
            if (record.CallerData.Count > 0) UiKit.Label(detail, Pairs("your data", record.CallerData), 14f, Theme.Text);
            if (record.SdkData.Count > 0)
                UiKit.Label(detail, Pairs(record.Automatic ? "made by the SDK" : "added by the SDK", record.SdkData), 14f, Theme.Text);
            if (record.Samples > 1)
            {
                var seconds = (record.Time - record.FirstTime).TotalSeconds;
                var rate = seconds > 0 ? (record.Samples - 1) / seconds : 0;
                Note(detail, "merged", $"×{record.Samples} · {rate.ToString("0.0", CultureInfo.InvariantCulture)}/s · showing the latest values", Theme.TextMuted);
            }
            if (!string.IsNullOrEmpty(record.Feeds)) Note(detail, "feeds", record.Feeds, Theme.TextMuted);
            Note(detail, "status", record.Status, record.Tone == StatusTone.Neutral ? Theme.TextMuted : StatusColor(record.Tone, record.Status));

            var bar = UiKit.Row(detail, "Bar", 8f);
            GameObject payload = null;
            UiKit.ShellButton(bar, "payload", "braces", () =>
            {
                row.PayloadOpen = !row.PayloadOpen;
                payload.SetActive(row.PayloadOpen);
            }, ButtonLook.Secondary, 26f, 13f);
            UiKit.Spacer(bar);
            UiKit.Caption(bar, $"seq {record.Sequence}", 13f, Theme.TextFaint);

            var block = UiKit.Card(detail, "Payload", new Color(0f, 0f, 0f, 0.32f), 8f);
            block.raycastTarget = false;
            payload = block.gameObject;
            var layout = block.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 11, 11);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            UiKit.Label(block.transform, PrettyJson(record.ToJson()), 13.5f, Theme.TextMuted).lineSpacing = 8f;
            UiKit.Outline(block.transform, 8f, Theme.Line);
            payload.SetActive(row.PayloadOpen);
        }

        private static void Note(Transform parent, string label, string text, Color color) =>
            UiKit.Label(parent, $"<i>{Theme.Paint($"// {label}", Theme.TextFaint)}</i>   {Plain(text)}", 14f, color);

        /// <summary>A record's data as aligned key and value lines under a comment-style title.</summary>
        private static string Pairs(string title, List<KeyValuePair<string, string>> pairs)
        {
            // Values line up after the longest key, measured in ems as the keys' font isn't monospaced.
            var indent = Mathf.Clamp(pairs.Max(pair => pair.Key.Length) * 0.56f + 1.6f, 5f, 15f);
            var text = new StringBuilder($"<i>{Theme.Paint($"// {title}", Theme.TextFaint)}</i>");
            foreach (var pair in pairs)
            {
                text.Append('\n').Append(Theme.Paint(Plain(pair.Key), Theme.Key));
                var fits = pair.Key.Length * 0.56f + 1.6f <= indent;
                text.Append(fits ? $"<indent={indent.ToString("0.0", CultureInfo.InvariantCulture)}em>" : "   ");
                text.Append(Value(pair.Value));
                if (fits) text.Append("</indent>");
            }
            return text.ToString();
        }

        /// <summary>A value coloured by what it looks like: a number, a keyword, or text in quotes.</summary>
        private static string Value(string value)
        {
            if (value == "true" || value == "false" || value == "null") return Theme.Paint(value, Theme.Keyword);
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return Theme.Paint(Plain(value), Theme.NumberValue);
            return Theme.Paint(Plain($"\"{value}\""), Theme.StringValue);
        }

        /// <summary>Text from a record, kept from being read as TextMesh Pro rich text.</summary>
        private static string Plain(string text) =>
            string.IsNullOrEmpty(text) ? "" : $"<noparse>{text.Replace("</noparse>", "")}</noparse>";

        /// <summary>A call as highlighted code: the method name, then strings, numbers, and keywords in its arguments.</summary>
        private static string Colorize(string call)
        {
            var open = call?.IndexOf('(') ?? -1;
            if (open < 0 || !call.StartsWith("Abxr.", StringComparison.Ordinal)) return Plain(call);

            var text = new StringBuilder(Theme.Paint("Abxr.", Theme.TextFaint));
            text.Append(Theme.Paint(call.Substring(5, open - 5), Theme.Method));
            var arguments = call.Substring(open);
            var at = 0;
            foreach (Match token in CallTokens.Matches(arguments))
            {
                text.Append(Plain(arguments.Substring(at, token.Index - at)));
                var color = token.Value[0] == '"' ? Theme.StringValue : char.IsLetter(token.Value[0]) ? Theme.Keyword : Theme.NumberValue;
                text.Append(Theme.Paint(Plain(token.Value), color));
                at = token.Index + token.Length;
            }
            return text.Append(Plain(arguments.Substring(at))).ToString();
        }

        /// <summary>Compact JSON on indented lines, with its keys, strings, numbers, and keywords coloured.</summary>
        private static string PrettyJson(string json)
        {
            var text = new StringBuilder();
            var depth = 0;
            void NewLine() => text.Append('\n').Append(' ', depth * 4);

            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '"')
                {
                    var end = i + 1;
                    while (end < json.Length && json[end] != '"') end += json[end] == '\\' ? 2 : 1;
                    end = Mathf.Min(end, json.Length - 1);
                    var isKey = end + 1 < json.Length && json[end + 1] == ':';
                    text.Append(Theme.Paint(Plain(json.Substring(i, end - i + 1)), isKey ? Theme.Key : Theme.StringValue));
                    i = end;
                }
                else if (c == '{' || c == '[')
                {
                    text.Append(c);
                    if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) text.Append(json[++i]);
                    else
                    {
                        depth++;
                        NewLine();
                    }
                }
                else if (c == '}' || c == ']')
                {
                    depth--;
                    NewLine();
                    text.Append(c);
                }
                else if (c == ',')
                {
                    text.Append(c);
                    NewLine();
                }
                else if (c == ':') text.Append(": ");
                else
                {
                    var end = i + 1;
                    while (end < json.Length && ",}]".IndexOf(json[end]) < 0) end++;
                    var literal = json.Substring(i, end - i);
                    text.Append(Theme.Paint(literal, char.IsLetter(literal[0]) ? Theme.Keyword : Theme.NumberValue));
                    i = end - 1;
                }
            }
            return text.ToString();
        }

        private void Forget(StreamRecord record)
        {
            if (record == null) return;
            _byRecordId.Remove(record.RecordId);
            _rowOf.Remove(record);
        }

        private void ShowDivider(string text)
        {
            var divider = UiKit.Row(_list, "Divider", 10f, TextAnchor.MiddleCenter);
            divider.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(16, 16, 10, 8);
            DividerLine(divider);
            UiKit.Rigid(UiKit.Caption(divider, text, 13f, Theme.Mint));
            DividerLine(divider);
            divider.SetAsFirstSibling();
            _rows.Add(new Row { Root = divider.gameObject });
            Trim();
        }

        private static void DividerLine(Transform parent)
        {
            var line = UiKit.Panel(parent, "Line", new Color(Theme.Mint.r, Theme.Mint.g, Theme.Mint.b, 0.22f));
            line.raycastTarget = false;
            var element = line.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1;
            element.minHeight = element.preferredHeight = 1f;
        }

        private static string Tag(StreamKind kind) => kind switch
        {
            StreamKind.Event => "EVT",
            StreamKind.Assessment => "ASMT",
            StreamKind.Objective => "OBJ",
            StreamKind.Interaction => "INT",
            StreamKind.Critical => "CRIT",
            StreamKind.Telemetry => "TELE",
            StreamKind.Log => "LOG",
            StreamKind.Storage => "STOR",
            StreamKind.Auth => "AUTH",
            StreamKind.Pairing => "PAIR",
            StreamKind.Identity => "USER",
            StreamKind.Module => "MOD",
            _ => "",
        };

        private static Color GroupColor(StreamFilter group) => group switch
        {
            StreamFilter.Events => Theme.Oklch(0.76f, 0.13f, 285f),
            StreamFilter.Telemetry => Theme.Oklch(0.80f, 0.11f, 210f),
            StreamFilter.Logs => Theme.Oklch(0.80f, 0.03f, 260f),
            StreamFilter.System => Theme.Oklch(0.80f, 0.13f, 166f),
            _ => Theme.TextFaint,
        };

        private static bool Alert(StreamRecord record) => record.Kind == StreamKind.Critical || record.Method == "LogError";

        private static Color TagColor(StreamRecord record) =>
            Alert(record) ? Theme.Red : record.Method == "LogWarn" ? Theme.Amber : GroupColor(record.Group);

        private static Color RestColor(StreamRecord record) => Alert(record) ? RowAlert : Color.clear;

        private static Color StatusColor(StatusTone tone, string status) => tone switch
        {
            StatusTone.Good => Theme.Mint,
            StatusTone.Warning => Theme.Amber,
            StatusTone.Bad => Theme.Red,
            _ => status == Queued ? new Color(1f, 1f, 1f, 0.28f) : Theme.TextFaint,
        };

        private IEnumerator Flash(Row row)
        {
            var rest = RestColor(row.Record);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.9f)
            {
                if (row.Summary == null) yield break;
                if (!row.Hover.IsOver) row.Summary.color = Color.Lerp(RowFlash, rest, t);
                yield return null;
            }
            if (row.Summary != null) row.Hover.Apply();
        }

        private bool Visible(StreamRecord record) =>
            record == null || ((_filter == StreamFilter.All || record.Group == _filter) && (_showSdk || !record.Automatic));

        public void SetFilter(StreamFilter filter)
        {
            _filter = filter;
            foreach (var pair in _filters)
            {
                var active = pair.Key == filter;
                pair.Value.hover.SetColors(active ? Theme.Pressed : Color.clear, active ? Theme.Pressed : Theme.Hover);
                pair.Value.label.color = active ? Theme.Text : Theme.TextFaint;
            }
            ApplyVisibility();
        }

        private void ToggleSdkRecords()
        {
            _showSdk = !_showSdk;
            UiKit.SetToggled(_sdkToggle, _showSdk);
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            foreach (var row in _rows) row.Root.SetActive(Visible(row.Record));
            RefreshCounts();
        }

        /// <summary>Each filter's count of the records on screen, and a line of text when none match.</summary>
        private void RefreshCounts()
        {
            var counts = new int[Enum.GetValues(typeof(StreamFilter)).Length];
            var shown = 0;
            foreach (var row in _rows)
            {
                if (row.Record == null || (!_showSdk && row.Record.Automatic)) continue;
                counts[(int)StreamFilter.All]++;
                counts[(int)row.Record.Group]++;
                if (Visible(row.Record)) shown++;
            }
            foreach (var pair in _filters) pair.Value.count.text = counts[(int)pair.Key].ToString(CultureInfo.InvariantCulture);

            _empty.text = _rows.Any(row => row.Record != null) ? "No records match this filter." : "Waiting for records…";
            _empty.gameObject.SetActive(shown == 0);
            _empty.transform.SetAsLastSibling();
        }

        /// <summary>The footer says how many records wait in the SDK's queue, or that the Stream is paused.</summary>
        private void RefreshFooter()
        {
            _resume.SetActive(_paused);
            if (_paused)
            {
                _footerDot.color = Theme.Amber;
                _footerText.color = Theme.Amber;
                _footerText.text = _held.Count > 0 ? $"Paused · {_held.Count} new" : "Paused";
                return;
            }

            var wait = AbxrLib.Runtime.Core.Configuration.Instance.sendNextBatchWaitSeconds;
            _footerDot.color = _queued.Count > 0 ? StatusColor(StatusTone.Neutral, Queued) : Theme.Mint;
            _footerText.color = Theme.TextFaint;
            _footerText.text = _queued.Count > 0 ? $"{_queued.Count} queued · batches go out every {wait} s" : "Nothing queued";
        }

        private void TogglePause()
        {
            _paused = !_paused;
            UiKit.SetToggled(_pauseButton, _paused, _paused ? "play" : "pause");
            var color = _paused ? Theme.Amber : Theme.Mint;
            _live.color = color;
            _liveHalo.color = new Color(color.r, color.g, color.b, 0.2f);
            if (!_paused)
            {
                foreach (var record in _held) Show(record, flash: true);
                _held.Clear();
            }
            RefreshFooter();
        }

        private void Clear()
        {
            foreach (var row in _rows) Destroy(row.Root);
            _rows.Clear();
            _rowOf.Clear();
            _byRecordId.Clear();
            _liveTelemetry.Clear();
            _held.Clear();
            RefreshCounts();
            RefreshFooter();
        }

        private void Trim()
        {
            while (_rows.Count > MaxRows)
            {
                var oldest = _rows[0];
                // Telemetry of the same name starts a new row once its live row has scrolled off the end.
                foreach (var name in _liveTelemetry.Where(pair => pair.Value == oldest).Select(pair => pair.Key).ToList())
                    _liveTelemetry.Remove(name);
                Forget(oldest.Record);
                Destroy(oldest.Root);
                _rows.RemoveAt(0);
            }
        }
    }
}
