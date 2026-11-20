using System;
using System.Text;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.Storage
{
    /// <summary>
    /// A training console for a three-chapter forklift maintenance course. It resumes where the learner left
    /// off, when the app finds saved progress.
    /// </summary>
    public class CourseConsole : MonoBehaviour
    {
        private static readonly string[] Chapters = { "Daily inspection", "Battery charging", "Hydraulics and forks" };

        [SerializeField] private Material material;
        [SerializeField] private GameObject forkliftModel;

        private TextMeshPro _screen;
        private int _completed;
        private bool _learnerActed;
        private string _status = "Looking for saved progress…";
        private string _history = "";
        private bool _signedIn;
        private bool _followsLearner;

        public event Action<int> ChapterCompleted;
        public event Action ResetRequested;

        public StationInteractable CompleteButton { get; private set; }
        public StationInteractable ResetButton { get; private set; }

        private void Awake()
        {
            BuildConsole(StationBuilder.Group(transform, "Console", new Vector3(-0.15f, 0, 0.3f)));
            BuildForklift(StationBuilder.Group(transform, "Forklift", new Vector3(1.25f, 0, 0.7f)));
            Refresh();
        }

        /// <summary>
        /// Where progress goes: nowhere while signed out, and with a signed-in session, to the learner or, when the
        /// sign-in identifies nobody, to this device.
        /// </summary>
        public void ShowStorage(bool signedIn, bool followsLearner)
        {
            _signedIn = signedIn;
            _followsLearner = followsLearner;
            Refresh();
        }

        /// <summary>
        /// Picks up from the last completed chapter (0 for none). readSucceeded is false when nothing could be read.
        /// Ignored once the learner has started, since a slow read mustn't undo their clicks.
        /// </summary>
        public void Resume(int lastChapter, bool readSucceeded)
        {
            if (_learnerActed) return;
            _completed = Mathf.Clamp(lastChapter, 0, Chapters.Length);
            _status = !readSucceeded ? _signedIn ? "Couldn't read saved progress." : "Starting at chapter 1."
                : _completed > 0 ? $"Resumed after chapter {_completed} from saved progress."
                : "No saved progress. Starting at chapter 1.";
            Refresh();
        }

        public void ShowHistory(int entries)
        {
            _history = entries > 0 ? $"History: {entries} chapter {(entries == 1 ? "completion" : "completions")} saved." : "";
            Refresh();
        }

        private void Complete()
        {
            if (_completed >= Chapters.Length) return;
            _learnerActed = true;
            _completed++;
            // The app saves it first, which also says whether anything could be kept.
            ChapterCompleted?.Invoke(_completed);
            _status = _completed == Chapters.Length ? "<color=#05DA98><b>Course complete.</b></color>"
                : _signedIn ? $"Chapter {_completed} saved."
                : $"Chapter {_completed} done.";
            Refresh();
            if (_completed == Chapters.Length) SampleEvents.ReportFinished(SampleOutcome.Pass);
        }

        private void ResetProgress()
        {
            var abandoned = _completed > 0 && _completed < Chapters.Length;
            _learnerActed = true;
            _completed = 0;
            _status = abandoned ? "<color=#F5B841><b>Progress reset before the course was finished.</b></color>" : "Progress reset. Starting at chapter 1.";
            Refresh();
            ResetRequested?.Invoke();
            if (abandoned) SampleEvents.ReportFinished(SampleOutcome.Fail);
        }

        private void Refresh()
        {
            var text = new StringBuilder("<b>FORKLIFT MAINTENANCE</b>  <size=75%>3 chapters</size>\n<size=80%>");
            for (var i = 0; i < Chapters.Length; i++)
            {
                var line = $"{i + 1}. {Chapters[i]}";
                text.Append(i < _completed ? $"<color=#05DA98>{line} · done</color>\n" : i == _completed ? $"<b>{line} · next</b>\n" : $"<color=#FFFFFF99>{line}</color>\n");
            }
            text.Append($"</size><size=70%>{_status}");
            text.Append($"\n<color=#FFFFFF99>{StorageLine()}</color>");
            if (_history.Length > 0) text.Append($"\n<color=#FFFFFF99>{_history}</color>");
            _screen.text = text.Append("</size>").ToString();

            CompleteButton.Interactable = _completed < Chapters.Length;
            StationBuilder.SetLabel(CompleteButton, _completed < Chapters.Length ? $"Complete chapter {_completed + 1}" : "Course complete");
        }

        private string StorageLine() =>
            !_signedIn ? "Not signed in: progress isn't saved or read."
            : _followsLearner ? "Saved for this learner: it follows them to any headset."
            : "Signed in without a user: saved to this device.";

        private void BuildConsole(Transform root)
        {
            StationBuilder.Kiosk(root, new Vector3(0, 1.45f, 0.1f), new Vector3(1.3f, 0.92f, 0.12f), new Vector3(0, 1.55f, 0.039f), new Vector2(1.2f, 0.66f), material);
            _screen = StationBuilder.Text(root, "", new Vector3(0, 1.56f, 0.034f), 0.55f, Color.white, 1.1f, 0.6f, TextAlignmentOptions.TopLeft);
            CompleteButton = StationBuilder.Button(root, "Complete chapter 1", new Vector3(-0.25f, 1.1f, 0.03f), new Vector2(0.56f, 0.11f), material, UiKit.Purple, 0.34f);
            CompleteButton.Activated += _ => Complete();
            ResetButton = StationBuilder.Button(root, "Reset progress", new Vector3(0.36f, 1.1f, 0.03f), new Vector2(0.42f, 0.11f), material, UiKit.NavyLighter, 0.32f);
            ResetButton.Activated += _ => ResetProgress();
        }

        private void BuildForklift(Transform root)
        {
            if (forkliftModel != null)
            {
                var truck = StationBuilder.Model(root, "Forklift Model", forkliftModel, Vector3.zero, Vector3.one).transform;
                truck.Find("Wheel FL Damaged").gameObject.SetActive(false);
                // Real size, so it stands further back to fit beside the console.
                root.localPosition = new Vector3(1.0f, 0, 1.9f);
                root.localRotation = Quaternion.Euler(0, -42f, 0);
                return;
            }

            Block(root, "Body", new Vector3(0, 0.42f, 0.1f), new Vector3(0.7f, 0.5f, 1.0f), PropColors.SafetyYellow);
            Block(root, "Counterweight", new Vector3(0, 0.45f, 0.62f), new Vector3(0.72f, 0.5f, 0.16f), PropColors.Rubber);
            Block(root, "Overhead Guard", new Vector3(0, 1.32f, 0.2f), new Vector3(0.66f, 0.04f, 0.7f), PropColors.DarkSteel);
            foreach (var x in new[] { -0.31f, 0.31f })
            {
                Block(root, "Guard Post", new Vector3(x, 0.98f, -0.12f), new Vector3(0.04f, 0.7f, 0.04f), PropColors.DarkSteel);
                Block(root, "Guard Post", new Vector3(x, 0.98f, 0.52f), new Vector3(0.04f, 0.7f, 0.04f), PropColors.DarkSteel);
                Block(root, "Mast", new Vector3(x * 0.8f, 0.85f, -0.45f), new Vector3(0.06f, 1.6f, 0.06f), PropColors.DarkSteel);
                Block(root, "Fork", new Vector3(x * 0.6f, 0.08f, -0.75f), new Vector3(0.08f, 0.04f, 0.6f), PropColors.DarkSteel);
            }
            foreach (var z in new[] { -0.22f, 0.45f })
            foreach (var x in new[] { -0.36f, 0.36f })
                StationBuilder.Cylinder(root, "Wheel", new Vector3(x, 0.17f, z), new Vector3(0.34f, 0.14f, 0.34f), material, PropColors.Rubber).transform.localRotation = Quaternion.Euler(0, 0, 90f);
            root.localRotation = Quaternion.Euler(0, -35f, 0);
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
