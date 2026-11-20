using System.Globalization;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.DiagnosticLogs
{
    /// <summary>
    /// Sample 12: diagnostic logs for developers. Info for normal milestones, Warn when the app carries on with a
    /// fallback, Error when something fails. Learner mistakes and performance are events, never logs.
    /// </summary>
    public class DiagnosticLogsSample : MonoBehaviour
    {
        [SerializeField] private RobotCell station;

        private void OnEnable()
        {
            station.JobLoaded += HandleJobLoaded;
            station.JobFailed += HandleJobFailed;
        }

        private void OnDisable()
        {
            station.JobLoaded -= HandleJobLoaded;
            station.JobFailed -= HandleJobFailed;
        }

        private void HandleJobLoaded(JobResult result)
        {
            if (result.UsedDefaultSpeed)
            {
                // Warn: the app works, but a developer should fix the file. Say what was used instead.
                var warning = new Abxr.Dict
                {
                    ["file"] = result.FileName,
                    ["field"] = "speed_pct",
                    ["default_used"] = result.SpeedPct.ToString(CultureInfo.InvariantCulture),
                };
                Abxr.LogWarn("Job file has no speed_pct; using the default", warning);
            }

            // Info: a normal milestone that helps explain what happened next. Keep messages fixed and put the
            // details in metadata, so logs can be grouped and searched.
            var loaded = new Abxr.Dict { ["file"] = result.FileName, ["program"] = result.Job.program };
            Abxr.LogInfo("Job file loaded", loaded);
        }

        private void HandleJobFailed(string fileName, string error)
        {
            // Error: something failed and the app couldn't recover on its own. Include what a developer needs
            // to find the cause.
            var failure = new Abxr.Dict { ["file"] = fileName, ["error"] = error };
            Abxr.LogError("Job file failed to load", failure);
        }
    }
}
