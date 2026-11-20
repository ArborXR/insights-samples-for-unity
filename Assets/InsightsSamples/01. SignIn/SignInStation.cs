using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.SignIn
{
    /// <summary>
    /// The front-desk console: shows the sign-in state, with a button to sign in again.
    /// </summary>
    public class SignInStation : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject counterModel;
        [SerializeField] private GameObject monitorModel;

        private TextMeshPro _status;
        private int _authResultsAtClick = -1;

        public StationInteractable SignInButton { get; private set; }

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

            // The button lies on the counter's ledge with a model, and stands in front of the desk without one.
            var ledge = counterModel != null && monitorModel != null;
            SignInButton = StationBuilder.Button(transform, "Sign in again", new Vector3(0, ledge ? 1.13f : 0.98f, 0.0f), new Vector2(0.56f, 0.16f), material, UiKit.Purple, 0.42f);
            if (ledge) SignInButton.transform.localRotation = Quaternion.Euler(60f, 0, 0);
        }

        /// <summary>A visitor sign-in screen on the reception counter.</summary>
        private void BuildReception()
        {
            StationBuilder.Model(transform, "Counter Model", counterModel, new Vector3(0, 0, 0.25f), Vector3.one);
            // A large display, so the sign-in state reads from where a visitor stands.
            var monitor = StationBuilder.Model(transform, "Monitor Model", monitorModel, new Vector3(0, 0.765f, 0.45f), Vector3.one * 1.25f).transform;
            StationBuilder.Tint(monitor.Find("Monitor Screen").gameObject, UiKit.Navy);
            _status = StationBuilder.Text(transform, "", new Vector3(0, 1.605f, 0.42f), 0.45f, Color.white, 1.1f, 0.62f, TextAlignmentOptions.Left);
        }

        private void Start()
        {
            SignInButton.Activated += _ =>
            {
                _authResultsAtClick = SignInSample.Instance.AuthResults;
                SignInSample.Instance.SignInAgain();
            };

            SignInSample.Instance.Changed += Refresh;
            ShellSignals.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (SignInSample.Instance != null) SignInSample.Instance.Changed -= Refresh;
            ShellSignals.Changed -= Refresh;
        }

        private void Refresh()
        {
            var sample = SignInSample.Instance;
            var signIn = sample.SignedIn ? "<color=#05DA98>Signed in</color>"
                : sample.LastError != null ? $"<color=#F5B841>Not signed in</color>\n<size=70%>{sample.LastError}</size>"
                : "Signing in…";
            // Where the SDK got this device's organization. The shell hears about pairing from sample 18.
            var organization = sample.SignedIn
                ? ShellSignals.Paired ? "A pairing (sample 18)" : "ArborXR device management\nor an org token"
                : ShellSignals.Connection == ConnectionState.NotPaired
                    ? "None yet\n<size=70%>ArborXR doesn't manage this device: pair it in sample 18.</size>"
                    : "—";
            _status.text = $"<size=75%><color=#FFFFFFAA>SIGN-IN</color></size>\n{signIn}\n" +
                           $"<size=70%><color=#FFFFFFAA>OnAuthCompleted calls: {sample.AuthResults}</color></size>\n\n" +
                           $"<size=75%><color=#FFFFFFAA>ORGANIZATION FROM</color></size>\n{organization}";

            if (_authResultsAtClick >= 0 && sample.AuthResults > _authResultsAtClick)
            {
                _authResultsAtClick = -1;
                SampleEvents.ReportFinished(sample.SignedIn ? SampleOutcome.Pass : SampleOutcome.Fail);
            }
        }
    }
}
