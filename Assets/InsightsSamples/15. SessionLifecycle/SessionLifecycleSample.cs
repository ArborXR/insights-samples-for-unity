using System;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SessionLifecycle
{
    /// <summary>
    /// Sample 15: sessions on a shared headset. Choose how the SDK records unfinished work, end one trainee's
    /// session cleanly, and start the next. This applies to the whole app, so the shell creates it at launch.
    /// </summary>
    public class SessionLifecycleSample : MonoBehaviour
    {
        // A short drill (an assessment, as in sample 02), so there's something open when the headset changes hands.
        private const string Drill = "headset_handover_drill";

        public static SessionLifecycleSample Instance { get; private set; }

        public event Action Changed;

        public bool DrillOpen { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // When a session ends or the app quits with work still open, the SDK completes it for you. It records
            // an open assessment as Fail by default; stopping early isn't failing, so record Incomplete instead.
            Abxr.OnQuitAssessmentStatus = Abxr.EventStatus.Incomplete;

            // When a headset comes off and goes back on, the SDK asks (with its poll UI) whether it's the same
            // person, and signs the newcomer in if not. It only asks when something subscribes. XR headsets only.
            Abxr.OnHeadsetPutOnNewSession += HandleHeadsetPutOnNewSession;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Abxr.OnHeadsetPutOnNewSession -= HandleHeadsetPutOnNewSession;
            Instance = null;
        }

        public void StartDrill()
        {
            Abxr.EventAssessmentStart(Drill);
            DrillOpen = true;
            Changed?.Invoke();
        }

        public void FinishDrill()
        {
            DrillOpen = false;
            Abxr.EventAssessmentComplete(Drill, 100, Abxr.EventStatus.Pass);
            Changed?.Invoke();
        }

        /// <summary>Leaving the sample mid-drill: close it as Incomplete, as in sample 02.</summary>
        public void CloseDrill()
        {
            if (!DrillOpen) return;
            DrillOpen = false;
            Abxr.EventAssessmentComplete(Drill, 0, Abxr.EventStatus.Incomplete);
            Changed?.Invoke();
        }

        public void HandToNextTrainee()
        {
            // End this trainee's session first: EndSession completes anything still open with the OnQuit values,
            // and sends what's queued. StartNewSession alone would discard anything not yet sent.
            Abxr.EndSession();
            // OnAuthCompleted doesn't fire for an ended session, so tell the app's sign-in state.
            ShellSignals.ReportSessionEnded();

            // Then start the next trainee's session: a new session ID, and signing in again.
            Abxr.StartNewSession();
            DrillOpen = false;
            Changed?.Invoke();
        }

        public void EndSession()
        {
            // Ends without starting another: the headset stays signed out until StartNewSession or
            // StartAuthentication. OnAuthCompleted doesn't fire for it, so tell the app's sign-in state.
            Abxr.EndSession();
            ShellSignals.ReportSessionEnded();
            DrillOpen = false;
            Changed?.Invoke();
        }

        private void HandleHeadsetPutOnNewSession()
        {
            // Someone new has the headset and the SDK is signing them in. Close what the last trainee left open.
            CloseDrill();
        }
    }
}
