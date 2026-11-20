using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.CriticalEvents
{
    /// <summary>
    /// Sample 07: flag a safety violation with EventCritical. The lesson then fails the attempt, whatever else
    /// the learner gets right: EventCritical only records the violation.
    /// </summary>
    public class CriticalEventsSample : MonoBehaviour, ISampleLifecycle
    {
        private const string AssessmentName = "conveyor_jam_clearing";
        private const string HandInRunningConveyor = "hand_in_running_conveyor";

        [SerializeField] private JammedConveyor station;

        private bool _open;
        private bool _violation;

        private void OnEnable()
        {
            station.TaskStarted += HandleTaskStarted;
            station.ReachedIntoRunningConveyor += HandleReachedIntoRunningConveyor;
            station.JamCleared += HandleJamCleared;
        }

        private void OnDisable()
        {
            station.TaskStarted -= HandleTaskStarted;
            station.ReachedIntoRunningConveyor -= HandleReachedIntoRunningConveyor;
            station.JamCleared -= HandleJamCleared;
        }

        private void HandleTaskStarted()
        {
            _violation = false;
            Abxr.EventAssessmentStart(AssessmentName);
            _open = true;
        }

        private void HandleReachedIntoRunningConveyor()
        {
            // Critical events are for actions that could seriously hurt someone. The SDK records this one as
            // CRITICAL_ABXR_hand_in_running_conveyor, which puts it in Insights' Critical Choices chart.
            // Ordinary mistakes don't belong here: they show up in scores and statuses.
            Abxr.EventCritical(HandInRunningConveyor);
            _violation = true;
        }

        private void HandleJamCleared()
        {
            // A critical violation fails the attempt, even though the learner went on to clear the jam.
            var score = _violation ? 0 : 100;
            var status = _violation ? Abxr.EventStatus.Fail : Abxr.EventStatus.Pass;
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, score, status);
            station.ShowResult(!_violation);
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            if (!_open) return;

            // Leaving before the jam is cleared: Incomplete, unless a violation has already failed the attempt.
            var status = _violation ? Abxr.EventStatus.Fail : Abxr.EventStatus.Incomplete;
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, 0, status);
        }
    }
}
