using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Storage
{
    /// <summary>
    /// Both runs reset first, so saved progress from an earlier run doesn't change them. Pass: complete all
    /// three chapters. Fail: complete one, then reset the progress.
    /// </summary>
    public class StorageAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private CourseConsole station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.ResetButton);
            var chapters = outcome == SampleOutcome.Pass ? 3 : 1;
            for (var i = 0; i < chapters; i++) yield return driver.Click(station.CompleteButton);
            if (outcome == SampleOutcome.Fail) yield return driver.Click(station.ResetButton);
        }
    }
}
