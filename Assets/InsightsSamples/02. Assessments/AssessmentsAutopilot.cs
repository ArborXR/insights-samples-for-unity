using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Assessments
{
    /// <summary>
    /// Pass: answer all three correctly (100). Fail: get only the first right (33).
    /// </summary>
    public class AssessmentsAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private QuizKiosk kiosk;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(kiosk.StartButton);

            for (var question = 0; kiosk.InQuiz; question++)
            {
                var correct = kiosk.CorrectAnswerIndex;
                var choice = outcome == SampleOutcome.Pass || question == 0 ? correct : (correct + 1) % kiosk.AnswerButtons.Count;
                yield return driver.Click(kiosk.AnswerButtons[choice]);
            }
        }
    }
}
