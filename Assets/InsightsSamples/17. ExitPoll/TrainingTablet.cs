using System;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.ExitPoll
{
    /// <summary>
    /// A tablet at the end of fire-extinguisher practice, beside the put-out training pan. Finishing the training
    /// asks for feedback; a learner who rates their confidence 3 or lower is flagged for a refresher.
    /// </summary>
    public class TrainingTablet : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject extinguisherModel;
        [SerializeField] private GameObject panModel;
        [SerializeField] private GameObject cabinetModel;

        private TextMeshPro _screen;
        private string _confidence;
        private string _useful;
        private string _nextTopic;

        public event Action TrainingFinished;

        public StationInteractable FinishButton { get; private set; }

        private void Awake()
        {
            BuildStand(StationBuilder.Group(transform, "Tablet Stand", new Vector3(-0.3f, 0, 0.6f)));
            BuildPractice(StationBuilder.Group(transform, "Practice Area", new Vector3(0.8f, 0, 1.0f)));
            BuildCabinet();
            Refresh("The fire is out. Tap <b>Finish training</b> to give feedback.");
        }

        public void ShowConfidence(string rating)
        {
            _confidence = rating;
            Refresh("Thanks.");
        }

        public void ShowUseful(string thumbs)
        {
            _useful = thumbs;
            Refresh("Thanks.");
        }

        public void ShowNextTopic(string topic)
        {
            _nextTopic = topic;
            var confident = int.TryParse(_confidence, out var rating) && rating >= 4;
            Refresh(confident ? "<color=#05DA98><b>Feedback recorded.</b></color>"
                : "<color=#F5B841><b>Feedback recorded. Flagged for a refresher: confidence 3 or lower.</b></color>");
            SampleEvents.ReportFinished(confident ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Finish()
        {
            _confidence = _useful = _nextTopic = null;
            Refresh("Answer the questions in front of you.");
            TrainingFinished?.Invoke();
        }

        private void Refresh(string message)
        {
            _screen.text = "<b>FIRE EXTINGUISHER PRACTICE</b>\n<size=80%>" +
                           $"Confidence: <b>{_confidence ?? "-"}</b> of 5   Useful: <b>{_useful ?? "-"}</b>\nNext topic: <b>{_nextTopic ?? "-"}</b>\n{message}</size>";
        }

        private void BuildStand(Transform root)
        {
            StationBuilder.Kiosk(root, new Vector3(0, 1.4f, 0), new Vector3(1.0f, 0.68f, 0.05f), new Vector3(0, 1.42f, -0.026f), new Vector2(0.92f, 0.56f), material);
            _screen = StationBuilder.Text(root, "", new Vector3(0, 1.48f, -0.03f), 0.5f, Color.white, 0.86f, 0.42f, TextAlignmentOptions.TopLeft);
            FinishButton = StationBuilder.Button(root, "Finish training", new Vector3(0, 1.21f, -0.04f), new Vector2(0.46f, 0.1f), material, UiKit.Purple, 0.32f);
            FinishButton.Activated += _ => Finish();
        }

        /// <summary>The extinguisher's wall cabinet on the back wall, standing empty while it's out at the pan.</summary>
        private void BuildCabinet()
        {
            if (cabinetModel == null) return;
            var cabinet = StationBuilder.Model(transform, "Extinguisher Cabinet", cabinetModel, new Vector3(1.55f, 0.95f, 2.4f), Vector3.one).transform;
            StationBuilder.Text(cabinet, "<b>FIRE\nEXTINGUISHER</b>", new Vector3(0, 0.69f, -0.238f), 0.3f, PropColors.SafetyRed, 0.3f, 0.08f);
        }

        private void BuildPractice(Transform root)
        {
            // A propane training pan, shut off at its supply post, and the extinguisher that put it out.
            if (panModel != null)
                StationBuilder.Model(root, "Pan Model", panModel, Vector3.zero, Vector3.one);
            else
            {
                Block(root, "Pan", new Vector3(0, 0.22f, 0), new Vector3(0.75f, 0.17f, 0.6f), PropColors.DarkSteel);
                foreach (var x in new[] { -0.33f, 0.33f })
                foreach (var z in new[] { -0.26f, 0.26f })
                    Block(root, "Leg", new Vector3(x, 0.07f, z), new Vector3(0.05f, 0.14f, 0.05f), PropColors.DarkSteel);
            }

            // Yellow floor tape marks out the practice zone.
            const float tapeWidth = 0.05f;
            var zone = new Vector2(1.5f, 0.95f);
            foreach (var z in new[] { -zone.y / 2, zone.y / 2 })
                Block(root, "Floor Tape", new Vector3(0, 0.001f, z), new Vector3(zone.x + tapeWidth, 0.002f, tapeWidth), PropColors.SafetyYellow);
            foreach (var x in new[] { -zone.x / 2, zone.x / 2 })
                Block(root, "Floor Tape", new Vector3(x, 0.001f, 0), new Vector3(tapeWidth, 0.002f, zone.y), PropColors.SafetyYellow);

            var extinguisher = new Vector3(-0.58f, 0, -0.12f);
            if (extinguisherModel != null)
                StationBuilder.Model(root, "Extinguisher Model", extinguisherModel, extinguisher, Vector3.one)
                    .transform.localRotation = Quaternion.Euler(0, 25f, 0);
            else
                StationBuilder.Cylinder(root, "Extinguisher", extinguisher + new Vector3(0, 0.25f, 0), new Vector3(0.15f, 0.5f, 0.15f),
                    material, PropColors.SafetyRed);
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
