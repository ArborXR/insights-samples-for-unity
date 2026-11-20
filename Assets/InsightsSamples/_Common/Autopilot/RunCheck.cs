using System;
using System.Collections.Generic;
using System.Linq;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Checks an autopilot run against the records its SampleInfo expects. The Insights Stream reports every
    /// record it shows; the shell compares them with the expected list when the run finishes.
    /// </summary>
    public static class RunCheck
    {
        private static readonly List<(string method, string name, List<KeyValuePair<string, string>> data)> Observed =
            new List<(string, string, List<KeyValuePair<string, string>>)>();
        private static bool _active;

        public static void Begin()
        {
            Observed.Clear();
            _active = true;
        }

        /// <summary>Stops observing without checking, for a run that was interrupted.</summary>
        public static void Cancel()
        {
            Observed.Clear();
            _active = false;
        }

        /// <param name="data">The record's fields: what the app passed and what the SDK added.</param>
        public static void Observe(string method, string name, IEnumerable<KeyValuePair<string, string>> data)
        {
            if (_active) Observed.Add((method, name, data.ToList()));
        }

        /// <summary>Stops observing and returns one line per expectation that wasn't met.</summary>
        public static List<string> End(ExpectedRecord[] expected)
        {
            _active = false;
            var problems = new List<string>();
            foreach (var record in expected ?? Array.Empty<ExpectedRecord>())
            {
                var seen = Observed.Count(o => o.method == record.method &&
                                               (string.IsNullOrEmpty(record.name) || o.name == record.name) &&
                                               Matches(record.status, o.data));
                if (record.atLeast ? seen < record.count : seen != record.count)
                    problems.Add($"{Describe(record)}: expected {(record.atLeast ? "at least " : "")}{record.count}, saw {seen}");
            }
            return problems;
        }

        /// <summary>
        /// An expected status matches the record's status or result ("Pass", "Correct"), or a field when written
        /// as key=value ("mispicks=0"). "key=" with no value matches a record without that field.
        /// </summary>
        private static bool Matches(string expected, List<KeyValuePair<string, string>> data)
        {
            if (string.IsNullOrEmpty(expected)) return true;
            var separator = expected.IndexOf('=');
            if (separator <= 0) return data.Any(pair => (pair.Key == "status" || pair.Key == "result") && Same(pair.Value, expected));

            var key = expected.Substring(0, separator);
            var value = expected.Substring(separator + 1);
            return value.Length == 0
                ? !data.Any(pair => pair.Key == key && !string.IsNullOrEmpty(pair.Value))
                : data.Any(pair => pair.Key == key && Same(pair.Value, value));
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static string Describe(ExpectedRecord record) =>
            string.Join(" ", new[] { record.method, record.name, record.status }.Where(part => !string.IsNullOrEmpty(part)));
    }
}
