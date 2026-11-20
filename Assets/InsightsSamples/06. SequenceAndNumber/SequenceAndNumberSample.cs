using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SequenceAndNumber
{
    /// <summary>
    /// Sample 06: two more interaction types. Sequencing for putting steps in order, with the learner's order
    /// as the response; Number for an entered value, with the value as the response.
    /// </summary>
    public class SequenceAndNumberSample : MonoBehaviour, ISampleLifecycle
    {
        private const string AssessmentName = "clinic_skills_check";
        private const string SequenceInteraction = "hand_hygiene_sequence";
        private const string RateInteraction = "infusion_rate_ml_per_h";

        // Step IDs in the correct order, matching the station's cards. The response lists them in the
        // learner's order, so wrong orders can be grouped and compared.
        private static readonly string[] HandHygieneSteps = { "wet", "soap", "scrub", "rinse", "dry" };

        // 500 mL over 10 hours.
        private const int PrescribedRate = 50;

        [SerializeField] private ClinicStation station;

        private string _openInteraction;
        private Abxr.InteractionType _openType;
        private bool _assessmentOpen;
        private int _correct;

        private void OnEnable()
        {
            station.TaskStarted += HandleTaskStarted;
            station.SequenceCompleted += HandleSequenceCompleted;
            station.RateEntered += HandleRateEntered;
        }

        private void OnDisable()
        {
            station.TaskStarted -= HandleTaskStarted;
            station.SequenceCompleted -= HandleSequenceCompleted;
            station.RateEntered -= HandleRateEntered;
        }

        private void HandleTaskStarted()
        {
            _correct = 0;
            Abxr.EventAssessmentStart(AssessmentName);
            _assessmentOpen = true;
            StartInteraction(SequenceInteraction, Abxr.InteractionType.Sequencing);
        }

        private void HandleSequenceCompleted(IReadOnlyList<int> order)
        {
            // Sequencing: the response is the learner's order as comma-separated IDs, e.g.
            // "wet,soap,scrub,rinse,dry". It's Correct only when the whole order matches.
            var response = string.Join(",", order.Select(step => HandHygieneSteps[step]));
            var correct = order.SequenceEqual(Enumerable.Range(0, HandHygieneSteps.Length));
            CompleteInteraction(correct, response);

            // The pump task starts once the hands are clean.
            StartInteraction(RateInteraction, Abxr.InteractionType.Number);
        }

        private void HandleRateEntered(int rate)
        {
            // Number: the response is the value itself. Format it with the invariant culture, so "1500" never
            // arrives as "1,500" or "1.500".
            CompleteInteraction(rate == PrescribedRate, rate.ToString(CultureInfo.InvariantCulture));

            // Both tasks are safety-critical, so passing needs both right.
            var score = 100 * _correct / 2;
            var status = score == 100 ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail;
            _assessmentOpen = false;
            Abxr.EventAssessmentComplete(AssessmentName, score, status);
            station.ShowResult(status == Abxr.EventStatus.Pass, score);
        }

        private void StartInteraction(string name, Abxr.InteractionType type)
        {
            Abxr.EventInteractionStart(name);
            _openInteraction = name;
            _openType = type;
        }

        private void CompleteInteraction(bool correct, string response)
        {
            var result = correct ? Abxr.InteractionResult.Correct : Abxr.InteractionResult.Incorrect;
            Abxr.EventInteractionComplete(_openInteraction, _openType, result, response);
            if (correct) _correct++;
            _openInteraction = null;
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            // Leaving mid-task: close the open interaction unanswered (Neutral, no response), then the
            // assessment as Incomplete.
            if (_openInteraction != null)
            {
                Abxr.EventInteractionComplete(_openInteraction, _openType, Abxr.InteractionResult.Neutral);
                _openInteraction = null;
            }

            if (!_assessmentOpen) return;
            _assessmentOpen = false;
            Abxr.EventAssessmentComplete(AssessmentName, 0, Abxr.EventStatus.Incomplete);
        }
    }
}
