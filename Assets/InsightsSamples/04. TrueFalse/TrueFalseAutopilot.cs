using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.TrueFalse
{
    /// <summary>
    /// Pass: flag the three hazards (100). Fail: miss the blocked exit and flag the guarded grinder (67 - 34 = 33).
    /// </summary>
    public class TrueFalseAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private static readonly int[] HazardItems = { 0, 1, 2 };
        private static readonly int[] MistakenItems = { 0, 2, 3 };

        [SerializeField] private Workshop station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.StartButton);
            foreach (var item in outcome == SampleOutcome.Pass ? HazardItems : MistakenItems)
                yield return driver.Click(station.Item(item));
            yield return driver.Click(station.FinishButton);
        }
    }
}
