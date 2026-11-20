using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.UserData
{
    /// <summary>
    /// Pass: Sam taps their employee badge, then checks the user data. Fail: a visitor badge, which sets no
    /// user data.
    /// </summary>
    public class UserDataAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private BadgeReader station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            if (outcome == SampleOutcome.Fail)
            {
                yield return driver.Click(station.Badge("V-311"));
                yield break;
            }

            yield return driver.Click(station.Badge("E-1042"));
            yield return driver.Click(station.CheckUserDataButton);
        }
    }
}
