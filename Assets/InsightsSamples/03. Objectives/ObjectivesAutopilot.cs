using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Objectives
{
    /// <summary>
    /// Pass: every step in order (score 100). Fail: skip isolating energy, so the start test fails too (60).
    /// </summary>
    public class ObjectivesAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private const int IsolateEnergyStep = 2;

        [SerializeField] private LockoutStation station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.BeginButton);

            for (var step = 0; step < LockoutStation.StepCount; step++)
            {
                if (outcome == SampleOutcome.Fail && step == IsolateEnergyStep) continue;
                yield return driver.Click(station.ActionFor(step));
            }
        }
    }
}
