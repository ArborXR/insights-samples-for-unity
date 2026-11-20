using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Assessments
{
    /// <summary>
    /// Sample 02: report a quiz as one assessment. EventAssessmentStart when it begins, then exactly one
    /// EventAssessmentComplete when it ends, with a score and a pass or fail status.
    /// </summary>
    public class AssessmentsSample : MonoBehaviour, ISampleLifecycle
    {
        // A stable snake_case ID. It names this assessment in Insights dashboards and LMS reports, so don't
        // change it once data exists.
        private const string AssessmentName = "site_induction";
        private const int PassMark = 70;

        [SerializeField] private QuizKiosk kiosk;

        private bool _open;

        private void OnEnable()
        {
            kiosk.QuizStarted += HandleQuizStarted;
            kiosk.QuizFinished += HandleQuizFinished;
        }

        private void OnDisable()
        {
            kiosk.QuizStarted -= HandleQuizStarted;
            kiosk.QuizFinished -= HandleQuizFinished;
        }

        private void HandleQuizStarted()
        {
            // Start the assessment when the learner starts. The SDK times it, so the matching complete
            // arrives with its duration.
            Abxr.EventAssessmentStart(AssessmentName);
            _open = true;
        }

        private void HandleQuizFinished(int score)
        {
            // Send a 0-100 score and a status. Insights reports pass rates from the status, so decide it here
            // instead of relying on the default (EventStatus.Complete).
            var status = score >= PassMark ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail;
            // Mark it closed first. In an LMS module sequence, completing any assessment moves the SDK to the next
            // module during this call, and the app may leave this sample before the call returns.
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, score, status);
            kiosk.ShowOutcome(status == Abxr.EventStatus.Pass, score);
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            // Leaving mid-quiz: close the assessment rather than leave it open. Incomplete, not Fail, because
            // the learner didn't finish, which isn't the same as failing.
            if (!_open) return;
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, 0, Abxr.EventStatus.Incomplete);
        }
    }
}
