using System;
using System.Collections;
using System.Linq;
using AbxrLib.Runtime.UI.Keyboard;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.Pairing
{
    /// <summary>
    /// Pass: signs in again. Unpaired, that needs a pairing passcode, which only a person has, so it opens the
    /// pairing prompt and waits for one first. Fail: clears any pairing and enters a wrong passcode on the SDK's prompt.
    /// The Portal rejects it, and the prompt stays open for a real one. A managed device never pairs, so its fail
    /// run has nothing to do, and the station says so.
    /// </summary>
    public class PairingAutopilot : MonoBehaviour, ISampleAutopilot
    {
        private const float PersonSeconds = 300f;
        private const float PromptSeconds = 15f;
        private const string WrongPasscode = "123456";

        [SerializeField] private PairingStation station;

        public IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver)
        {
            if (outcome == SampleOutcome.Fail)
            {
                if (station.Managed)
                {
                    // The button is off, so the click does nothing; reaching for it moves the intro off the note.
                    yield return driver.Click(station.PairButton);
                    station.ShowNothingToFail();
                }
                else yield return EnterWrongPasscode(driver);
                yield break;
            }

            if (!station.Paired && !station.Managed)
            {
                if (station.PairButton.Interactable) yield return driver.Click(station.PairButton);
                yield return WaitFor(() => station.Paired, PersonSeconds);
                if (!station.Paired) yield break;
            }
            yield return driver.Click(station.SignInButton);
        }

        private IEnumerator EnterWrongPasscode(AutopilotDriver driver)
        {
            // Unpairing makes the sample offer pairing again; unpaired after Not now, ask for it.
            if (station.Paired)
            {
                yield return driver.Click(station.ClearButton);
                yield return WaitFor(() => !station.Paired, PromptSeconds);
            }
            if (station.PairButton.Interactable) yield return driver.Click(station.PairButton);

            KeyboardManager pad = null;
            yield return WaitFor(() => (pad = PinPad()) != null, PromptSeconds);
            if (pad == null) yield break;

            if (pad.pairingGate != null && pad.pairingGate.activeInHierarchy)
                yield return Press(driver, pad.gateConnectButton.gameObject);
            foreach (var digit in WrongPasscode)
                yield return Press(driver, Key(pad, digit.ToString()));
            // The SDK sends it to the Portal, shows the error, and asks again; the station reports the fail.
            yield return Press(driver, pad.submitButton.gameObject);
        }

        /// <summary>Presses once the prompt accepts input: it ignores presses briefly after each step appears.</summary>
        private static IEnumerator Press(AutopilotDriver driver, GameObject control)
        {
            yield return WaitFor(() => !KeyboardHandler.IsInputGuarded, PromptSeconds);
            yield return driver.Press(control);
        }

        private static KeyboardManager PinPad() =>
            FindObjectsByType<KeyboardManager>(FindObjectsSortMode.None).FirstOrDefault(manager => manager.isActiveAndEnabled && manager.submitButton != null && manager.gateConnectButton != null);

        private static GameObject Key(KeyboardManager pad, string digit) =>
            pad.GetComponentsInChildren<KeyboardKey>().FirstOrDefault(key => key.keyLabel != null && key.keyLabel.text == digit)?.gameObject;

        private static IEnumerator WaitFor(Func<bool> done, float seconds)
        {
            for (float waited = 0; !done() && waited < seconds; waited += Time.unscaledDeltaTime) yield return null;
        }
    }
}
