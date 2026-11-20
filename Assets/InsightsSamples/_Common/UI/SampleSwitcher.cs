using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The menu under the header's sample name: every sample by series, to go straight to another one.
    /// </summary>
    public sealed class SampleSwitcher
    {
        private const float Width = 460f;
        private const float Radius = 14f;

        private readonly RectTransform _layer;
        private readonly RectTransform _panel;
        private readonly RectTransform _content;
        private readonly List<(SampleInfo sample, Hover hover, Color selected)> _items = new List<(SampleInfo, Hover, Color)>();

        /// <summary>Raised when the menu opens or closes.</summary>
        public event Action Changed;

        public bool IsOpen => _panel.parent.gameObject.activeSelf;

        /// <param name="layer">A rect over the whole screen, above the rest of the shell.</param>
        public SampleSwitcher(RectTransform layer, Action<string> open)
        {
            _layer = layer;
            _panel = UiKit.Popover(layer, "Sample Switcher", Close);
            _panel.pivot = new Vector2(0, 1);
            _content = UiKit.ScrollList(_panel, "Samples", 0f, new RectOffset(7, 7, 7, 7));

            var samples = SampleCatalog.Instance.samples.Where(sample => sample != null).OrderBy(sample => sample.number);
            foreach (var series in samples.GroupBy(sample => sample.series))
            {
                var style = SampleCatalog.Instance.StyleFor(series.Key);
                var heading = UiKit.Caption(_content, series.Key.ToUpperInvariant(), 13f, style.Tint(0.8f, 0.11f));
                heading.characterSpacing = 6f;
                heading.margin = new Vector4(12, 12, 12, 5);
                foreach (var sample in series) Item(sample, style, open);
            }
            UiKit.Outline(_panel, Radius, Theme.LineStrong);
        }

        /// <summary>Opens the menu under <paramref name="anchor"/>, with the current sample marked.</summary>
        public void Show(RectTransform anchor, SampleInfo current)
        {
            foreach (var (sample, hover, selected) in _items)
                hover.SetColors(sample == current ? selected : Color.clear, sample == current ? selected : Theme.Hover);

            _panel.parent.gameObject.SetActive(true);
            _panel.sizeDelta = new Vector2(Width, _panel.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _panel.sizeDelta = new Vector2(Width, Mathf.Min(_content.rect.height, _layer.rect.height * 0.78f));

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            _panel.position = corners[0];
            _panel.anchoredPosition += new Vector2(0, -8f);
            Changed?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _panel.parent.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        private void Item(SampleInfo sample, SeriesStyle style, Action<string> open)
        {
            var image = UiKit.Card(_content, sample.Label, Color.clear, 9f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var id = sample.id;
            button.onClick.AddListener(() =>
            {
                Close();
                open(id);
            });
            var hover = image.gameObject.AddComponent<Hover>();
            hover.Init(image, Color.clear, Theme.Hover);
            _items.Add((sample, hover, style.Tint(0.72f, 0.14f, 0.16f)));

            var row = image.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 8, 8);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.UpperLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            var number = UiKit.Label(image.transform, sample.number.ToString("00"), 15f, style.Tint(0.8f, 0.12f), FontStyles.Bold);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.margin = new Vector4(0, 2, 0, 0);
            var numberWidth = number.gameObject.AddComponent<LayoutElement>();
            numberWidth.minWidth = numberWidth.preferredWidth = 24f;

            var text = UiKit.Column(image.transform, "Text", 2f);
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            UiKit.Caption(text, sample.title, 17f, Theme.Text).overflowMode = TextOverflowModes.Ellipsis;
            var scenario = UiKit.Label(text, sample.scenario, 15f, Theme.TextFaint);
            scenario.textWrappingMode = TextWrappingModes.NoWrap;
            scenario.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
