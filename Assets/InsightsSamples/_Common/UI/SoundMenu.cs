using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The menu under the header's speaker button: mute, and a volume slider for the interface sounds.
    /// </summary>
    public sealed class SoundMenu
    {
        private const float Width = 300f;
        private const float Radius = 14f;

        private readonly RectTransform _panel;
        private readonly Button _mute;
        private readonly Slider _slider;
        private readonly TextMeshProUGUI _percent;

        /// <summary>Raised when the menu opens or closes.</summary>
        public event Action Changed;

        public bool IsOpen => _panel.parent.gameObject.activeSelf;

        /// <summary>The speaker icon for the current volume and mute.</summary>
        public static string Icon =>
            UiSounds.Muted || UiSounds.Volume <= 0f ? "volume-x" : UiSounds.Volume < 0.5f ? "volume-1" : "volume-2";

        /// <param name="layer">A rect over the whole screen, above the rest of the shell.</param>
        public SoundMenu(RectTransform layer)
        {
            _panel = UiKit.Popover(layer, "Sound Menu", Close);
            _panel.pivot = new Vector2(1, 1);
            _panel.sizeDelta = new Vector2(Width, 0);
            var layout = _panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 16, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            UiKit.FitHeight(_panel);

            _mute = UiKit.IconButton(_panel, Icon, () => UiSounds.Muted = !UiSounds.Muted, 40f, 19f);
            _slider = UiKit.Slider(_panel, UiSounds.Volume, value =>
            {
                UiSounds.Volume = value;
                // Moving the slider is asking for sound.
                UiSounds.Muted = false;
                UiSounds.Preview();
            });
            _percent = UiKit.Caption(_panel, "100%", 15f, Theme.TextMuted);
            _percent.alignment = TextAlignmentOptions.MidlineRight;
            var percentWidth = _percent.gameObject.AddComponent<LayoutElement>();
            percentWidth.minWidth = percentWidth.preferredWidth = 52f;
            UiKit.Outline(_panel, Radius, Theme.LineStrong);

            UiSounds.Changed += Refresh;
            Refresh();
        }

        /// <summary>Opens the menu under <paramref name="anchor"/>, or closes it.</summary>
        public void Toggle(RectTransform anchor)
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            _panel.parent.gameObject.SetActive(true);
            Refresh();
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            _panel.position = corners[3];
            _panel.anchoredPosition += new Vector2(0, -8f);
            Changed?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _panel.parent.gameObject.SetActive(false);
            UiSounds.Save();
            Changed?.Invoke();
        }

        private void Refresh()
        {
            UiKit.SetToggled(_mute, UiSounds.Muted, Icon);
            _slider.SetValueWithoutNotify(UiSounds.Volume);
            _percent.text = UiSounds.Muted ? "Muted" : $"{Mathf.RoundToInt(UiSounds.Volume * 100f)}%";
        }
    }
}
