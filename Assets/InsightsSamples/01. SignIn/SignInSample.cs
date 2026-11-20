using System;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SignIn
{
    /// <summary>
    /// Sample 01: sign in to ArborXR Insights. Sign-in applies to the whole app, so the shell creates this at
    /// launch and it lives for the session.
    /// </summary>
    public class SignInSample : MonoBehaviour
    {
        public static SignInSample Instance { get; private set; }

        public event Action Changed;

        public bool SignedIn { get; private set; }
        public string LastError { get; private set; }
        public int AuthResults { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Subscribe in Awake. With "Enable Auto Start Authentication" on (the default), the SDK starts
            // signing in one frame after launch, and OnAuthCompleted reports how it went. A false isn't always final:
            // offline, the SDK reports false once, keeps retrying, and reports true when a retry succeeds.
            Abxr.OnAuthCompleted += HandleAuthCompleted;
            ShellSignals.SessionEnded += HandleSessionEnded;
        }

        private void Start()
        {
            if (Instance == this) Publish();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Abxr.OnAuthCompleted -= HandleAuthCompleted;
            ShellSignals.SessionEnded -= HandleSessionEnded;
            Instance = null;
        }

        // The app passes nothing in: the SDK already knows the organization, from ArborXR device management, an org
        // token, or a pairing (sample 18). Calling this is only needed to sign in again, such as after EndSession().
        public void SignInAgain() => Abxr.StartAuthentication();

        // Ending a session (EndSession, or clearing a pairing) raises no OnAuthCompleted, so the samples that end
        // one report it.
        private void HandleSessionEnded(string reason)
        {
            SignedIn = false;
            LastError = reason;
            Publish();
        }

        private void HandleAuthCompleted(bool success, string errorMessage)
        {
            // Just record the state: this can run again for the same sign-in. A device with no organization yet
            // reports one failure ("No organization identity"). Where ArborXR doesn't manage the device, that means
            // "pair first" (sample 18), not that something is broken.
            SignedIn = success;
            LastError = success ? null : errorMessage;
            AuthResults++;
            Publish();
        }

        private void Publish()
        {
            ShellSignals.SetSignIn(SignedIn, AuthResults > 0);
            Changed?.Invoke();
        }
    }
}
