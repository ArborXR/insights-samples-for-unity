using System;

namespace InsightsSamples.Common
{
    public enum ConnectionState
    {
        Unknown,
        SignedIn,
        NotPaired,
        NotSignedIn,
    }

    /// <summary>
    /// App-wide state that sample scripts publish for the shell, so the shell never references the SDK.
    /// </summary>
    public static class ShellSignals
    {
        public static event Action Changed;

        public static ConnectionState Connection { get; private set; }
        public static string ConnectionDetail { get; private set; }

        /// <summary>This device got its organization by pairing with a passcode (sample 18).</summary>
        public static bool Paired { get; private set; }

        /// <summary>The name this device paired under, if it gave one.</summary>
        public static string PairedDeviceName { get; private set; }

        /// <summary>
        /// True while an SDK prompt (sign-in, pairing, keyboard, QR scan, or exit poll) is open in the world. Overlays
        /// step aside and stations ignore taps.
        /// </summary>
        public static bool SdkPromptOpen { get; private set; }

        /// <summary>How to switch this build to pairing, for sample 18 to show on a managed device, or null.</summary>
        public static string PairingHint { get; private set; }

        private static bool _signedIn;
        private static bool _signInSettled;
        private static bool _needsPairing;

        /// <summary>Sample 01 reports sign-in: whether the app is signed in, and whether a result has arrived yet.</summary>
        public static void SetSignIn(bool signedIn, bool settled)
        {
            _signedIn = signedIn;
            _signInSettled = settled;
            PublishConnection();
        }

        /// <summary>Sample 18 reports pairing: whether this device still has to pair, or has, and under which name.</summary>
        public static void SetPairing(bool needsPairing, bool paired, string deviceName)
        {
            _needsPairing = needsPairing;
            Paired = paired;
            PairedDeviceName = paired ? deviceName : null;
            PublishConnection();
        }

        private static void PublishConnection()
        {
            var (state, detail) =
                _signedIn ? (ConnectionState.SignedIn, string.IsNullOrEmpty(PairedDeviceName) ? "Signed in" : $"Paired: {PairedDeviceName}")
                : _needsPairing ? (ConnectionState.NotPaired, "Not paired")
                : (ConnectionState.NotSignedIn, _signInSettled ? "Not signed in" : "Signing in…");
            if (Connection == state && ConnectionDetail == detail) return;
            Connection = state;
            ConnectionDetail = detail;
            Changed?.Invoke();
        }

        /// <summary>
        /// Raised when a sample ends the SDK session, with EndSession or by clearing the pairing. The SDK has no
        /// callback for that, so the app's sign-in state hears it here, with the reason to show.
        /// </summary>
        public static event Action<string> SessionEnded;

        public static void ReportSessionEnded(string reason = "Session ended") => SessionEnded?.Invoke(reason);

        public static void SetPairingHint(string hint)
        {
            if (PairingHint == hint) return;
            PairingHint = hint;
            Changed?.Invoke();
        }

        public static void SetSdkPromptOpen(bool open)
        {
            if (SdkPromptOpen == open) return;
            SdkPromptOpen = open;
            Changed?.Invoke();
        }
    }
}
