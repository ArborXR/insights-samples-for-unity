using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SuperMetadata
{
    /// <summary>
    /// Sample 09: register context once as super metadata, and the SDK adds it to every later event, log, and
    /// telemetry entry. Individual calls stay short and can't forget it.
    /// </summary>
    public class SuperMetadataSample : MonoBehaviour, ISampleLifecycle
    {
        // Stable snake_case keys. module, moduleName, moduleId, and moduleOrder are reserved: the SDK sets them
        // (from an LMS's current module, or from the assessment name), and Register refuses them.
        private const string SiteKey = "site";
        private const string FirstSiteKey = "first_site_seen";
        private const string LanguageKey = "language_code";
        private const string LabelPrinted = "label_printed";

        [SerializeField] private LabelPrinter station;

        private void OnEnable()
        {
            station.SiteChosen += HandleSiteChosen;
            station.LanguageChosen += HandleLanguageChosen;
            station.PrintRequested += HandlePrintRequested;
            station.UnregisterSiteRequested += HandleUnregisterSiteRequested;
            station.ResetRequested += HandleResetRequested;
        }

        private void OnDisable()
        {
            station.SiteChosen -= HandleSiteChosen;
            station.LanguageChosen -= HandleLanguageChosen;
            station.PrintRequested -= HandlePrintRequested;
            station.UnregisterSiteRequested -= HandleUnregisterSiteRequested;
            station.ResetRequested -= HandleResetRequested;
        }

        public void OnSampleEnter()
        {
            // Register context when the scenario starts, rather than adding it to every event.
            HandleSiteChosen(station.Site);
            HandleLanguageChosen(station.Language);
        }

        private void HandleSiteChosen(string site)
        {
            Abxr.Register(SiteKey, site);

            // Register replaces a value. RegisterOnce keeps the first one, so first_site_seen stays put when
            // the learner switches sites.
            Abxr.RegisterOnce(FirstSiteKey, site);
            ShowContext();
        }

        private void HandleLanguageChosen(string languageCode)
        {
            Abxr.Register(LanguageKey, languageCode);
            ShowContext();
        }

        private void HandlePrintRequested()
        {
            // No metadata here: the SDK merges the registered context into the event.
            Abxr.Event(LabelPrinted);

            // The label prints from the station's own site and language, not from super metadata: that describes
            // records for analytics and isn't app state. Reading it here only checks whether this record carried a
            // site that reports can split on, to show what a missing one costs.
            var context = Abxr.GetSuperMetaData();
            station.ApplyLabel(context != null && context.ContainsKey(SiteKey));
        }

        private void HandleUnregisterSiteRequested()
        {
            Abxr.Unregister(SiteKey);
            ShowContext();
        }

        private void HandleResetRequested()
        {
            // Reset clears all super metadata, including the module the SDK set from the last assessment. Use
            // it when a new scenario or session begins.
            Abxr.Reset();
            ShowContext();
        }

        public void OnSampleExit()
        {
            // Context belongs to the scenario that set it. Remove it on the way out, or later events elsewhere
            // in the app would carry it.
            foreach (var key in new[] { SiteKey, FirstSiteKey, LanguageKey })
            {
                Abxr.Unregister(key);
            }
        }

        // GetSuperMetaData returns a copy of everything registered, or null when nothing is.
        private void ShowContext() => station.ShowContext(Abxr.GetSuperMetaData());
    }
}
