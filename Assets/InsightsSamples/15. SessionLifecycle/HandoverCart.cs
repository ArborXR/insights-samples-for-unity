using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.SessionLifecycle
{
    /// <summary>
    /// A training-room cart where one trainee finishes a short drill and hands the headset to the next. Handing
    /// over mid-drill leaves the drill unfinished, and the SDK closes it.
    /// </summary>
    public class HandoverCart : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject cartModel;
        [SerializeField] private GameObject headsetModel;

        private TextMeshPro _screen;
        private string _message = "Start the drill, finish it, then hand the headset over.";
        private bool _drillStarted;
        private bool _listening;

        public StationInteractable DrillButton { get; private set; }
        public StationInteractable HandOverButton { get; private set; }
        public StationInteractable EndSessionButton { get; private set; }

        private static SessionLifecycleSample Lesson => SessionLifecycleSample.Instance;

        private void Awake()
        {
            BuildCart(StationBuilder.Group(transform, "Cart", new Vector3(0.75f, 0, 0.55f)));
            BuildPanel(StationBuilder.Group(transform, "Panel", new Vector3(-0.35f, 1.5f, 0.75f)));
        }

        private void OnEnable() => Listen();

        // Pressing Play on this scene creates the shell, and the lesson with it, after OnEnable: try again here.
        private void Start() => Listen();

        private void Listen()
        {
            if (!_listening && Lesson != null)
            {
                Lesson.Changed += Refresh;
                _listening = true;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (_listening && Lesson != null) Lesson.Changed -= Refresh;
            _listening = false;
        }

        private void OnDestroy()
        {
            if (Lesson != null) Lesson.CloseDrill();
        }

        private void ToggleDrill()
        {
            if (Lesson.DrillOpen)
            {
                Lesson.FinishDrill();
                _message = "Drill finished.";
            }
            else
            {
                Lesson.StartDrill();
                _drillStarted = true;
                _message = "Drill in progress: check the strap, the lenses, and the battery.";
            }
            Refresh();
        }

        private void HandOver() => EndWith(Lesson.HandToNextTrainee, "Handed to the next trainee: a new session is starting.");

        private void EndSession() => EndWith(Lesson.EndSession, "Session ended. The headset is signed out.");

        private void EndWith(System.Action end, string message)
        {
            var unfinished = Lesson.DrillOpen;
            end();
            _message = unfinished ? $"{message}\n<color=#F5B841><b>The drill wasn't finished, so the SDK closed it as Incomplete.</b></color>" : message;
            Refresh();
            if (_drillStarted) SampleEvents.ReportFinished(unfinished ? SampleOutcome.Fail : SampleOutcome.Pass);
            _drillStarted = false;
        }

        private void Refresh()
        {
            if (_screen == null || Lesson == null) return;
            StationBuilder.SetLabel(DrillButton, Lesson.DrillOpen ? "Finish drill" : "Start drill");
            _screen.text = $"<b>HEADSET 3 · SHARED</b>\n<size=82%>{_message}</size>";
        }

        private void BuildCart(Transform root)
        {
            if (cartModel != null)
            {
                // Three headsets charging and one bay empty: a headset is out with a trainee.
                var cart = StationBuilder.Model(root, "Cart Model", cartModel, Vector3.zero, Vector3.one).transform;
                for (var i = 0; i < 4; i++)
                {
                    var docked = i < 3;
                    if (docked && headsetModel != null)
                        StationBuilder.Model(root, "Headset", headsetModel, new Vector3(-0.36f + i * 0.24f, 0.915f, -0.06f), Vector3.one);
                    StationBuilder.Glow(cart.Find($"Bay Light {i + 1}").gameObject, PropColors.SafetyGreen, docked);
                }
                StationBuilder.Text(root, "<b>SHARED HEADSETS</b>", new Vector3(0, 0.62f, -0.262f), 0.36f, UiKit.Ink, 0.9f, 0.1f);
                return;
            }

            Block(root, "Cart", new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.82f, 0.5f), PropColors.Steel);
            Block(root, "Top", new Vector3(0, 0.88f, 0), new Vector3(1.04f, 0.04f, 0.54f), PropColors.DarkSteel);
            for (var i = 0; i < 3; i++)
            {
                var x = -0.3f + i * 0.3f;
                Block(root, "Headset", new Vector3(x, 0.97f, 0), new Vector3(0.2f, 0.11f, 0.22f), PropColors.Rubber);
                Block(root, "Strap", new Vector3(x, 1.04f, 0.05f), new Vector3(0.18f, 0.04f, 0.1f), PropColors.Off);
            }
            StationBuilder.Text(root, "<b>SHARED HEADSETS</b>", new Vector3(0, 0.62f, -0.255f), 0.36f, UiKit.Ink, 0.9f, 0.1f);
        }

        private void BuildPanel(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.34f, 0.74f), material, stand: true);
            _screen = StationBuilder.Text(root, "", new Vector3(0, 0.12f, -0.025f), 0.52f, Color.white, 1.28f, 0.48f, TextAlignmentOptions.TopLeft);
            DrillButton = StationBuilder.Button(root, "Start drill", new Vector3(-0.42f, -0.27f, -0.03f), new Vector2(0.4f, 0.12f), material, UiKit.Purple, 0.34f);
            DrillButton.Activated += _ => ToggleDrill();
            HandOverButton = StationBuilder.Button(root, "Hand to next trainee", new Vector3(0.1f, -0.27f, -0.03f), new Vector2(0.58f, 0.12f), material, UiKit.Purple, 0.32f);
            HandOverButton.Activated += _ => HandOver();
            EndSessionButton = StationBuilder.Button(root, "End session", new Vector3(0.53f, -0.11f, -0.03f), new Vector2(0.28f, 0.1f), material, UiKit.NavyLighter, 0.26f);
            EndSessionButton.Activated += _ => EndSession();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
