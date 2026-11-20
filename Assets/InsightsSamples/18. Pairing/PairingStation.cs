using InsightsSamples.Common;
using InsightsSamples.SignIn;
using TMPro;
using UnityEngine;

namespace InsightsSamples.Pairing
{
    /// <summary>
    /// The front-desk console of a device ArborXR doesn't manage: shows pairing and sign-in state, with buttons to
    /// pair, clear the pairing, and sign in again (sample 01).
    /// </summary>
    public class PairingStation : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject counterModel;
        [SerializeField] private GameObject monitorModel;

        private TextMeshPro _status;
        private int _authResultsAtClick = -1;
        private string _pairingStatus;
        private string _managedNote;

        public StationInteractable PairButton { get; private set; }
        public StationInteractable ClearButton { get; private set; }
        public StationInteractable SignInButton { get; private set; }
        public bool Paired => PairingSample.Instance.Paired;
        public bool Managed => PairingSample.Instance.Managed;

        private void Awake()
        {
            if (counterModel != null && monitorModel != null)
                BuildReception();
            else
            {
                StationBuilder.Tint(StationBuilder.Block(transform, "Desk", new Vector3(0, 0.45f, 0.25f), new Vector3(1.9f, 0.9f, 0.7f), material), new Color(0.89f, 0.9f, 0.93f));
                StationBuilder.Tint(StationBuilder.Block(transform, "Screen", new Vector3(0, 1.42f, 0.5f), new Vector3(1.6f, 0.9f, 0.05f), material), UiKit.Navy);
                _status = StationBuilder.Text(transform, "", new Vector3(0, 1.42f, 0.47f), 0.55f, Color.white, 1.4f, 0.8f, TextAlignmentOptions.Left);
            }

            // The buttons lie on the counter's ledge with a model, and stand in front of the desk without one.
            var ledge = counterModel != null && monitorModel != null;
            var y = ledge ? 1.13f : 0.98f;
            PairButton = Button("Pair this device", -0.62f, y, UiKit.Purple, ledge);
            ClearButton = Button("Clear pairing", 0, y, UiKit.NavyLight, ledge);
            SignInButton = Button("Sign in again", 0.62f, y, UiKit.Purple, ledge);
        }

        /// <summary>A setup screen on the reception counter.</summary>
        private void BuildReception()
        {
            StationBuilder.Model(transform, "Counter Model", counterModel, new Vector3(0, 0, 0.25f), Vector3.one);
            // A large display, so the pairing state reads from where a visitor stands.
            var monitor = StationBuilder.Model(transform, "Monitor Model", monitorModel, new Vector3(0, 0.765f, 0.45f), Vector3.one * 1.25f).transform;
            StationBuilder.Tint(monitor.Find("Monitor Screen").gameObject, UiKit.Navy);
            _status = StationBuilder.Text(transform, "", new Vector3(0, 1.605f, 0.42f), 0.45f, Color.white, 1.1f, 0.62f, TextAlignmentOptions.Left);
        }

        private StationInteractable Button(string label, float x, float y, Color color, bool onLedge)
        {
            var button = StationBuilder.Button(transform, label, new Vector3(x, y, 0.0f), new Vector2(0.56f, 0.16f), material, color, 0.42f);
            if (onLedge) button.transform.localRotation = Quaternion.Euler(60f, 0, 0);
            return button;
        }

        private void Start()
        {
            PairButton.Activated += _ => PairingSample.Instance.OfferPairing();
            ClearButton.Activated += _ => PairingSample.Instance.ClearPairing();
            SignInButton.Activated += _ =>
            {
                _authResultsAtClick = SignInSample.Instance.AuthResults;
                SignInSample.Instance.SignInAgain();
            };

            PairingSample.Instance.Changed += Refresh;
            SignInSample.Instance.Changed += Refresh;
            ShellSignals.Changed += Refresh;
            Refresh();
        }

        // Prompting and Redeeming raise no SDK event, so watch for them.
        private void Update()
        {
            if (PairingSample.Instance.PairingStatus != _pairingStatus) Refresh();
        }

        private void OnDestroy()
        {
            if (PairingSample.Instance != null) PairingSample.Instance.Changed -= Refresh;
            if (SignInSample.Instance != null) SignInSample.Instance.Changed -= Refresh;
            ShellSignals.Changed -= Refresh;
        }

        /// <summary>A fail run on a managed device: it never pairs, so there's no passcode to get wrong.</summary>
        public void ShowNothingToFail()
        {
            _managedNote = "A managed device never pairs, so there's nothing to fail here.";
            Refresh();
        }

        private void Refresh()
        {
            var pairing = PairingSample.Instance;
            var signedIn = SignInSample.Instance;
            var signIn = signedIn.SignedIn ? "<color=#05DA98>Signed in</color>"
                : signedIn.LastError != null ? $"<color=#F5B841>Not signed in</color>\n<size=70%>{signedIn.LastError}</size>"
                : "Signing in…";
            var status = pairing.Managed
                ? "Managed\n<size=70%>ArborXR or an org token names the organization, so this device doesn't pair." +
                  (ShellSignals.PairingHint != null ? $" {ShellSignals.PairingHint}" : "") +
                  (_managedNote != null ? $"\n<color=#F5B841>{_managedNote}</color>" : "") + "</size>"
                : pairing.PairingStatus;
            var device = string.IsNullOrEmpty(pairing.DeviceName) ? "—" : pairing.DeviceName;
            _status.text = $"<size=75%><color=#FFFFFFAA>PAIRING</color></size>\n{status}\n\n" +
                           $"<size=75%><color=#FFFFFFAA>PAIRED DEVICE NAME</color></size>\n{device}\n\n" +
                           $"<size=75%><color=#FFFFFFAA>SIGN-IN</color></size>\n{signIn}";

            PairButton.Interactable = pairing.CanPair;
            ClearButton.Interactable = pairing.Paired;

            if (_authResultsAtClick >= 0 && signedIn.AuthResults > _authResultsAtClick)
            {
                _authResultsAtClick = -1;
                SampleEvents.ReportFinished(signedIn.SignedIn ? SampleOutcome.Pass : SampleOutcome.Fail);
            }

            // A rejected passcode (the prompt stays open and asks again), or closing the prompt without pairing
            // ("Not now"), is a fail.
            var rejected = _pairingStatus == "Redeeming" && pairing.PairingStatus == "Prompting" && pairing.PasscodeRejected;
            var dismissed = _pairingStatus is "Prompting" or "Redeeming" && pairing.PairingStatus == "Unpaired";
            _pairingStatus = pairing.PairingStatus;
            if (rejected || dismissed) SampleEvents.ReportFinished(SampleOutcome.Fail);
        }
    }
}
