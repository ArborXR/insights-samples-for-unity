using System;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Everything the shell knows about one sample: the browser card, header, intro, Insights Stream
    /// dashboard notes, deep link ID, and the records an autopilot run should produce.
    /// </summary>
    [CreateAssetMenu(menuName = "Insights Samples/Sample Info")]
    public class SampleInfo : ScriptableObject
    {
        [Tooltip("Stable ID used in deep links and logs, e.g. 02-assessments.")]
        public string id;
        public int number;
        public string title;
        public string series;
        [Tooltip("The cover's icon: a name from UiIcons (ArtSource/Icons/lucide).")]
        public string icon;
        public string scenario;
        [Tooltip("The enterprise situation the sample models, for its intro: who the learner is and what's at stake.")]
        [TextArea(3, 6)] public string realWorld;
        [Tooltip("The SDK calls this sample teaches, for its browser card.")]
        public string teaches;
        public string sceneName;

        [Tooltip("Created at launch for samples whose feature applies to the whole app, such as sign-in.")]
        public GameObject appWidePrefab;

        [TextArea(2, 4)] public string whatThisShows;
        [TextArea(1, 3)] public string[] tryThis;
        [TextArea(1, 3)] public string[] watchFor;
        public string lessonScript;
        public int lessonScriptLine;

        public DashboardNote[] dashboardNotes;
        public ExpectedRecord[] expectedOnPass;
        public ExpectedRecord[] expectedOnFail;

        public string Label => $"{number:00} · {title}";

        public string DashboardNoteFor(string method, string name)
        {
            if (dashboardNotes == null || string.IsNullOrEmpty(method)) return null;
            foreach (var note in dashboardNotes)
            {
                if (string.IsNullOrEmpty(note.methodPrefix) || !method.StartsWith(note.methodPrefix, StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(note.nameSuffix) && (name == null || !name.EndsWith(note.nameSuffix, StringComparison.Ordinal))) continue;
                return note.note;
            }
            return null;
        }
    }

    [Serializable]
    public struct DashboardNote
    {
        public string methodPrefix;
        [Tooltip("Optional: only records whose name ends with this, so the SDK's own events (scene changes, DEFAULT) and telemetry (battery, memory) don't get the note.")]
        public string nameSuffix;
        public string note;
    }

    [Serializable]
    public struct ExpectedRecord
    {
        public string method;
        public string name;
        public int count;
        [Tooltip("Count is a minimum, for records sampled over time such as telemetry.")]
        public bool atLeast;
        public string status;
    }
}
