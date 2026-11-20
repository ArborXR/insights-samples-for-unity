using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Telemetry
{
    /// <summary>
    /// Pass: drag the torch steadily along the seam. Fail: wander about 20 mm either side of it.
    /// </summary>
    public class TelemetryAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private WeldStation station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Drag(station.TorchHandle, station.SeamPath(steady: outcome == SampleOutcome.Pass), seconds: 4f);
        }
    }
}
