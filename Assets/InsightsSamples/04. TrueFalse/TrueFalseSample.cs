using System;
using System.Linq;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.TrueFalse
{
    /// <summary>
    /// Sample 04: ask "is this a hazard?" about each thing in a workshop as a true/false (Bool) interaction.
    /// The response is what the learner answered; the result says whether that was right.
    /// </summary>
    public class TrueFalseSample : MonoBehaviour, ISampleLifecycle
    {
        private const string AssessmentName = "hazard_spotting";
        private const int PassMark = 67;
        private const int FalseFlagPenalty = 34;

        // One interaction per thing the learner can flag, in the station's order. The guarded grinder is a
        // decoy: leaving it unflagged is the right answer.
        private static readonly (string id, bool isHazard)[] Items =
        {
            ("hazard_frayed_cable", true),
            ("hazard_blocked_exit", true),
            ("hazard_oil_spill", true),
            ("decoy_guarded_grinder", false),
        };

        private static readonly int HazardCount = Items.Count(item => item.isHazard);

        [SerializeField] private Workshop station;

        private readonly bool[] _answered = new bool[Items.Length];
        private int _found;
        private int _falseFlags;
        private bool _open;

        private void OnEnable()
        {
            station.InspectionStarted += HandleInspectionStarted;
            station.ItemFlagged += HandleItemFlagged;
            station.InspectionFinished += HandleInspectionFinished;
        }

        private void OnDisable()
        {
            station.InspectionStarted -= HandleInspectionStarted;
            station.ItemFlagged -= HandleItemFlagged;
            station.InspectionFinished -= HandleInspectionFinished;
        }

        private void HandleInspectionStarted()
        {
            Array.Clear(_answered, 0, _answered.Length);
            _found = 0;
            _falseFlags = 0;
            Abxr.EventAssessmentStart(AssessmentName);

            // Start every interaction together: each question is open from the moment the learner sees the
            // workshop, so a flagged item's duration is the time it took to spot.
            foreach (var (id, _) in Items)
            {
                Abxr.EventInteractionStart(id);
            }
            _open = true;
        }

        private void HandleItemFlagged(int index) => Answer(index, flagged: true);

        private void HandleInspectionFinished()
        {
            // Anything still unflagged at the end is answered "false".
            for (var index = 0; index < Items.Length; index++)
                if (!_answered[index]) Answer(index, flagged: false);

            var score = Mathf.Max(0, Mathf.RoundToInt(100f * _found / HazardCount) - FalseFlagPenalty * _falseFlags);
            var status = score >= PassMark ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail;
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, score, status);
            station.ShowResult(status == Abxr.EventStatus.Pass, score, _found, _falseFlags);
        }

        private void Answer(int index, bool flagged)
        {
            var (id, isHazard) = Items[index];
            // A Bool interaction's response is "true" or "false" (here: "I flagged it"). The result compares it
            // with the right answer, so Insights can report how often each hazard is missed.
            var response = flagged ? "true" : "false";
            var result = flagged == isHazard ? Abxr.InteractionResult.Correct : Abxr.InteractionResult.Incorrect;
            Abxr.EventInteractionComplete(id, Abxr.InteractionType.Bool, result, response);

            _answered[index] = true;
            if (flagged && isHazard) _found++;
            if (flagged && !isHazard) _falseFlags++;
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            if (!_open) return;

            // Leaving mid-inspection: close the unanswered questions without judging them (Neutral, no
            // response), then the assessment as Incomplete.
            for (var index = 0; index < Items.Length; index++)
            {
                if (_answered[index]) continue;
                Abxr.EventInteractionComplete(Items[index].id, Abxr.InteractionType.Bool, Abxr.InteractionResult.Neutral);
            }
            _open = false;
            Abxr.EventAssessmentComplete(AssessmentName, 0, Abxr.EventStatus.Incomplete);
        }
    }
}
