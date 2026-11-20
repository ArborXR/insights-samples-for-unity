using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.DiagnosticLogs
{
    /// <summary>
    /// Pass: load job_b.json, which is valid but leaves out an optional field. Fail: load job_c.json, which is
    /// malformed.
    /// </summary>
    public class DiagnosticLogsAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private RobotCell station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.FileButton(outcome == SampleOutcome.Pass ? "job_b.json" : "job_c.json"));
        }
    }
}
