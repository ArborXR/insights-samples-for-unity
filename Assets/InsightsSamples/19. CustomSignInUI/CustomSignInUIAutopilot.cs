using System.Collections;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.CustomSignInUI
{
    /// <summary>
    /// Asks the SDK to sign in (pairing first when the device isn't paired), then answers on the panel. Fail
    /// mistypes the code, sees the SDK's error, and closes the prompt. Pass needs a real passcode or PIN, which
    /// only a person has, so it waits for one. Without an App Token the SDK never asks, and the panel says so.
    /// </summary>
    public class CustomSignInUIAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private const float PromptSeconds = 15f;
        private const float PersonSeconds = 90f;

        [SerializeField] private AcmeSignInPanel station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            var finished = false;
            System.Action<SampleOutcome> handleFinished = _ => finished = true;
            SampleEvents.Finished += handleFinished;
            try
            {
                yield return driver.Click(station.PairButton.isActiveAndEnabled ? station.PairButton : station.SignInButton);
                for (float waited = 0; !finished && station.Asking == AcmeSignInPanel.Entry.None && waited < PromptSeconds; waited += Time.unscaledDeltaTime)
                    yield return null;
                if (finished || station.Asking == AcmeSignInPanel.Entry.None) yield break;

                if (outcome == SampleOutcome.Pass)
                {
                    for (float waited = 0; !finished && waited < PersonSeconds; waited += Time.unscaledDeltaTime) yield return null;
                    yield break;
                }

                // Too short to be a passcode, so the SDK rejects it without sending it, and asks again.
                var code = station.Asking == AcmeSignInPanel.Entry.Pin || station.Asking == AcmeSignInPanel.Entry.Passcode;
                if (code)
                    foreach (var digit in "123") yield return driver.Click(station.Key(digit));
                else
                    station.Type("not_a_user");
                yield return driver.Click(station.SubmitButton);

                for (float waited = 0; !finished && station.ShownError == null && waited < PromptSeconds; waited += Time.unscaledDeltaTime)
                    yield return null;
                if (station.NotNowButton.isActiveAndEnabled) yield return driver.Click(station.NotNowButton);
            }
            finally
            {
                SampleEvents.Finished -= handleFinished;
            }
        }
    }
}
