using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Storage
{
    /// <summary>
    /// Sample 11: save a learner's progress through a long course with Insights storage, and resume it next time,
    /// on this headset or another one.
    /// </summary>
    public class StorageSample : MonoBehaviour, ISampleLifecycle
    {
        private const string ProgressEntry = "course_progress";
        private const string HistoryEntry = "attempt_history";

        [SerializeField] private CourseConsole station;

        private void OnEnable()
        {
            station.ChapterCompleted += HandleChapterCompleted;
            station.ResetRequested += HandleResetRequested;
            Abxr.OnAuthCompleted += HandleAuthCompleted;
        }

        private void OnDisable()
        {
            station.ChapterCompleted -= HandleChapterCompleted;
            station.ResetRequested -= HandleResetRequested;
            Abxr.OnAuthCompleted -= HandleAuthCompleted;
        }

        public void OnSampleEnter() => LoadProgress();

        // Storage needs a signed-in session. Right after launch, sign-in may still be running, so read again once it
        // succeeds.
        private void HandleAuthCompleted(bool success, string errorMessage)
        {
            if (success) LoadProgress();
        }

        // User scope follows the learner to any headset; Device scope stays with this headset or browser. An app
        // whose sign-in identifies nobody (GetUserId() is null) has only the device, so it saves there instead.
        private Abxr.StorageScope ChooseScope()
        {
            var scope = Abxr.GetUserId() != null ? Abxr.StorageScope.User : Abxr.StorageScope.Device;
            // GetUserData() is null until sign-in succeeds, and storage keeps and reads nothing before then.
            station.ShowStorage(Abxr.GetUserData() != null, scope == Abxr.StorageScope.User);
            return scope;
        }

        private void LoadProgress()
        {
            // Reads are asynchronous: run them as coroutines and use the result in the callback.
            var scope = ChooseScope();
            StartCoroutine(Abxr.StorageGetEntry(ProgressEntry, scope, HandleProgressLoaded));

            StartCoroutine(Abxr.StorageGetEntry(HistoryEntry, scope, HandleHistoryLoaded));
        }

        private void HandleProgressLoaded(List<Dictionary<string, string>> entries)
        {
            // null when storage couldn't be read (not signed in, or the read failed), empty when nothing is saved.
            var saved = entries?.LastOrDefault();
            var chapter = saved != null && saved.TryGetValue("chapter", out var value) && int.TryParse(value, out var number) ? number : 0;
            station.Resume(chapter, entries != null);
        }

        private void HandleHistoryLoaded(List<Dictionary<string, string>> entries) =>
            station.ShowHistory(entries?.Count ?? 0);

        private void HandleChapterCompleted(int chapter)
        {
            var scope = ChooseScope();
            var completedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var progress = new Abxr.Dict { ["chapter"] = chapter.ToString(CultureInfo.InvariantCulture), ["completed_at"] = completedAt };
            // KeepLatest replaces what was saved before: the learner's place in the course. Saves are sent with the
            // next batch (Send Next Batch Wait Seconds, 30 by default), so a read before then returns the last
            // value that was sent.
            Abxr.StorageSetEntry(ProgressEntry, progress, scope, Abxr.StoragePolicy.KeepLatest);

            var attempt = new Abxr.Dict { ["chapter"] = chapter.ToString(CultureInfo.InvariantCulture), ["completed_at"] = completedAt };
            // AppendHistory adds to what was saved, keeping a record of every completed chapter.
            Abxr.StorageSetEntry(HistoryEntry, attempt, scope, Abxr.StoragePolicy.AppendHistory);
        }

        private void HandleResetRequested()
        {
            // Removes the saved place in the course; the history stays. A remove is sent at once, but saves wait for
            // the next batch, so a save still waiting to be sent reaches Insights after the remove and restores it.
            Abxr.StorageRemoveEntry(ProgressEntry, ChooseScope());
        }

        public void OnSampleExit()
        {
            // Nothing to close: each chapter was saved as it was completed.
        }
    }
}
