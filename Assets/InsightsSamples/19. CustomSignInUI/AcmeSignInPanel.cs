using System;
using System.Collections;
using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InsightsSamples.CustomSignInUI
{
    /// <summary>
    /// Acme's own sign-in panel, standing in for the SDK's: a prompt, an entry field with an error line, a keypad
    /// for PINs and passcodes, and the keyboard for logins and names. It never talks to the SDK; the lesson script
    /// tells it what to ask for and hears the answers.
    /// </summary>
    public class AcmeSignInPanel : MonoBehaviour
    {
        public enum Entry
        {
            None,
            Pin,
            Passcode,
            Text,
            Email,
            DeviceName,
            Join,
        }

        private const int CodeLength = 6;
        private static readonly Color Brand = UiKit.FromHex(0x1F6F8B);

        [SerializeField] private Material material;

        private readonly Dictionary<char, StationInteractable> _keys = new Dictionary<char, StationInteractable>();
        private readonly List<GameObject> _keypad = new List<GameObject>();
        private TextMeshPro _prompt;
        private TextMeshPro _field;
        private TextMeshPro _error;
        private TextMeshPro _hint;
        private GameObject _fieldBox;
        private Keyboard _keyboard;
        private TouchScreenKeyboard _touchKeyboard;
        private string _value = "";
        private string _suffix;
        private Action _askAgain;
        private bool _running;
        private int _askedFrame = -1;

        public event Action PairRequested;
        public event Action SignInRequested;
        public event Action<string> Submitted;
        public event Action SkipRequested;
        public event Action NotNowRequested;
        public event Action ScanRequested;
        public event Action<bool> JoinAnswered;

        public Entry Asking { get; private set; }
        public string ShownError { get; private set; }
        public StationInteractable PairButton { get; private set; }
        public StationInteractable SignInButton { get; private set; }
        public StationInteractable SubmitButton { get; private set; }
        public StationInteractable SkipButton { get; private set; }
        public StationInteractable NotNowButton { get; private set; }
        public StationInteractable ScanButton { get; private set; }
        public StationInteractable JoinButton { get; private set; }
        public StationInteractable OtherNameButton { get; private set; }
        public StationInteractable DeleteKey { get; private set; }

        public StationInteractable Key(char digit) => _keys[digit];

        private void Awake()
        {
            BuildStand(StationBuilder.Group(transform, "Stand", new Vector3(-0.2f, 0, 0.8f)));
            BuildPanel(StationBuilder.Group(transform, "Panel", new Vector3(-0.2f, 1.45f, 0.75f)));
            ShowIdle("Waiting for the SDK to ask for input.", canPair: false, canSignIn: false);
        }

        private void Update()
        {
            UpdateTouchKeyboard();
            var keyboard = Keyboard.current;
            if (keyboard != _keyboard)
            {
                if (_keyboard != null) _keyboard.onTextInput -= HandleTextInput;
                _keyboard = keyboard;
                if (_keyboard != null) _keyboard.onTextInput += HandleTextInput;
            }
            if (_keyboard == null || Asking == Entry.None || Asking == Entry.Join) return;
            if (_keyboard.backspaceKey.wasPressedThisFrame) Delete();
            if (_keyboard.enterKey.wasPressedThisFrame || _keyboard.numpadEnterKey.wasPressedThisFrame) Submit();
        }

        // Phones and tablets have no hardware keyboard: logins, emails, and names come from the on-screen one.
        private void UpdateTouchKeyboard()
        {
            if (_touchKeyboard == null) return;
            if (Asking is Entry.None or Entry.Join or Entry.Pin or Entry.Passcode)
            {
                _touchKeyboard.active = false;
                _touchKeyboard = null;
                return;
            }
            var status = _touchKeyboard.status;
            if (status == TouchScreenKeyboard.Status.Canceled)
            {
                // Keep what was typed; tapping the field opens the keyboard again.
                _touchKeyboard = null;
                return;
            }
            if (_touchKeyboard.text != _value)
            {
                _value = "";
                Type(_touchKeyboard.text);
            }
            if (status == TouchScreenKeyboard.Status.Visible) return;
            _touchKeyboard = null;
            if (status == TouchScreenKeyboard.Status.Done) Submit();
        }

        private void OpenTouchKeyboard()
        {
            if (Asking is not (Entry.Text or Entry.Email or Entry.DeviceName) || !TouchScreenKeyboard.isSupported) return;
            if (_touchKeyboard != null && _touchKeyboard.active) return;
            var type = Asking == Entry.Email ? TouchScreenKeyboardType.EmailAddress : TouchScreenKeyboardType.Default;
            _touchKeyboard = TouchScreenKeyboard.Open(_value, type, autocorrection: false);
        }

        private void OnDisable()
        {
            if (_touchKeyboard != null) _touchKeyboard.active = false;
            _touchKeyboard = null;
            if (_keyboard != null) _keyboard.onTextInput -= HandleTextInput;
            _keyboard = null;
        }

        /// <summary>Asks for one value. A retry is another Ask with the error to show.</summary>
        public void Ask(Entry entry, string prompt, string error, string suffix = null, bool canSkip = false, bool canDismiss = false, bool canScan = false)
        {
            _askAgain = () => Ask(entry, prompt, error, suffix, canSkip, canDismiss, canScan);
            Asking = entry;
            _value = "";
            _suffix = suffix;
            _running = true;
            _askedFrame = Time.frameCount;
            ShownError = string.IsNullOrEmpty(error) ? null : error;

            _prompt.text = $"<b>{prompt}</b>";
            _error.text = ShownError ?? "";
            var code = entry == Entry.Pin || entry == Entry.Passcode;
            var onScreen = !code && TouchScreenKeyboard.isSupported;
            _hint.text = code ? "Use the keypad or your keyboard."
                : onScreen ? "Tap the field to type, then press Submit." : "Type on your keyboard, then press Submit or Enter.";
            SetField(true);
            OpenTouchKeyboard();
            SetKeypad(code);
            ShowButtons(SubmitButton, canSkip ? SkipButton : null, canDismiss ? NotNowButton : null, canScan ? ScanButton : null);

            // The SDK asked again because an answer was wrong: that run didn't pass.
            if (ShownError != null) Report(SampleOutcome.Fail);
        }

        /// <summary>A paired device already has the name; join it, or pick another.</summary>
        public void AskToJoin(string question, string existingName, bool canDismiss)
        {
            _askAgain = () => AskToJoin(question, existingName, canDismiss);
            Asking = Entry.Join;
            _running = true;
            _askedFrame = Time.frameCount;
            ShownError = null;

            _prompt.text = $"<b>{question}</b>";
            _error.text = "";
            _hint.text = "";
            SetField(false);
            SetKeypad(false);
            StationBuilder.SetLabel(JoinButton, $"Join {existingName}");
            ShowButtons(JoinButton, OtherNameButton, canDismiss ? NotNowButton : null);
        }

        /// <summary>Shows the last request again, for example after a cancelled badge scan.</summary>
        public void AskAgain() => _askAgain?.Invoke();

        public void ShowIdle(string message, bool canPair, bool canSignIn)
        {
            Asking = Entry.None;
            ShownError = null;
            _prompt.text = message;
            _error.text = "";
            _hint.text = "";
            SetField(false);
            SetKeypad(false);
            ShowButtons(canPair ? PairButton : null, canSignIn ? SignInButton : null);
        }

        public void ShowSignedIn(string detail, bool canSignIn)
        {
            ShowIdle($"<color=#05DA98><b>Signed in.</b></color> {detail}", canPair: false, canSignIn);
            Report(SampleOutcome.Pass);
        }

        public void ShowDismissed(bool canPair)
        {
            ShowIdle("<color=#F5B841><b>Not paired.</b></color> You closed the pairing prompt.", canPair, canSignIn: false);
            Report(SampleOutcome.Fail);
        }

        /// <summary>
        /// A failed sign-in. When the SDK asks again in the same frame (a wrong PIN), that's a retry, not the end.
        /// </summary>
        public void ShowFailed(string message, bool canPair, bool canSignIn)
        {
            var failedFrame = Time.frameCount;
            ShowIdle($"<color=#F5B841><b>Not signed in.</b></color>\n<size=80%>{message}</size>", canPair, canSignIn);
            if (isActiveAndEnabled) StartCoroutine(ReportUnlessAskedAgain(failedFrame));
        }

        /// <summary>A trigger that didn't reach the SDK's prompt, such as pairing in a build that can't pair.</summary>
        public void ShowRefused(string message, bool canPair, bool canSignIn)
        {
            ShowIdle($"<color=#F5B841><b>{message}</b></color>", canPair, canSignIn);
            Report(SampleOutcome.Fail);
        }

        /// <summary>Typing, from the keypad, the keyboard, or the autopilot.</summary>
        public void Type(string text)
        {
            if (Asking == Entry.None || Asking == Entry.Join) return;
            foreach (var c in text)
            {
                var code = Asking == Entry.Pin || Asking == Entry.Passcode;
                if (code && (!char.IsDigit(c) || _value.Length >= CodeLength)) continue;
                if (!code && (char.IsControl(c) || _value.Length >= 64)) continue;
                _value += c;
            }
            RefreshField();
        }

        private void HandleTextInput(char c) => Type(c.ToString());

        private void Delete()
        {
            if (_value.Length == 0) return;
            _value = _value.Substring(0, _value.Length - 1);
            RefreshField();
        }

        private void Submit()
        {
            if (Asking == Entry.None || Asking == Entry.Join) return;
            var value = _value;
            ShowWorking();
            Submitted?.Invoke(value);
        }

        /// <summary>The learner asked the SDK to sign in: how that ends is this run's result.</summary>
        private void Begin(Action raise)
        {
            _running = true;
            raise?.Invoke();
        }

        private void Answer(Action raise)
        {
            ShowWorking();
            raise();
        }

        // Shown before the answer goes out: the SDK may ask again before the call returns.
        private void ShowWorking()
        {
            Asking = Entry.None;
            _prompt.text = "Checking…";
            _error.text = "";
            _hint.text = "";
            SetField(false);
            SetKeypad(false);
            ShowButtons();
        }

        private void Report(SampleOutcome outcome)
        {
            if (!_running) return;
            _running = false;
            SampleEvents.ReportFinished(outcome);
        }

        private IEnumerator ReportUnlessAskedAgain(int failedFrame)
        {
            yield return null;
            if (_askedFrame < failedFrame) Report(SampleOutcome.Fail);
        }

        private void RefreshField()
        {
            var code = Asking == Entry.Pin || Asking == Entry.Passcode;
            if (code)
            {
                // PINs are personal, so they're masked; a pairing passcode is a one-time code, so it's shown.
                var shown = Asking == Entry.Pin ? new string('•', _value.Length) : _value;
                _field.text = string.Join(" ", (shown + new string('_', CodeLength - shown.Length)).ToCharArray());
                return;
            }
            var typed = _value.Length > 0 ? _value : "<color=#94A3B8>…</color>";
            _field.text = _suffix != null ? $"{typed}<color=#64748B>{_suffix}</color>" : typed;
        }

        private void SetField(bool visible)
        {
            _fieldBox.SetActive(visible);
            _field.gameObject.SetActive(visible);
            if (visible) RefreshField();
        }

        private void SetKeypad(bool visible)
        {
            foreach (var key in _keypad) key.SetActive(visible);
        }

        private void ShowButtons(params StationInteractable[] visible)
        {
            var buttons = new[] { PairButton, SignInButton, SubmitButton, SkipButton, NotNowButton, ScanButton, JoinButton, OtherNameButton };
            foreach (var button in buttons) button.gameObject.SetActive(false);

            // Visible buttons fill the row from the left.
            var x = -0.86f;
            foreach (var button in visible)
            {
                if (button == null) continue;
                button.gameObject.SetActive(true);
                var width = button.GetComponent<BoxCollider>().size.x;
                button.transform.localPosition = new Vector3(x + width / 2, -0.36f, -0.03f);
                x += width + 0.04f;
            }
        }

        private void BuildPanel(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.84f, 0.99f), material, stand: false);
            Block(root, "Brand Bar", new Vector3(0, 0.44f, -0.01f), new Vector3(1.9f, 0.17f, 0.03f), Brand);
            StationBuilder.Text(root, "<b>ACME</b>  <size=60%>TRAINING SIGN-IN</size>", new Vector3(-0.3f, 0.44f, -0.03f), 0.6f, Color.white, 1.2f, 0.15f, TextAlignmentOptions.Left);

            _prompt = StationBuilder.Text(root, "", new Vector3(-0.31f, 0.26f, -0.025f), 0.46f, Color.white, 1.1f, 0.16f, TextAlignmentOptions.TopLeft);
            _fieldBox = Block(root, "Field", new Vector3(-0.31f, 0.07f, -0.01f), new Vector3(1.1f, 0.14f, 0.03f), PropColors.Panel);
            // Blocks have no collider; the field needs one so a tap can bring the on-screen keyboard back.
            _fieldBox.AddComponent<BoxCollider>();
            _fieldBox.AddComponent<StationInteractable>().Activated += _ => OpenTouchKeyboard();
            _field = StationBuilder.Text(root, "", new Vector3(-0.31f, 0.07f, -0.03f), 0.55f, UiKit.Ink, 1.04f, 0.12f, TextAlignmentOptions.Left);
            _error = StationBuilder.Text(root, "", new Vector3(-0.31f, -0.08f, -0.025f), 0.36f, UiKit.Amber, 1.1f, 0.12f, TextAlignmentOptions.TopLeft);
            _hint = StationBuilder.Text(root, "", new Vector3(-0.31f, -0.2f, -0.025f), 0.3f, UiKit.MutedOnDark, 1.1f, 0.08f, TextAlignmentOptions.Left);

            const string digits = "1234567890";
            for (var i = 0; i < digits.Length; i++)
            {
                var digit = digits[i];
                var column = i == 9 ? 1 : i % 3;
                var row = i == 9 ? 3 : i / 3;
                var key = StationBuilder.Button(root, digit.ToString(), new Vector3(0.5f + column * 0.17f, 0.24f - row * 0.135f, -0.03f), new Vector2(0.15f, 0.115f), material, UiKit.NavyLighter, 0.5f);
                key.Activated += _ => Type(digit.ToString());
                _keys[digit] = key;
                _keypad.Add(key.gameObject);
            }
            DeleteKey = StationBuilder.Button(root, "Del", new Vector3(0.84f, 0.24f - 3 * 0.135f, -0.03f), new Vector2(0.15f, 0.115f), material, UiKit.NavyLight, 0.36f);
            DeleteKey.Activated += _ => Delete();
            _keypad.Add(DeleteKey.gameObject);

            PairButton = ActionButton(root, "Pair this device", 0.5f, () => Begin(PairRequested));
            SignInButton = ActionButton(root, "Sign in again", 0.44f, () => Begin(SignInRequested));
            SubmitButton = ActionButton(root, "Submit", 0.3f, Submit);
            SkipButton = ActionButton(root, "Skip", 0.24f, () => Answer(() => SkipRequested?.Invoke()), UiKit.NavyLighter);
            NotNowButton = ActionButton(root, "Not now", 0.3f, () => NotNowRequested?.Invoke(), UiKit.NavyLighter);
            ScanButton = ActionButton(root, "Scan badge", 0.34f, () => ScanRequested?.Invoke(), UiKit.NavyLighter);
            JoinButton = ActionButton(root, "Join", 0.5f, () => Answer(() => JoinAnswered?.Invoke(true)));
            OtherNameButton = ActionButton(root, "Use another name", 0.48f, () => Answer(() => JoinAnswered?.Invoke(false)), UiKit.NavyLighter);
        }

        private StationInteractable ActionButton(Transform root, string label, float width, Action activated, Color? color = null)
        {
            var button = StationBuilder.Button(root, label, new Vector3(0, -0.36f, -0.03f), new Vector2(width, 0.12f), material, color ?? UiKit.Purple, 0.34f);
            button.Activated += _ => activated();
            return button;
        }

        private void BuildStand(Transform root)
        {
            Block(root, "Base", new Vector3(0, 0.02f, 0), new Vector3(0.7f, 0.04f, 0.5f), PropColors.DarkSteel);
            Block(root, "Post", new Vector3(0, 0.47f, 0.05f), new Vector3(0.12f, 0.9f, 0.12f), PropColors.DarkSteel);
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
