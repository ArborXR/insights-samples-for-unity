using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.MultipleChoice
{
    /// <summary>
    /// Sample 05: a question with several options is a Select interaction. The response is the ID of the
    /// option the learner chose, so Insights can show which wrong answers learners pick.
    /// </summary>
    public class MultipleChoiceSample : MonoBehaviour, ISampleLifecycle
    {
        private const string AssessmentName = "chemical_ppe";
        private const string InteractionName = "ppe_glove_selection";

        // One stable ID per option, in the station's order. Send IDs, not display text: responses are grouped
        // by value, and IDs survive rewording and translation.
        private static readonly string[] Options = { "glove_nitrile", "glove_latex", "glove_cotton", "glove_none" };
        private const string CorrectOption = "glove_nitrile";

        [SerializeField] private GloveStation station;

        private bool _open;

        private void OnEnable()
        {
            station.TaskStarted += HandleTaskStarted;
            station.OptionChosen += HandleOptionChosen;
        }

        private void OnDisable()
        {
            station.TaskStarted -= HandleTaskStarted;
            station.OptionChosen -= HandleOptionChosen;
        }

        private void HandleTaskStarted()
        {
            Abxr.EventAssessmentStart(AssessmentName);

            // Start the interaction when the question appears, so its duration is the time taken to decide.
            Abxr.EventInteractionStart(InteractionName);
            _open = true;
        }

        private void HandleOptionChosen(int option)
        {
            var response = Options[option];
            var result = response == CorrectOption ? Abxr.InteractionResult.Correct : Abxr.InteractionResult.Incorrect;
            Abxr.EventInteractionComplete(InteractionName, Abxr.InteractionType.Select, result, response);

            var passed = result == Abxr.InteractionResult.Correct;
            var score = passed ? 100 : 0;
            var status = passed ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail;
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, score, status);
            station.ShowResult(passed);
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            if (!_open) return;

            // Leaving before choosing: close the question unanswered (Neutral, no response), then the
            // assessment as Incomplete.
            Abxr.EventInteractionComplete(InteractionName, Abxr.InteractionType.Select, Abxr.InteractionResult.Neutral);
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, 0, Abxr.EventStatus.Incomplete);
        }
    }
}
