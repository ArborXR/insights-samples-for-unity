using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.TimedEvents
{
    /// <summary>
    /// Pass: pick the three ordered items. Fail: also pick a hard hat that isn't on the order (one mispick).
    /// </summary>
    public class TimedEventsAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private const int HardHatBin = 3;

        [SerializeField] private PickFace station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.AcceptButton);
            for (var line = 0; line < station.OrderedBins.Count; line++)
            {
                yield return driver.Click(station.Bin(station.OrderedBins[line]));
                if (outcome == SampleOutcome.Fail && line == 0) yield return driver.Click(station.Bin(HardHatBin));
            }
            yield return driver.Click(station.ScanOutButton);
        }
    }
}
