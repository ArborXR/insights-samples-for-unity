using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SessionLifecycle
{
    /// <summary>
    /// Pass: start and finish the drill, then hand the headset over. Fail: hand it over mid-drill, so the SDK
    /// closes the drill as Incomplete.
    /// </summary>
    public class SessionLifecycleAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private HandoverCart station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.DrillButton);
            if (outcome == SampleOutcome.Pass) yield return driver.Click(station.DrillButton);
            yield return driver.Click(station.HandOverButton);
        }
    }
}
