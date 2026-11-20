using System.Collections;
using System.Collections.Generic;
using System.Linq;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.ModuleTargets
{
    /// <summary>
    /// Sample 16: an app with several modules that an LMS can assign. Every route into a module (its button,
    /// an LMS target, or a deep link) goes through one entry point, and each module is its own assessment.
    /// The LMS can send a target as soon as sign-in finishes, so the shell creates this at launch.
    /// </summary>
    public class ModuleTargetsSample : MonoBehaviour
    {
        private const string SampleId = "16-module-targets";

        // The modules this app has, in its own order. Module IDs double as assessment names.
        private static readonly string[] AppModules = { "preshift_tires", "preshift_forks", "preshift_controls" };

        private ForkliftCheck _station;
        private string _pending;

        public static ModuleTargetsSample Instance { get; private set; }

        public string Current { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Subscribe at launch, in something that lives as long as the app: when sign-in assigns modules, the
            // SDK sends the first target straight away.
            Abxr.OnModuleTarget += HandleModuleTarget;
            Abxr.OnAllModulesCompleted += HandleAllModulesCompleted;
        }

        private void Start()
        {
            // The app's own deep link to a module uses the same entry point as the LMS.
            var linked = DeepLinks.Get(DeepLinks.Module);
            if (!string.IsNullOrEmpty(linked)) StartModule(linked);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Abxr.OnModuleTarget -= HandleModuleTarget;
            Abxr.OnAllModulesCompleted -= HandleAllModulesCompleted;
            Instance = null;
        }

        /// <summary>The modules in order: the LMS's when it assigned a sequence, the app's otherwise.</summary>
        public IReadOnlyList<string> ModuleOrder()
        {
            var assigned = Abxr.GetModuleList();
            return assigned != null && assigned.Count > 0 ? assigned.Select(module => module.Target).ToList() : AppModules;
        }

        private void HandleModuleTarget(string target) => StartModule(target);

        /// <summary>The single entry point into a module: module buttons, LMS targets, and deep links all come here.</summary>
        public void StartModule(string moduleId)
        {
            // The SDK sends the current target again after every sign-in, including a background re-auth: carry on.
            if (moduleId == Current) return;

            // Check against the modules the app has, not the LMS's list: an LMS can assign one this app doesn't have.
            if (!AppModules.Contains(moduleId))
            {
                var details = new Abxr.Dict { ["target"] = moduleId };
                // An unknown target is a configuration problem for a developer: log it, then show the module list.
                Abxr.LogWarn("Unknown module target", details);
                _pending = null;
                ShowSample();
                return;
            }

            _pending = moduleId;
            if (_station != null) BeginPending();
            else ShowSample();
        }

        /// <summary>The module's station, in the sample's scene. A target that arrived first starts now.</summary>
        public void Attach(ForkliftCheck station)
        {
            _station = station;
            if (_pending != null) BeginPending();
        }

        public void Detach(ForkliftCheck station)
        {
            if (_station != station) return;
            _station = null;
            // Leaving mid-module: close it as Incomplete, as in sample 02. In an LMS sequence the SDK then sends the
            // next target, which waits until the learner comes back.
            if (Current != null) Complete(0, Abxr.EventStatus.Incomplete);
        }

        public void CompleteModule(bool passed) => Complete(passed ? 100 : 0, passed ? Abxr.EventStatus.Pass : Abxr.EventStatus.Fail);

        private void BeginPending()
        {
            var next = _pending;
            _pending = null;
            // Switching modules closes the running one. In an LMS sequence that sends the LMS's next target, which
            // has already started by the time Complete returns.
            if (Current != null) Complete(0, Abxr.EventStatus.Incomplete);
            if (Current != null) return;

            Current = next;
            // One assessment per module, named by the module ID. Never put module, moduleName, moduleId, or
            // moduleOrder in metadata: the SDK sets them (from the LMS's current module in a sequence, otherwise
            // from this name), and metadata you pass would override them.
            Abxr.EventAssessmentStart(Current);
            _station.Begin(Current);
        }

        private void Complete(int score, Abxr.EventStatus status)
        {
            var module = Current;
            Current = null;
            // In an LMS sequence, completing an assessment is all it takes to move on: before this call returns,
            // the SDK calls OnModuleTarget with the next module, or OnAllModulesCompleted after the last. That's
            // why Current is cleared first.
            Abxr.EventAssessmentComplete(module, score, status);
        }

        private void HandleAllModulesCompleted()
        {
            if (_station != null) _station.ShowAllDone();
            // The assigned sequence is done: a fresh session for whoever's next. End this one first so the last
            // module's result is sent (sample 15): StartNewSession alone discards anything not yet sent.
            Abxr.EndSession();
            ShellSignals.ReportSessionEnded();
            Abxr.StartNewSession();
        }

        private void ShowSample()
        {
            // Already here, or leaving this sample (its station is going away): the target waits for the station.
            var shell = SampleShell.Instance;
            if (shell == null || shell.Current?.id == SampleId) return;
            // OnModuleTarget can arrive during another sample's EventAssessmentComplete: let that call return before
            // leaving the sample it came from.
            StartCoroutine(OpenSampleNextFrame(shell));
        }

        private static IEnumerator OpenSampleNextFrame(SampleShell shell)
        {
            yield return null;
            shell.Open(SampleId);
        }
    }
}
