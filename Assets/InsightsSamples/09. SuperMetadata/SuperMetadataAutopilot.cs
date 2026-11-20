using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SuperMetadata
{
    /// <summary>
    /// Pass: switch to Leeds and French, then label a carton; its record carries all the context. Fail: unregister
    /// the site, then label a carton; its record has no site.
    /// </summary>
    public class SuperMetadataAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private LabelPrinter station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            if (outcome == SampleOutcome.Pass)
            {
                yield return driver.Click(station.SiteButton("leeds_dc"));
                yield return driver.Click(station.LanguageButton("fr"));
            }
            else
            {
                yield return driver.Click(station.UnregisterSiteButton);
            }
            yield return driver.Click(station.PrintButton);
        }
    }
}
