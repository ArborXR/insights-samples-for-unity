using System;

namespace InsightsSamples.Common
{
    public enum SampleOutcome
    {
        Pass,
        Fail,
    }

    /// <summary>
    /// Implemented by lesson scripts. OnSampleExit runs before the shell leaves the sample, so the lesson can
    /// close anything it started (an assessment, objective, or interaction) with the correct status.
    /// </summary>
    public interface ISampleLifecycle
    {
        void OnSampleEnter();
        void OnSampleExit();
    }

    /// <summary>
    /// Stations report how a run ended. The shell's autopilot runner and logs listen.
    /// </summary>
    public static class SampleEvents
    {
        public static event Action<SampleOutcome> Finished;

        public static void ReportFinished(SampleOutcome outcome) => Finished?.Invoke(outcome);
    }
}
