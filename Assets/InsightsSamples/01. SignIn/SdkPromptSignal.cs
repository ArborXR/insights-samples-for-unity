using AbxrLib.Runtime.UI.ExitPoll;
using AbxrLib.Runtime.UI.Keyboard;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SignIn
{
    /// <summary>
    /// Tells the shell while any of the SDK's world-space prompts is open: the sign-in or pairing PIN pad, the
    /// keyboard, the QR scan panel, or an exit poll. Stations ignore taps until it closes.
    /// </summary>
    public class SdkPromptSignal : MonoBehaviour
    {
        private void Update() => ShellSignals.SetSdkPromptOpen(KeyboardHandler.IsOpen || ExitPollHandler.IsOpen);
    }
}
