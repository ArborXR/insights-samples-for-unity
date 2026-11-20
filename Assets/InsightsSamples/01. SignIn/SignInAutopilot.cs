using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.SignIn
{
    /// <summary>
    /// Signs in again, and the device decides the result: with an organization (from ArborXR device management, an
    /// org token, or a pairing) it passes, and without one it fails. Neither can be forced from here, so a fail run
    /// on a device that signs in reports a pass.
    /// </summary>
    public class SignInAutopilot : MonoBehaviour, ISampleAutopilot
    {
        [SerializeField] private SignInStation station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            yield return driver.Click(station.SignInButton);
        }
    }
}
