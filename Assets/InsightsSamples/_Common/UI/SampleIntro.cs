using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Each sample's intro, centred over the 3D view: the enterprise scenario it models, what it shows, what to
    /// try, what to watch for in the Insights Stream, and which script to read. The Run sample buttons sit centred
    /// along the bottom, with Show intro beside them while the intro is hidden.
    /// </summary>
    public sealed class SampleIntro
    {
        private const float CardWidth = 900f;
        private const float CardRadius = 20f;
        private const float TermWidth = 200f;
        private const float BarHeight = 46f;
        private const float BarPadding = 7f;
        private const float Margin = 20f;

        // SDK names in the intro's prose are tinted as code: calls, PascalCase members, snake_case record names,
        // and deep link parameters.
        private static readonly Regex CodeWords = new Regex(
            @"\?sample=[\w&=-]+|&\w+=\w+|\b[A-Z]\w*\([^()]*\)" +
            @"|\b(?!ArborXR\b|WebGL\b)(?:Abxr\.)?[A-Z][a-z]+(?:[A-Z][a-z0-9]*)+(?:\.[A-Z]\w*)*\*?" +
            @"|\b[a-z]+(?:_[a-z0-9]+)+(?:\.[a-z]+)?\b");

        private readonly RectTransform _view;
        private readonly RectTransform _card;
        private readonly RectTransform _content;
        private readonly RectTransform _bar;
        private readonly GameObject _showIntro;
        private readonly GameObject _showIntroSeparator;
        private bool _hasSample;
        private bool _hidden;
        private bool _dismissed;

        /// <summary>Raised when the intro is shown or hidden.</summary>
        public event Action Changed;

        public bool Showing => _card.gameObject.activeSelf;

        /// <param name="view">The 3D view's area: the screen below the header, left of the Stream.</param>
        public SampleIntro(RectTransform view, Action<SampleOutcome> runAutopilot)
        {
            _view = view;

            var panel = UiKit.Card(view, "Intro", Theme.Glass, CardRadius, shadow: true);
            panel.GetComponent<SoftShadow>().color = Theme.Shadow;
            _card = panel.rectTransform;
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.anchoredPosition = new Vector2(0, (BarHeight + 2 * BarPadding + Margin) / 2);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 26, 28);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            UiKit.FitHeight(_card);
            _content = UiKit.Column(_card, "Content", 0f);

            var close = (RectTransform)UiKit.IconButton(_card, "x", Dismiss, 36f, 18f).transform;
            close.GetComponent<LayoutElement>().ignoreLayout = true;
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
            close.anchoredPosition = new Vector2(-14f, -14f);
            close.sizeDelta = new Vector2(36f, 36f);
            UiKit.Outline(_card, CardRadius, Theme.LineStrong);

            var bar = UiKit.Card(view, "Run Bar", Theme.Glass, 17f, shadow: true);
            bar.GetComponent<SoftShadow>().color = Theme.Shadow;
            _bar = bar.rectTransform;
            _bar.anchorMin = _bar.anchorMax = new Vector2(0.5f, 0);
            _bar.pivot = new Vector2(0.5f, 0);
            _bar.anchoredPosition = new Vector2(0, Margin);
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset((int)BarPadding, (int)BarPadding, (int)BarPadding, (int)BarPadding);
            row.spacing = BarPadding;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            var fitter = bar.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiKit.ShellButton(_bar, "Run sample: pass", "play", () => runAutopilot(SampleOutcome.Pass), ButtonLook.Primary, BarHeight);
            UiKit.ShellButton(_bar, "Run sample: fail", null, () => runAutopilot(SampleOutcome.Fail), ButtonLook.Secondary, BarHeight);
            _showIntroSeparator = UiKit.Separator(_bar, 22f).gameObject;
            _showIntro = UiKit.ShellButton(_bar, "Show intro", "book-open", Reopen, ButtonLook.Ghost, BarHeight).gameObject;
            UiKit.Outline(_bar, 17f, Theme.LineStrong);
        }

        /// <summary>Opens a sample with its intro showing; null (the sample browser) hides it all.</summary>
        public void Show(SampleInfo sample)
        {
            _hasSample = sample != null;
            _dismissed = false;
            if (_hasSample) Compose(sample);
            Apply();
        }

        /// <summary>Steps aside for an SDK prompt, which opens in the middle of the view. The Run sample buttons stay.</summary>
        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            Apply();
        }

        /// <summary>Hides the intro, leaving Show intro in the bar, e.g. when the autopilot starts on the station.</summary>
        public void Dismiss()
        {
            _dismissed = true;
            Apply();
        }

        /// <summary>Shows the intro if it's hidden, and hides it if it's showing.</summary>
        public void Toggle()
        {
            _dismissed = !_dismissed;
            Apply();
        }

        /// <summary>Keeps the card inside the view as it changes, e.g. when the Stream is hidden.</summary>
        public void FitToView()
        {
            var width = Mathf.Min(CardWidth, _view.rect.width - 2 * Margin);
            _card.sizeDelta = new Vector2(Mathf.Max(width, 280f), _card.sizeDelta.y);
        }

        private void Reopen()
        {
            _dismissed = false;
            Apply();
        }

        private void Apply()
        {
            FitToView();
            _card.gameObject.SetActive(_hasSample && !_hidden && !_dismissed);
            _bar.gameObject.SetActive(_hasSample);
            _showIntro.SetActive(_dismissed && !_hidden);
            _showIntroSeparator.SetActive(_dismissed && !_hidden);
            Changed?.Invoke();
        }

        private void Compose(SampleInfo sample)
        {
            foreach (Transform old in _content)
            {
                // Destroy waits for the end of the frame; an inactive child is out of the layout straight away.
                old.gameObject.SetActive(false);
                Object.Destroy(old.gameObject);
            }
            var style = SampleCatalog.Instance.StyleFor(sample.series);

            // The head leaves room on its right for the close button.
            var head = UiKit.Column(_content, "Head", 8f, new RectOffset(0, 40, 0, 0));
            var series = UiKit.Row(head, "Series", 8f);
            UiKit.Icon(series, style.icon, 15f, style.Tint(0.82f, 0.12f));
            UiKit.Caption(series, sample.series.ToUpperInvariant(), 13.5f, style.Tint(0.82f, 0.12f)).characterSpacing = 6f;
            UiKit.Label(head, $"{Theme.Paint(sample.number.ToString("00"), style.Tint(0.8f, 0.12f))}  {sample.title}", 27f, Theme.Text, FontStyles.Bold);

            var rule = UiKit.Column(_content, "Rule", 0f, new RectOffset(0, 0, 20, 20));
            var line = UiKit.Panel(rule, "Line", Theme.Line);
            line.raycastTarget = false;
            line.gameObject.AddComponent<LayoutElement>().preferredHeight = 1f;

            var rows = UiKit.Column(_content, "Rows", 15f);
            Row(rows, style, "The scenario", sample.realWorld);
            Row(rows, style, "What this shows", Highlight(sample.whatThisShows));
            Row(rows, style, "Try this", Steps(sample.tryThis));
            Row(rows, style, "Watch the stream for", Steps(sample.watchFor));
            var script = Theme.Paint(sample.lessonScript, Theme.Text);
            Row(rows, style, "Read the code", sample.lessonScriptLine > 0 ? $"{script} from line {sample.lessonScriptLine}" : script);
        }

        /// <summary>One line of the intro: a small heading in the series' colour beside its text.</summary>
        private static void Row(Transform parent, SeriesStyle style, string term, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var row = UiKit.Row(parent, term, 26f, TextAnchor.UpperLeft);

            var heading = UiKit.Caption(row, term.ToUpperInvariant(), 13f, style.Tint(0.8f, 0.1f));
            heading.characterSpacing = 6f;
            heading.alignment = TextAlignmentOptions.TopLeft;
            heading.textWrappingMode = TextWrappingModes.Normal;
            heading.margin = new Vector4(0, 4, 0, 0);
            var width = heading.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = TermWidth;

            var body = UiKit.Label(row, text, 17f, Theme.TextMuted);
            body.lineSpacing = 14f;
            body.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        }

        private static string Steps(string[] lines)
        {
            if (lines == null || lines.Length == 0) return null;
            return string.Join("\n", lines.Select((line, index) =>
                $"{Theme.Paint($"{index + 1}.", Theme.TextFaint)}<indent=1.5em>{Highlight(line)}</indent>"));
        }

        private static string Highlight(string text) =>
            string.IsNullOrEmpty(text) ? text : CodeWords.Replace(text, match => Theme.Paint(match.Value, Theme.Code));
    }
}
