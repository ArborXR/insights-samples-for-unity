using System.Collections;
using System.Linq;
using AbxrLib.Runtime.UI.ExitPoll;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsightsSamples.ExitPoll
{
    /// <summary>
    /// Answers the SDK's poll UI the way a learner would. Pass: confidence 4, thumbs up. Fail: confidence 2,
    /// thumbs down. Both choose "Evacuation routes" as the next topic.
    /// </summary>
    public class ExitPollAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private const float PollTimeout = 10f;

        [SerializeField] private TrainingTablet station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            var pass = outcome == SampleOutcome.Pass;
            yield return driver.Click(station.FinishButton);

            ExitPollRating rating = null;
            yield return WaitFor(() => rating = FindAnyObjectByType<ExitPollRating>());
            if (rating != null) yield return driver.ClickUi(pass ? rating.fourRatingButton : rating.twoRatingButton);

            ExitPollThumbs thumbs = null;
            yield return WaitFor(() => thumbs = FindAnyObjectByType<ExitPollThumbs>());
            if (thumbs != null) yield return driver.ClickUi(pass ? thumbs.thumbsUpButton : thumbs.thumbsDownButton);

            Button topic = null;
            yield return WaitFor(() => topic = FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(button => button.GetComponentInChildren<TMP_Text>()?.text == "Evacuation routes"));
            if (topic != null) yield return driver.ClickUi(topic);
        }

        /// <summary>Waits for the poll UI to appear, then a frame more while it wires up its buttons.</summary>
        private static IEnumerator WaitFor(System.Func<Object> find)
        {
            for (float waited = 0; find() == null && waited < PollTimeout; waited += Time.unscaledDeltaTime) yield return null;
            yield return null;
        }
    }
}
