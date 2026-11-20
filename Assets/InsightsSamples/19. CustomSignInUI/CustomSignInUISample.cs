using System;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.CustomSignInUI
{
    /// <summary>
    /// Sample 19: your own sign-in panel instead of the SDK's. One handler on OnInputRequested receives every
    /// request (sign-in and pairing), shows the right input and any error, and answers with OnInputSubmitted.
    /// </summary>
    public class CustomSignInUISample : MonoBehaviour, ISampleLifecycle
    {
        // The SDK's answer for "not this one". At the join step, it asks for a different name.
        private const string SkipAnswer = "**skip**";

        [SerializeField] private AcmeSignInPanel station;

        private string _joinName;

        private void Awake()
        {
            // Assign the handler (=), don't subscribe (+=): there's only one, and while it's set the SDK never shows
            // its own prompt. In your app, assign it at launch, before sign-in starts one frame later. This sample
            // assigns it when it opens, because the other samples use the SDK's prompt.
            Abxr.OnInputRequested = HandleInputRequested;
            Abxr.OnAuthCompleted += HandleAuthCompleted;
            Abxr.OnPairingStateChanged += HandlePairingStateChanged;
        }

        private void OnDestroy()
        {
            Abxr.OnInputRequested = null;
            Abxr.OnAuthCompleted -= HandleAuthCompleted;
            Abxr.OnPairingStateChanged -= HandlePairingStateChanged;
        }

        private void OnEnable()
        {
            station.PairRequested += Pair;
            station.SignInRequested += SignInAgain;
            station.Submitted += Submit;
            station.SkipRequested += SkipDeviceName;
            station.JoinAnswered += AnswerJoin;
            station.NotNowRequested += NotNow;
            station.ScanRequested += ScanBadge;
        }

        private void OnDisable()
        {
            station.PairRequested -= Pair;
            station.SignInRequested -= SignInAgain;
            station.Submitted -= Submit;
            station.SkipRequested -= SkipDeviceName;
            station.JoinAnswered -= AnswerJoin;
            station.NotNowRequested -= NotNow;
            station.ScanRequested -= ScanBadge;
        }

        public void OnSampleEnter()
        {
            // The SDK's own pairing prompt may already be open (sample 18 offers pairing at launch). Close it and
            // open it again, so it comes here.
            if (Abxr.GetPairingState() == Abxr.PairingState.Prompting)
            {
                NotNow();
                Pair();
            }
            else if (Abxr.IsAuthInputRequestPending())
                station.ShowIdle("The SDK's own sign-in prompt is open. Sign in again to answer here instead.", CanPair, canSignIn: true);
            else
                ShowReady();
        }

        public void OnSampleExit()
        {
            // Leaving with a prompt open: close it, so nothing waits on a panel that's gone. Without a handler,
            // the SDK's own prompt asks from now on.
            Abxr.OnInputRequested = null;
            if (Abxr.GetPairingState() == Abxr.PairingState.Prompting) NotNow();
            else if (Abxr.IsAuthInputRequestPending()) SignInAgain();
        }

        private void HandleInputRequested(string type, string prompt, string domain, string error)
        {
            // Each request says what to ask for. After a wrong answer the same type comes back with error set.
            // Badge scanning (Quest and Pico) is offered only when the SDK says it would be accepted.
            var canScan = Abxr.IsQRScanForAuthAvailable();
            switch (type)
            {
                case "assessmentPin":
                    station.Ask(AcmeSignInPanel.Entry.Pin, string.IsNullOrEmpty(prompt) ? "Enter your 6-digit PIN" : $"Enter your {prompt} PIN", error, canScan: canScan);
                    break;
                case "email":
                    // The SDK adds "@" and the domain when the answer has none, so ask for the username only.
                    station.Ask(AcmeSignInPanel.Entry.Email, "Enter your email username", error, suffix: "@" + domain, canScan: canScan);
                    break;
                case "pairingPasscode":
                    station.Ask(AcmeSignInPanel.Entry.Passcode, "Enter the pairing passcode from your Insights admin", error, canDismiss: true);
                    break;
                case "pairingDeviceName":
                case "pairingDeviceNameRequired":
                    station.Ask(AcmeSignInPanel.Entry.DeviceName, prompt, error, canSkip: type == "pairingDeviceName", canDismiss: true);
                    break;
                case "pairingDeviceJoin":
                    // A paired device in the organization already has this name (domain carries it). Joining adds this
                    // app to that device, which may be a different headset, so let the learner confirm it.
                    _joinName = domain;
                    station.AskToJoin(prompt, domain, canDismiss: true);
                    break;
                default:
                    // "text": a login such as an employee ID.
                    station.Ask(AcmeSignInPanel.Entry.Text, string.IsNullOrEmpty(prompt) ? "Enter your login" : $"Enter your {prompt}", error, canScan: canScan);
                    break;
            }
        }

        private void Submit(string value)
        {
            // Answers the open request. A wrong answer comes back as another request, with error set.
            Abxr.OnInputSubmitted(value);
        }

        // An empty name pairs without one. Only "pairingDeviceName" offers this; "pairingDeviceNameRequired" doesn't.
        private void SkipDeviceName() => Submit("");

        // Submitting the taken name adds this app to that headset; SkipAnswer asks for a different name.
        private void AnswerJoin(bool join) => Submit(join ? _joinName : SkipAnswer);

        private void NotNow()
        {
            // Closes the pairing prompt without pairing, at any step.
            Abxr.CancelPairing();
        }

        private void ScanBadge()
        {
            // The callback gets the scanned value, or null if the learner cancelled the scan.
            Action<string> handleScan = value =>
            {
                if (value != null) Submit(value);
                else station.AskAgain();
            };
            Abxr.StartQRScanForAuthInput(handleScan);
        }

        private void Pair()
        {
            // As in sample 18; with the handler set, the passcode request comes here. False when this build or
            // platform can't pair, with an SDK warning that says why (in the Editor and development builds).
            if (!Abxr.StartPairing())
                station.ShowRefused("This build can't pair: pairing needs a WebGL or Android build with an App Token. The console says why.", canPair: false, canSignIn: true);
        }

        private void SignInAgain()
        {
            station.ShowIdle("Signing in…", canPair: false, canSignIn: false);
            // Sign-in starts over, as in sample 15: end this session, then start a new one, and the SDK asks this
            // panel for whatever the app's sign-in needs.
            Abxr.EndSession();
            ShellSignals.ReportSessionEnded();
            Abxr.StartNewSession();
        }

        private void HandleAuthCompleted(bool success, string errorMessage)
        {
            // Sign-in results land here, possibly more than once for one sign-in (a false while offline, then true when
            // a retry succeeds). After a wrong PIN the SDK also asks again, and the panel shows that.
            if (success) station.ShowSignedIn("This app is sending data.", canSignIn: true);
            else station.ShowFailed(errorMessage, CanPair, canSignIn: true);
        }

        private void HandlePairingStateChanged(Abxr.PairingState state, Abxr.PairingChangeReason reason)
        {
            if (reason == Abxr.PairingChangeReason.Dismissed) station.ShowDismissed(CanPair);
            else if (state == Abxr.PairingState.Paired) station.ShowIdle("<color=#05DA98><b>Paired.</b></color> Signing in…", canPair: false, canSignIn: false);
        }

        private void ShowReady() => station.ShowIdle(Abxr.GetPairingState() == Abxr.PairingState.Unpaired
            ? "Not paired. Pair this device, and the SDK asks this panel for the passcode."
            : "Sign in again, and the SDK asks this panel for whatever sign-in needs.", CanPair, canSignIn: true);

        private static bool CanPair => Abxr.GetPairingState() == Abxr.PairingState.Unpaired;
    }
}
