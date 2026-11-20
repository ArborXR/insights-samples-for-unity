using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// One-line JSON logs for automation: "[sample] {...}" when a sample opens and when an autopilot run
    /// starts and finishes. On in development builds, or with the stream_log=1 deep link.
    /// </summary>
    public static class SampleLog
    {
        public static bool Enabled => Debug.isDebugBuild || DeepLinks.Get(DeepLinks.StreamLog) == "1";

        public static void Opened(SampleInfo sample) =>
            Write(new JsonText().Field("id", sample != null ? sample.id : "home").Field("state", "opened"));

        public static void Started(SampleInfo sample, SampleOutcome outcome)
        {
            var expected = outcome == SampleOutcome.Pass ? sample.expectedOnPass : sample.expectedOnFail;
            Write(new JsonText()
                .Field("id", sample.id)
                .Field("state", "started")
                .Field("outcome", Name(outcome))
                .Raw("expect", ExpectedJson(expected)));
        }

        public static void Finished(SampleInfo sample, SampleOutcome expected, SampleOutcome? actual, List<string> recordProblems) =>
            Write(new JsonText()
                .Field("id", sample.id)
                .Field("state", "finished")
                .Field("outcome", Name(expected))
                .Field("result", actual.HasValue ? Name(actual.Value) : "timeout")
                .Field("records", recordProblems.Count == 0 ? "ok" : "mismatch")
                .Raw("problems", StringsJson(recordProblems)));

        public static void SuiteFinished(int runs, List<string> failures)
        {
            if (Enabled)
                Debug.Log("[suite] " + new JsonText()
                    .Field("state", "finished")
                    .Field("runs", runs)
                    .Field("passed", runs - failures.Count)
                    .Raw("failures", StringsJson(failures)));
        }

        private static string Name(SampleOutcome outcome) => outcome == SampleOutcome.Pass ? "pass" : "fail";

        private static string StringsJson(List<string> values) =>
            "[" + string.Join(",", values.Select(value => JsonText.Quoted(value))) + "]";

        private static string ExpectedJson(ExpectedRecord[] records)
        {
            var builder = new StringBuilder("[");
            for (var i = 0; records != null && i < records.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(new JsonText()
                    .Field("method", records[i].method)
                    .Field("name", records[i].name)
                    .Field("count", records[i].count)
                    .Field("atLeast", records[i].atLeast ? "true" : "false")
                    .Field("status", records[i].status));
            }
            return builder.Append(']').ToString();
        }

        private static void Write(JsonText json)
        {
            if (Enabled) Debug.Log("[sample] " + json);
        }
    }
}
