using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.GazeTargets
{
    /// <summary>
    /// Pass: look at the customer, then greet them. Fail: look at the phone on the counter, then greet.
    /// </summary>
    public class GazeTargetsAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private RetailCounter station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            var look = Camera.main.GetComponent<StationLook>();
            var target = outcome == SampleOutcome.Pass ? station.CustomerFace : station.Phone;
            yield return look.TurnTowards(target.transform.position, seconds: driver.Fast ? 0f : 1f);
            yield return driver.Click(station.GreetButton);
        }
    }
}
