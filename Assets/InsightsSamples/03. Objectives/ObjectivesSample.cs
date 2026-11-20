using System.Collections.Generic;
using System.Linq;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Objectives
{
    /// <summary>
    /// Sample 03: report each step of a procedure as an objective. An objective starts when its step becomes
    /// current and completes with a score and status. The assessment (sample 02) rolls the steps up.
    /// </summary>
    public class ObjectivesSample : MonoBehaviour, ISampleLifecycle
    {
        private const string AssessmentName = "lockout_tagout";
        private const int PassMark = 80;

        // One objective per step, in procedure order. Like assessment names, these stable IDs name the
        // objectives in Insights, so the dashboard can show which steps learners miss.
        private static readonly string[] ObjectiveNames =
        {
            "loto_notify_affected",
            "loto_shut_down",
            "loto_isolate_energy",
            "loto_apply_lock_tag",
            "loto_verify_zero_energy",
        };

        [SerializeField] private LockoutStation station;

        private readonly List<int> _scores = new List<int>();
        private int _openStep = -1;
        private bool _assessmentOpen;

        private void OnEnable()
        {
            station.ProcedureStarted += HandleProcedureStarted;
            station.StepStarted += HandleStepStarted;
            station.StepFinished += HandleStepFinished;
            station.ProcedureFinished += HandleProcedureFinished;
        }

        private void OnDisable()
        {
            station.ProcedureStarted -= HandleProcedureStarted;
            station.StepStarted -= HandleStepStarted;
            station.StepFinished -= HandleStepFinished;
            station.ProcedureFinished -= HandleProcedureFinished;
        }

        private void HandleProcedureStarted()
        {
            _scores.Clear();
            Abxr.EventAssessmentStart(AssessmentName);
            _assessmentOpen = true;
        }

        private void HandleStepStarted(int step)
        {
            // Start the objective when its step becomes current. The SDK times it, so the complete carries
            // how long the learner spent on this step.
            Abxr.EventObjectiveStart(ObjectiveNames[step]);
            _openStep = step;
        }

        private void HandleStepFinished(int step, bool correct)
        {
            // Complete every objective you start, with a 0-100 score and a status: Pass when the step was done
            // right, Fail when the learner skipped it or got it wrong.
            var score = correct ? 100 : 0;
            var status = correct ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail;
            Abxr.EventObjectiveComplete(ObjectiveNames[step], score, status);
            _scores.Add(score);
            _openStep = -1;
        }

        private void HandleProcedureFinished()
        {
            // The assessment score rolls up the objectives; here it's their mean. Skipping isolation fails
            // two steps (the start test fails too), which takes the score below the pass mark.
            var score = Mathf.RoundToInt((float)_scores.Average());
            var status = score >= PassMark ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail;
            _assessmentOpen = false;
            Abxr.EventAssessmentComplete(AssessmentName, score, status);
            station.ShowResult(status == Abxr.EventStatus.Pass, score);
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            // Leaving mid-procedure: close the open objective, then its assessment, both Incomplete. That's
            // the same order the SDK uses when it closes records at quit.
            if (_openStep >= 0)
            {
                Abxr.EventObjectiveComplete(ObjectiveNames[_openStep], 0, Abxr.EventStatus.Incomplete);
                _openStep = -1;
            }

            if (!_assessmentOpen) return;
            _assessmentOpen = false;
            Abxr.EventAssessmentComplete(AssessmentName, 0, Abxr.EventStatus.Incomplete);
        }
    }
}
