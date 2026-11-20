using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The home screen: a shelf of cards for each series. A card opens its sample.
    /// </summary>
    public class SampleBrowser : MonoBehaviour
    {
        // Behind an SDK prompt, which opens 1.5 m in front of the camera (authUIDistanceFromCamera).
        private const float PlaneDistance = 5f;
        private const int Gutter = 48;
        private const float CardWidth = 360f;
        private const float CardHeight = 378f;
        private const float CardRadius = 16f;
        // Room under the cards for the shadow a card casts while the pointer is over it.
        private const int ShadowRoom = 30;

        private readonly List<(ScrollRect scroll, GameObject previous, GameObject next)> _shelves =
            new List<(ScrollRect, GameObject, GameObject)>();
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private Camera _camera;
        private RectTransform _area;

        private void Awake()
        {
            _canvas = UiKit.CreateScreenCanvas(transform, "Browser Canvas", sortingOrder: 10);
            _scaler = _canvas.GetComponent<CanvasScaler>();
            _camera = Camera.main;
            if (_camera != null)
            {
                // Drawn through the camera, behind the SDK's world-space prompt, so the browser stays usable while a
                // prompt is open. An overlay would cover the prompt, so it would have to hide.
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _camera;
                _canvas.planeDistance = PlaneDistance;
                _canvas.sortingOrder = -10;
            }
            var area = _area = UiKit.Rect(_canvas.transform, "Area");
            FitToStream();
            Backdrop(area);

            var list = UiKit.ScrollList(area, "Samples", 0f, new RectOffset(0, 0, 40, 56 - ShadowRoom));
            var head = UiKit.Column(list, "Head", 10f, new RectOffset(Gutter, Gutter, 0, 0));
            head.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Label(head, "Insights Samples", 36f, Theme.Text, FontStyles.Bold);
            var lead = UiKit.Label(head, "Each sample teaches one ArborXR Insights feature in a short enterprise training scenario. " +
                                         "Open one and watch the Insights Stream to see exactly what each call sends. On a device " +
                                         "ArborXR doesn't manage, the pairing prompt at launch is sample 18's code.", 18f, Theme.TextMuted);
            lead.lineSpacing = 12f;
            lead.gameObject.AddComponent<LayoutElement>().preferredWidth = 980f;

            foreach (var series in Samples().GroupBy(sample => sample.series)) Shelf(list, series.Key, series.ToList());

            ShellSignals.Changed += ApplyPromptVisibility;
            SampleShell.LayoutChanged += FitToStream;
            ApplyPromptVisibility();
        }

        private void OnDestroy()
        {
            ShellSignals.Changed -= ApplyPromptVisibility;
            SampleShell.LayoutChanged -= FitToStream;
        }

        private void LateUpdate()
        {
            // The camera's viewport is the view left of the Stream and below the header (SampleShell), so scale to
            // that part of the 1920x1080 reference, as the shell's overlay does.
            if (_camera != null)
                _scaler.referenceResolution = new Vector2(1920f * _camera.rect.width, 1080f * _camera.rect.height);

            // A shelf gets its arrows only while its cards don't all fit.
            foreach (var (scroll, previous, next) in _shelves)
            {
                var overflows = scroll.content.rect.width > scroll.viewport.rect.width + 1f;
                if (previous.activeSelf == overflows) continue;
                previous.SetActive(overflows);
                next.SetActive(overflows);
            }
        }

        private void FitToStream()
        {
            if (_camera != null)
            {
                UiKit.Stretch(_area);
                return;
            }
            var shell = SampleShell.Instance;
            UiKit.Stretch(_area, right: shell != null ? shell.StreamSpace : SampleShell.StreamWidth, top: SampleShell.HeaderHeight);
        }

        private void ApplyPromptVisibility() => _canvas.enabled = _camera != null || !ShellSignals.SdkPromptOpen;

        /// <summary>The page's background: the theme's dark blue, lit from two corners in the brand colours.</summary>
        private static void Backdrop(Transform area)
        {
            var background = UiKit.Panel(area, "Background", Theme.Background);
            background.raycastTarget = false;
            UiKit.Stretch(background.rectTransform);

            var accent = UiKit.Glow(background.transform, "Accent Glow", Theme.Oklch(0.56f, 0.21f, Theme.BrandHue, 0.28f)).rectTransform;
            accent.anchorMin = new Vector2(-0.45f, 0.5f);
            accent.anchorMax = new Vector2(0.75f, 1.5f);
            accent.offsetMin = accent.offsetMax = Vector2.zero;

            var mint = UiKit.Glow(background.transform, "Mint Glow", Theme.Oklch(0.82f, 0.15f, 166f, 0.1f)).rectTransform;
            mint.anchorMin = new Vector2(0.6f, -0.4f);
            mint.anchorMax = new Vector2(1.4f, 0.4f);
            mint.offsetMin = mint.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<RectMask2D>();
        }

        private void Shelf(Transform list, string series, List<SampleInfo> samples)
        {
            var style = SampleCatalog.Instance.StyleFor(series);
            var shelf = UiKit.Column(list, series, 12f, new RectOffset(0, 0, 34, 0));

            var head = UiKit.Row(shelf, "Head", 12f);
            head.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Gutter, Gutter, 0, 0);
            var badge = UiKit.Card(head, "Icon", style.Tint(0.72f, 0.14f, 0.16f), 10f);
            badge.raycastTarget = false;
            var badgeSize = badge.gameObject.AddComponent<LayoutElement>();
            badgeSize.minWidth = badgeSize.preferredWidth = badgeSize.minHeight = badgeSize.preferredHeight = 36f;
            var icon = UiKit.Icon(badge.transform, style.icon, 19f, style.Tint(0.85f, 0.12f)).rectTransform;
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
            UiKit.Rigid(UiKit.Caption(head, series, 21f, Theme.Text));
            UiKit.Rigid(UiKit.Caption(head, samples.Count == 1 ? "1 sample" : $"{samples.Count} samples", 16f, Theme.TextFaint));
            if (!string.IsNullOrEmpty(style.note))
            {
                UiKit.Separator(head, 16f);
                UiKit.Rigid(UiKit.Caption(head, style.note, 16f, Theme.TextMuted));
            }
            UiKit.Spacer(head);

            var track = UiKit.Panel(shelf, "Track", Color.clear);
            var trackSize = track.gameObject.AddComponent<LayoutElement>();
            trackSize.minHeight = trackSize.preferredHeight = 6f + CardHeight + ShadowRoom;
            track.gameObject.AddComponent<RectMask2D>();
            var scroll = track.gameObject.AddComponent<ShelfScroll>();
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var cards = UiKit.Row(track.transform, "Cards", 18f, TextAnchor.UpperLeft);
            cards.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(Gutter, Gutter, 6, ShadowRoom);
            cards.anchorMin = new Vector2(0, 0);
            cards.anchorMax = new Vector2(0, 1);
            cards.pivot = new Vector2(0, 0.5f);
            cards.offsetMin = cards.offsetMax = Vector2.zero;
            cards.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = track.rectTransform;
            scroll.content = cards;
            foreach (var sample in samples) Card(cards, sample, style);

            var previous = UiKit.IconButton(head, "chevron-left", () => StartCoroutine(Slide(scroll, -1)), 34f, 19f);
            var next = UiKit.IconButton(head, "chevron-right", () => StartCoroutine(Slide(scroll, +1)), 34f, 19f);
            previous.gameObject.SetActive(false);
            next.gameObject.SetActive(false);
            _shelves.Add((scroll, previous.gameObject, next.gameObject));
        }

        /// <summary>Moves a shelf most of a screenful to one side.</summary>
        private static IEnumerator Slide(ScrollRect scroll, int direction)
        {
            var hidden = scroll.content.rect.width - scroll.viewport.rect.width;
            if (hidden <= 0f) yield break;
            var from = scroll.horizontalNormalizedPosition;
            var to = Mathf.Clamp01(from + direction * 0.8f * scroll.viewport.rect.width / hidden);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                scroll.horizontalNormalizedPosition = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            scroll.horizontalNormalizedPosition = to;
        }

        private static void Card(Transform parent, SampleInfo sample, SeriesStyle style)
        {
            var card = UiKit.Panel(parent, $"{sample.number:00} {sample.title}", Color.clear);
            var size = card.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = CardWidth;
            size.minHeight = size.preferredHeight = CardHeight;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.None;
            var id = sample.id;
            button.onClick.AddListener(() => SampleShell.Instance.Open(id));

            // Everything the card shows rises together while the pointer is over it; the card itself stays put for the layout.
            var face = UiKit.Rect(card.transform, "Face");
            UiKit.Stretch(face);
            var surface = UiKit.Card(face, "Surface", Theme.Surface, CardRadius, shadow: true);
            surface.raycastTarget = false;
            UiKit.Stretch(surface.rectTransform);
            var shadow = surface.GetComponent<SoftShadow>();
            shadow.color = style.Tint(0.6f, 0.16f, 0.28f);
            shadow.offset = new Vector2(0, -14);
            shadow.enabled = false;

            var cover = SampleCover.Build(face, sample, style, CardWidth, CardRadius);
            cover.anchorMin = cover.anchorMax = cover.pivot = new Vector2(0.5f, 1f);
            cover.anchoredPosition = Vector2.zero;

            var body = UiKit.Column(face, "Body", 5f, new RectOffset(18, 18, 15, 16));
            UiKit.Stretch(body, top: CardWidth / SampleCover.Aspect);
            var textWidth = CardWidth - 36f;
            var title = UiKit.Caption(body, $"{Theme.Paint($"<b>{sample.number:00}</b>", style.Tint(0.8f, 0.12f))}  {sample.title}", 19f, Theme.Text);
            title.overflowMode = TextOverflowModes.Ellipsis;
            UiKit.ClampLines(UiKit.Label(body, sample.scenario, 16.5f, Theme.TextMuted), textWidth, 2);
            UiKit.ClampLines(UiKit.Label(body, sample.teaches.Replace(", ", "  ·  "), 14f, style.Tint(0.78f, 0.06f)), textWidth, 2);

            var outline = UiKit.Outline(face, CardRadius, Theme.Line);

            var open = UiKit.Row(face, "Open", 5f, TextAnchor.MiddleCenter);
            open.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(13, 10, 6, 6);
            var pill = open.gameObject.AddComponent<Image>();
            pill.color = Theme.Accent;
            pill.raycastTarget = false;
            UiKit.Round(pill, 15f);
            var fit = open.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            open.anchorMin = open.anchorMax = open.pivot = new Vector2(1f, 1f);
            open.anchoredPosition = new Vector2(-12f, -12f);
            UiKit.Caption(open, "Open", 15f, Color.white);
            UiKit.Icon(open, "arrow-right", 15f, Color.white);
            open.gameObject.SetActive(false);

            var hover = card.gameObject.AddComponent<Hover>();
            hover.Changed += over =>
            {
                face.anchoredPosition = over ? new Vector2(0, 4) : Vector2.zero;
                outline.color = over ? style.Tint(0.72f, 0.14f, 0.5f) : Theme.Line;
                shadow.enabled = over;
                open.gameObject.SetActive(over);
            };
        }

        private static IEnumerable<SampleInfo> Samples() =>
            SampleCatalog.Instance.samples.Where(sample => sample != null).OrderBy(sample => sample.number);
    }
}
