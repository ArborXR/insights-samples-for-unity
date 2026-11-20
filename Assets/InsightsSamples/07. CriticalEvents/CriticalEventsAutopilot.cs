using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.CriticalEvents
{
    /// <summary>
    /// Pass: emergency stop, then clear the jam. Fail: reach in while the conveyor runs first, then do it
    /// properly; the critical event still fails the task.
    /// </summary>
    public class CriticalEventsAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private JammedConveyor station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.BeginButton);
            if (outcome == SampleOutcome.Fail) yield return driver.Click(station.JammedCarton);
            yield return driver.Click(station.EmergencyStop);
            yield return driver.Click(station.JammedCarton);
        }
    }
}
