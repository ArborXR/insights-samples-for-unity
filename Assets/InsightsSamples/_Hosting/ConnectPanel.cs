using InsightsSamples.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.Hosting
{
    /// <summary>
    /// WebGL builds without an App Token only: asks for the visitor's, shows which app is connected, and lets them
    /// change or forget it. The samples wait behind it until there's a token, and for a visitor who chose to pair,
    /// behind the pairing prompt until it's paired.
    /// </summary>
    public class ConnectPanel : MonoBehaviour
    {
        private RectTransform _modal;
        private TMP_InputField _field;
        private TextMeshProUGUI _error;
        private GameObject _close;
        private GameObject _forget;
        private Button _pair;
        private TextMeshProUGUI _pairingOff;
        private bool _offerPairingAfterSignIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (!HostedAppToken.Enabled) return;
            var go = new GameObject("Connect Panel");
            DontDestroyOnLoad(go);
            go.AddComponent<ConnectPanel>();
        }

        private void Awake()
        {
            BuildUi();
            Abxr.OnAuthCompleted += HandleAuthCompleted;
        }

        private void Start()
        {
            if (HostedAppToken.ChangedAtLaunch) Reconnect();
            var automated = DeepLinks.Get(DeepLinks.Suite) != null || DeepLinks.Get(DeepLinks.Autopilot) != null;
            // Sign-in waits for a token, except in an automated run, which plays signed out.
            if (!HostedAppToken.HasToken && automated) Abxr.StartAuthentication();
            var offered = HostedAppToken.Offered;
            var offer = !string.IsNullOrEmpty(offered) && offered != HostedAppToken.Current;
            var open = (!HostedAppToken.HasToken || offer) && !automated;
            Show(open);
            if (open && offer) _field.text = offered;
        }

        private void OnDestroy() => Abxr.OnAuthCompleted -= HandleAuthCompleted;

        private void Connect(bool pairs)
        {
            var token = _field.text.Trim();
            // An empty field keeps the connected app, to switch how it connects.
            if (string.IsNullOrEmpty(token)) token = HostedAppToken.Current;
            if (string.IsNullOrEmpty(token))
            {
                ShowError("Paste your App Token first.");
                return;
            }
            if (token == HostedAppToken.Current && pairs == HostedAppToken.Pairs)
            {
                Show(false);
                return;
            }
            // The SDK keeps the identity its first sign-in finds for the whole launch. After that, only pairing a
            // different app happens in place; any other change needs a new launch.
            var sameLaunch = Abxr.GetPairingState() == Abxr.PairingState.Resolving || (pairs && HostedAppToken.Pairs);
            if (!HostedAppToken.TryApply(token, pairs))
            {
                ShowError("That isn't an App Token. Copy it from your app's Insights settings in the ArborXR Portal.");
                return;
            }
            if (!sameLaunch && HostedAppToken.Relaunch(token, pairs)) return;
            HostedAppToken.Save(token, pairs);
            Show(false);
            Reconnect();
        }

        private void Forget()
        {
            HostedAppToken.Forget();
            Abxr.ClearPairing();
            Show(true);
        }

        private void Reconnect()
        {
            // A stored pairing belongs to the app it was made with.
            Abxr.ClearPairing();
            _offerPairingAfterSignIn = HostedAppToken.Pairs;
            Abxr.StartAuthentication();
        }

        private void HandleAuthCompleted(bool success, string error)
        {
            if (!_offerPairingAfterSignIn) return;
            _offerPairingAfterSignIn = false;
            // Pairing proves the build with the token that sign-in last loaded, so it opens only after this sign-in.
            // Sample 18 opens it itself when the pairing state changes.
            if (!success && Abxr.GetPairingState() == Abxr.PairingState.Unpaired) Abxr.StartPairing();
        }

        private void ShowError(string message)
        {
            _error.text = message;
            _error.gameObject.SetActive(true);
        }

        private void Show(bool open)
        {
            _modal.gameObject.SetActive(open);
            _error.gameObject.SetActive(false);
            _field.text = "";
            if (open) _field.ActivateInputField();
            // Without a token there's nothing to forget or go back to: the samples wait for one.
            _close.SetActive(HostedAppToken.HasToken);
            _forget.SetActive(HostedAppToken.HasToken);
            UiKit.SetInteractable(_pair, !HostedAppToken.PairingOff);
            _pairingOff.gameObject.SetActive(HostedAppToken.PairingOff);
            ShellSignals.SetPairingHint(!HostedAppToken.Pairs && !HostedAppToken.PairingOff
                ? "To try pairing, open the App button in the header and choose Pair with a passcode."
                : null);

            // The header's connection pill opens this panel, and names the connected app. The shell exists by Start,
            // which makes the first call.
            var appId = HostedAppToken.AppId();
            var label = !HostedAppToken.HasToken ? "Connect your app" :
                appId != null ? $"App …{appId.Substring(Mathf.Max(0, appId.Length - 6))}" : "App connected";
            SampleShell.Instance.SetConnectionPill(label, !HostedAppToken.HasToken, () => Show(true));
        }

        private void BuildUi()
        {
            var canvas = UiKit.CreateScreenCanvas(transform, "Connect Canvas", sortingOrder: 60);
            var root = (RectTransform)canvas.transform;

            // The canvas can't blur the samples behind the panel, so the scrim is nearly opaque instead.
            _modal = UiKit.Panel(root, "Modal", Theme.Oklch(0.10f, 0.02f, 268f, 0.9f)).rectTransform;
            UiKit.Stretch(_modal);

            var card = UiKit.Card(_modal, "Card", Theme.Surface, 18f, shadow: true);
            card.GetComponent<SoftShadow>().color = Theme.Shadow;
            var panel = card.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720, 0);
            var column = card.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(32, 32, 28, 28);
            column.spacing = 18f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            UiKit.FitHeight(panel);

            var head = UiKit.Row(panel, "Head", 12f);
            var title = UiKit.Label(head, "Connect your Insights app", 27f, Theme.Text, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            _close = UiKit.IconButton(head, "x", () => Show(false), 38f, 18f).gameObject;

            UiKit.Label(panel,
                "Paste the App Token of your Insights app. Its records go to the organization that owns the app.",
                18f, Theme.TextMuted);

            var field = UiKit.Column(panel, "Field", 7f);
            UiKit.Caption(field, "App Token", 15f, Theme.TextMuted);
            _field = TokenField(field);
            _error = UiKit.Label(panel, "", 16f, Theme.Red);
            BuildNote(panel);

            var connect = UiKit.Row(panel, "Connect", 10f);
            UiKit.ShellButton(connect, "Send to my organization", null, () => Connect(pairs: false), ButtonLook.Primary);
            _pair = UiKit.ShellButton(connect, "Pair with a passcode", "key-round", () => Connect(pairs: true), ButtonLook.Secondary);
            UiKit.Spacer(connect);
            _forget = UiKit.ShellButton(connect, "Forget this app", null, Forget, ButtonLook.Ghost).gameObject;
            _pairingOff = UiKit.Label(panel,
                "Pairing with a passcode needs MDM-less pairing, which isn't turned on for your organization.",
                16f, Theme.TextFaint);

            UiKit.Outline(panel, 18f, Theme.LineStrong);
        }

        private static void BuildNote(Transform parent)
        {
            var amber = Theme.Amber;
            var note = UiKit.Card(parent, "Build Note", new Color(amber.r, amber.g, amber.b, 0.07f), 10f);
            var layout = note.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(15, 15, 12, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            UiKit.Label(note.transform,
                $"<b>{Theme.Paint("Only for this demo.", amber)}</b> In your own app, the App Token goes in the AbxrLib " +
                $"configuration ({Theme.Paint("Resources/AbxrLib.asset", Theme.Code)}) and ships with the build, so nobody " +
                "types it in.",
                16f, Theme.TextMuted);
            UiKit.Outline(note.transform, 10f, new Color(amber.r, amber.g, amber.b, 0.22f));
        }

        private TMP_InputField TokenField(Transform parent)
        {
            var box = UiKit.Card(parent, "Token Field", Theme.Console, 10f);
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            // Inactive until it's wired up: the input field's OnEnable expects its text component.
            box.gameObject.SetActive(false);

            var area = UiKit.Rect(box.transform, "Text Area");
            UiKit.Stretch(area, left: 15, right: 15, top: 6, bottom: 6);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = FieldText(area, "Paste your App Token here", Theme.TextFaint);
            var text = FieldText(area, "", Theme.Text);
            text.richText = false;
            var outline = UiKit.Outline(box.transform, 10f, Theme.LineStrong);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = box;
            field.transition = Selectable.Transition.None;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = UiKit.Font;
            field.pointSize = 18f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = Theme.Text;
            field.selectionColor = Theme.SelectedHover;
            field.onSelect.AddListener(_ => outline.color = Theme.AccentHover);
            field.onDeselect.AddListener(_ => outline.color = Theme.LineStrong);
            field.onSubmit.AddListener(_ => Connect(HostedAppToken.Pairs && !HostedAppToken.PairingOff));
            box.gameObject.SetActive(true);
            return field;
        }

        private static TextMeshProUGUI FieldText(RectTransform area, string text, Color color)
        {
            var label = UiKit.Label(area, text, 18f, color);
            UiKit.Stretch(label.rectTransform);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
    }
}
