using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.MultipleChoice
{
    /// <summary>
    /// Pass: nitrile gloves. Fail: latex gloves.
    /// </summary>
    public class MultipleChoiceAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private const int Nitrile = 0;
        private const int Latex = 1;

        [SerializeField] private GloveStation station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.BeginButton);
            yield return driver.Click(station.Option(outcome == SampleOutcome.Pass ? Nitrile : Latex));
        }
    }
}
