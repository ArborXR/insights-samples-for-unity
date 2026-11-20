using System;
using System.Collections.Generic;
using System.Linq;
using InsightsSamples.Common;

namespace InsightsSamples.Stream
{
    public enum StreamKind
    {
        Event,
        Assessment,
        Objective,
        Interaction,
        Critical,
        Telemetry,
        Log,
        Storage,
        Auth,
        Pairing,
        Identity,
        Module,
    }

    public enum StreamFilter
    {
        All,
        Events,
        Telemetry,
        Logs,
        System,
    }

    public enum StatusTone
    {
        Neutral,
        Good,
        Warning,
        Bad,
    }

    /// <summary>One row in the Insights Stream: a record the SDK created, or an SDK callback.</summary>
    public sealed class StreamRecord
    {
        public int Sequence;
        public DateTime Time;
        public StreamKind Kind;
        public string Method;
        public string Name;
        public string Call;
        public string Status;
        public StatusTone Tone;
        public string Feeds;
        public bool Automatic;

        /// <summary>The SDK's id for the record, so its send result can update the row. 0 for callbacks.</summary>
        public long RecordId;

        /// <summary>For telemetry, which collapses into one live row per name: samples so far, and when the first came.</summary>
        public int Samples = 1;
        public DateTime FirstTime;

        public readonly List<KeyValuePair<string, string>> CallerData = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> SdkData = new List<KeyValuePair<string, string>>();

        public StreamFilter Group => Kind switch
        {
            StreamKind.Telemetry => StreamFilter.Telemetry,
            StreamKind.Log => StreamFilter.Logs,
            StreamKind.Auth or StreamKind.Pairing or StreamKind.Identity or StreamKind.Storage or StreamKind.Module => StreamFilter.System,
            _ => StreamFilter.Events,
        };

        public string ToJson() => new JsonText()
            .Field("seq", Sequence)
            .Field("id", RecordId)
            .Field("kind", Kind.ToString().ToLowerInvariant())
            .Field("method", Method)
            .Field("name", Name)
            .Field("status", Status)
            .Field("automatic", Automatic ? "true" : "false")
            .Field("data", CallerData)
            .Field("sdk", Redacted(SdkData))
            .ToString();

        /// <summary>
        /// Log lines never carry user data beyond the ID. Super metadata can hold app-set user data, so
        /// user-looking keys the SDK merged in are masked.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, string>> Redacted(IEnumerable<KeyValuePair<string, string>> data) =>
            data.Select(pair => pair.Key.StartsWith("user", StringComparison.OrdinalIgnoreCase) || pair.Key == "email"
                ? new KeyValuePair<string, string>(pair.Key, "(redacted)")
                : pair);
    }
}
