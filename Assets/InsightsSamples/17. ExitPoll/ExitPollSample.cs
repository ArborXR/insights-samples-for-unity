using System.Collections.Generic;
using AbxrLib.Runtime.Types;
using UnityEngine;

namespace InsightsSamples.ExitPoll
{
    /// <summary>
    /// Sample 17: ask the learner for feedback at the end of training with PollUser: a 1-5 rating, a thumbs up or
    /// down, and a multiple-choice question.
    /// </summary>
    public class ExitPollSample : MonoBehaviour
    {
        private const string ConfidencePrompt = "How confident do you feel doing this on the job?";
        private const string UsefulPrompt = "Was this training useful?";
        private const string NextTopicPrompt = "What should we cover next?";
        private static readonly List<string> NextTopics = new List<string> { "Fire alarms", "Evacuation routes", "Fire blankets" };

        [SerializeField] private TrainingTablet station;

        private void OnEnable() => station.TrainingFinished += HandleTrainingFinished;

        private void OnDisable() => station.TrainingFinished -= HandleTrainingFinished;

        private void HandleTrainingFinished()
        {
            // PollUser shows the SDK's poll UI. Polls queue and show one at a time, and the callback hands the
            // answer to the app: "1" to "5" for a rating, "up" or "down" for thumbs, and the chosen response for
            // multiple choice. The World-Space UI sample's poll UI also records each answer as a "poll" event with
            // the prompt and the answer; a poll UI of your own (AbxrUi.RegisterPollUi) records nothing unless it
            // calls Abxr.Event itself. The prompt and the chosen response are recorded as shown, so keep them
            // fixed: rewording or translating one splits its results.
            // An open poll outlives a scene load, so a callback can arrive after the learner has left this scene:
            // check the station is still there.
            Abxr.PollUser(ConfidencePrompt, PollType.Rating, null, answer => { if (station) station.ShowConfidence(answer); });
            Abxr.PollUser(UsefulPrompt, PollType.Thumbs, null, answer => { if (station) station.ShowUseful(answer); });
            Abxr.PollUser(NextTopicPrompt, PollType.MultipleChoice, NextTopics, answer => { if (station) station.ShowNextTopic(answer); });
        }
    }
}
