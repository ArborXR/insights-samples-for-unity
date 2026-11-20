using System;
using System.Collections.Generic;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Start-up parameters, read once: the WebGL page's query string, Android intent extras, or
    /// "-key value" command-line arguments in the Editor and desktop builds.
    /// </summary>
    public static class DeepLinks
    {
        public const string Sample = "sample";
        public const string Autopilot = "autopilot";
        public const string Suite = "suite";
        public const string Pace = "pace";
        public const string StreamLog = "stream_log";
        public const string Module = "module";
        public const string Pairing = "pairing";
        public const string Sound = "sound";

        private static readonly string[] Keys = { Sample, Autopilot, Suite, Pace, StreamLog, Module, Pairing, Sound };
        private static Dictionary<string, string> _values;

        /// <summary>Unattended runs (pairing=skip) keep the pairing prompt closed: it would wait for a person.</summary>
        public static bool KeepPairingPromptClosed => Get(Pairing) == "skip";

        public static string Get(string key)
        {
            _values ??= Read();
            return _values.TryGetValue(key, out var value) ? value : null;
        }

        private static Dictionary<string, string> Read()
        {
            var values = new Dictionary<string, string>();
#if UNITY_WEBGL && !UNITY_EDITOR
            ReadQuery(Application.absoluteURL, values);
#elif UNITY_ANDROID && !UNITY_EDITOR
            ReadIntentExtras(values);
#else
            ReadCommandLine(values);
#endif
            return values;
        }

        private static void ReadQuery(string url, Dictionary<string, string> values)
        {
            var start = url.IndexOf('?');
            if (start < 0) return;

            var query = url.Substring(start + 1);
            var fragment = query.IndexOf('#');
            if (fragment >= 0) query = query.Substring(0, fragment);

            foreach (var pair in query.Split('&'))
            {
                var equals = pair.IndexOf('=');
                if (equals <= 0) continue;
                values[Uri.UnescapeDataString(pair.Substring(0, equals))] = Uri.UnescapeDataString(pair.Substring(equals + 1));
            }
        }

        private static void ReadIntentExtras(Dictionary<string, string> values)
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent = activity.Call<AndroidJavaObject>("getIntent");
            foreach (var key in Keys)
            {
                var value = intent.Call<string>("getStringExtra", key);
                if (!string.IsNullOrEmpty(value)) values[key] = value;
            }
        }

        private static void ReadCommandLine(Dictionary<string, string> values)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                var key = args[i].TrimStart('-');
                if (args[i].StartsWith("-") && Array.IndexOf(Keys, key) >= 0) values[key] = args[i + 1];
            }
        }
    }
}
