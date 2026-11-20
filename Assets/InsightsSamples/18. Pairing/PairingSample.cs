using System;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Pairing
{
    /// <summary>
    /// Sample 18: on a device that ArborXR doesn't manage, pair with your organization using a passcode from the
    /// Portal. A managed device, or an app with an org token, already has its organization and never pairs.
    /// Pairing applies to the whole app, so the shell creates this at launch and it lives for the session.
    /// </summary>
    public class PairingSample : MonoBehaviour
    {
        public static PairingSample Instance { get; private set; }

        public event Action Changed;

        // Managed: ArborXR device management or an org token names the organization, so pairing doesn't apply.
        public bool Managed => Abxr.GetPairingState() == Abxr.PairingState.Managed;
        public bool CanPair => Abxr.GetPairingState() == Abxr.PairingState.Unpaired;
        public bool Paired => Abxr.GetPairingState() == Abxr.PairingState.Paired;
        public string PairingStatus => Abxr.GetPairingState().ToString();
        public string DeviceName => Abxr.GetPairedDeviceName();

        // A wrong, expired, or revoked passcode: the prompt shows the error and asks again. Nothing settles, so
        // OnPairingStateChanged doesn't fire; the last redeem result says what happened.
        public bool PasscodeRejected => Abxr.GetLastPairingRedeemResult().Error == Abxr.PairingRedeemError.InvalidPasscode;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Abxr.OnPairingStateChanged += HandlePairingStateChanged;
        }

        private void Start()
        {
            if (Instance != this) return;

            // The SDK never opens the pairing prompt by itself. Subscribe first (in Awake), then check the
            // state, because the startup decision can arrive before a late subscriber exists.
            if (Abxr.GetPairingState() == Abxr.PairingState.Unpaired && !DeepLinks.KeepPairingPromptClosed)
                OfferPairing();

            Publish();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Abxr.OnPairingStateChanged -= HandlePairingStateChanged;
            Instance = null;
        }

        public void OfferPairing()
        {
            // Pairing runs in Android and WebGL builds only. Elsewhere, or without an App Token, nothing opens, and an
            // SDK warning says why (in the Editor and development builds). Sample 19 shows using the false it returns.
            Abxr.StartPairing();
            Publish();
        }

        public void ClearPairing()
        {
            // Deletes the stored pairing. A signed-in session ends the way EndSession() does: anything open is
            // closed and queued data is flushed first.
            Abxr.ClearPairing();
        }

        private void HandlePairingStateChanged(Abxr.PairingState state, Abxr.PairingChangeReason reason)
        {
            // Clearing the pairing ends the session the way EndSession() does, and OnAuthCompleted doesn't fire for
            // that: tell the app's sign-in state (sample 01). (A revoke in the Portal is found when the device next
            // signs in, and that sign-in reports OnAuthCompleted(false, "Pairing revoked") after this.)
            if (reason == Abxr.PairingChangeReason.Cleared) ShellSignals.ReportSessionEnded("Pairing cleared");

            // Unpaired at startup, after ClearPairing, or after a revoke in the Portal: offer pairing.
            // After "Not now" (Dismissed), wait until the learner asks again.
            if (state == Abxr.PairingState.Unpaired && reason != Abxr.PairingChangeReason.Dismissed
                && !DeepLinks.KeepPairingPromptClosed)
                OfferPairing();
            else
                Publish();
        }

        private void Publish()
        {
            var pairing = Abxr.GetPairingState();
            var needsPairing = pairing == Abxr.PairingState.Unpaired || pairing == Abxr.PairingState.Prompting
                               || pairing == Abxr.PairingState.Redeeming;
            ShellSignals.SetPairing(needsPairing, Paired, DeviceName);
            Changed?.Invoke();
        }
    }
}
