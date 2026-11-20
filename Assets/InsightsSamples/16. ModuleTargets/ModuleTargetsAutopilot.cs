using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.ModuleTargets
{
    /// <summary>
    /// Pass: open the tires module and tag the forklift out. Fail: call it safe despite the damaged tire.
    /// </summary>
    public class ModuleTargetsAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private ForkliftCheck station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.ModuleButton("preshift_tires"));
            yield return driver.Click(outcome == SampleOutcome.Pass ? station.TagOutButton : station.SafeButton);
        }
    }
}
