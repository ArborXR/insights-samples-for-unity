using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SequenceAndNumber
{
    /// <summary>
    /// Pass: the steps in order and 50 mL/h (100). Fail: rinse before scrubbing, and 500 mL/h, the volume
    /// instead of the rate (0).
    /// </summary>
    public class SequenceAndNumberAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private static readonly int[] CorrectOrder = { 0, 1, 2, 3, 4 };
        private static readonly int[] RinseTooEarly = { 0, 1, 3, 2, 4 };

        [SerializeField] private ClinicStation station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            var pass = outcome == SampleOutcome.Pass;
            yield return driver.Click(station.BeginButton);
            foreach (var step in pass ? CorrectOrder : RinseTooEarly)
                yield return driver.Click(station.StepCard(step));
            foreach (var digit in pass ? "50" : "500")
                yield return driver.Click(station.Key(digit - '0'));
            yield return driver.Click(station.StartInfusionButton);
        }
    }
}
