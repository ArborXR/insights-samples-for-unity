using System;
using System.Text;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.Objectives
{
    /// <summary>
    /// A running conveyor with its lockout point: a radio, stop and start buttons, the main disconnect, and a
    /// lock and tag. The learner does the five steps in order; doing a later step first skips the current one.
    /// </summary>
    public class LockoutStation : MonoBehaviour
    {
        public const int StepCount = 5;

        private static readonly string[] Steps =
        {
            "Notify the line team",
            "Shut down the conveyor",
            "Isolate energy at the main disconnect",
            "Apply your lock and tag",
            "Verify zero energy: try to start it",
        };

        [SerializeField] private Material material;
        [SerializeField] private GameObject lockModel;
        [SerializeField] private GameObject conveyorModel;
        [SerializeField] private GameObject cartonModel;
        [SerializeField] private GameObject controlsModel;
        [SerializeField] private GameObject disconnectModel;
        [SerializeField] private GameObject radioModel;
        [SerializeField] private GameObject lockBoardModel;

        private readonly StationInteractable[] _actions = new StationInteractable[StepCount];
        private readonly bool?[] _results = new bool?[StepCount];
        private Conveyor _conveyor;
        private TextMeshPro _board;
        private TextMeshPro _disconnectState;
        private Transform _handle;
        private Transform _lock;
        private Vector3 _lockHome;
        private Vector3 _lockApplied;
        private int _current = -1;
        private bool _isolated;
        private string _message = "Click <b>Begin lockout</b>, then do each step in order.";
        private string _result;

        public event Action ProcedureStarted;
        public event Action<int> StepStarted;
        public event Action<int, bool> StepFinished;
        public event Action ProcedureFinished;

        public StationInteractable BeginButton { get; private set; }
        public bool InProcedure => _current >= 0 && _current < StepCount;

        /// <summary>The prop for a step: 0 radio, 1 stop button, 2 main disconnect, 3 lock and tag, 4 start button.</summary>
        public StationInteractable ActionFor(int step) => _actions[step];

        private void Awake()
        {
            _conveyor = Conveyor.Build(transform, new Vector3(0.2f, 0, 0.6f), 2.7f, material, conveyorModel);
            _conveyor.AddCarton(-0.8f, new Vector3(0.32f, 0.26f, 0.3f), model: cartonModel);
            _conveyor.AddCarton(0.55f, new Vector3(0.26f, 0.22f, 0.26f), model: cartonModel);
            BuildRadio(Group(transform, "Radio", new Vector3(-1.35f, 0, -0.1f)));
            BuildControls(Group(transform, "Controls", new Vector3(-0.7f, 0, -0.1f)));
            BuildDisconnect(Group(transform, "Main Disconnect", new Vector3(-0.05f, 0, -0.1f)));
            BuildLockPoint(Group(transform, "Lockout Point", new Vector3(0.6f, 0, -0.1f)));
            BuildBoard(Group(transform, "Procedure Board", new Vector3(0.45f, 2.05f, 0.75f)));

            for (var step = 0; step < StepCount; step++)
            {
                var index = step;
                _actions[step].Activated += _ => Act(index);
            }
            Refresh();
        }

        public void ShowResult(bool passed, int score)
        {
            _result = $"Score {score}: " + (passed ? "<color=#05DA98>passed</color>" : "<color=#F5B841>not passed</color>");
            StationBuilder.SetLabel(BeginButton, "Start again");
            BeginButton.gameObject.SetActive(true);
            Refresh();
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Begin()
        {
            Array.Clear(_results, 0, _results.Length);
            _conveyor.Running = true;
            _isolated = false;
            _lock.localPosition = _lockHome;
            _message = null;
            _result = null;
            _current = 0;
            BeginButton.gameObject.SetActive(false);

            ProcedureStarted?.Invoke();
            StepStarted?.Invoke(_current);
            Refresh();
        }

        private void Act(int step)
        {
            if (!InProcedure || step < _current) return;
            while (_current < step) Finish(false);
            Finish(Perform(step));
        }

        /// <summary>Does what the prop does, and returns whether that completed its step correctly.</summary>
        private bool Perform(int step)
        {
            switch (step)
            {
                case 0:
                    _message = "Line team notified: conveyor 3 is going down for maintenance.";
                    return true;
                case 1:
                    _conveyor.Running = false;
                    _message = "Conveyor stopped.";
                    return true;
                case 2:
                    _isolated = true;
                    _conveyor.Running = false;
                    _message = "Main disconnect off: the conveyor has no power.";
                    return true;
                case 3:
                    _lock.localPosition = _lockApplied;
                    _message = "Lock and tag applied to the main disconnect.";
                    return true;
                default:
                    if (_isolated)
                    {
                        _message = "Start pressed: nothing moves. Zero energy verified.";
                        return true;
                    }
                    _conveyor.Running = true;
                    _message = "<color=#E5484D>Start pressed: the conveyor started. Its energy was never isolated.</color>";
                    return false;
            }
        }

        private void Finish(bool correct)
        {
            _results[_current] = correct;
            StepFinished?.Invoke(_current, correct);
            _current++;
            if (_current < StepCount) StepStarted?.Invoke(_current);
            else ProcedureFinished?.Invoke();
            Refresh();
        }

        private void Refresh()
        {
            for (var step = 0; step < StepCount; step++)
                _actions[step].Interactable = InProcedure && step >= _current;

            _handle.localRotation = Quaternion.Euler(0, 0, _isolated ? 180f : 0f);
            _disconnectState.text = _isolated ? "<color=#E5484D>OFF</color>" : "<color=#2E9E5B>ON</color>";

            var text = new StringBuilder("<b>LOCKOUT/TAGOUT · CONVEYOR 3</b>\n<size=82%>");
            for (var step = 0; step < StepCount; step++)
            {
                var line = $"{step + 1}. {Steps[step]}";
                if (_results[step] == true) text.Append($"<color=#05DA98>{line} · done</color>\n");
                else if (_results[step] == false) text.Append($"<color=#F5B841>{line} · missed</color>\n");
                else if (step == _current) text.Append($"<b>{line} · now</b>\n");
                else text.Append($"<color=#FFFFFF99>{line}</color>\n");
            }
            text.Append("</size>");
            if (!string.IsNullOrEmpty(_message)) text.Append($"<size=75%>{_message}</size>\n");
            if (!string.IsNullOrEmpty(_result)) text.Append($"<b>{_result}</b>");
            _board.text = text.ToString();
        }

        private void BuildRadio(Transform root)
        {
            if (radioModel != null)
            {
                // The site radio sits in its charger on a post-mounted shelf.
                var model = StationBuilder.Model(root, "Radio Model", radioModel, Vector3.zero, Vector3.one).transform;
                var handset = model.Find("Radio").gameObject;
                _actions[0] = StationBuilder.Interactable(handset, "Radio", new Vector3(0.24f, 0.32f, 0.16f), handset, PropColors.Textured);
                Label(root, "Radio", new Vector3(0, 1.25f, 0));
                return;
            }

            Block(root, "Stand", new Vector3(0, 0.45f, 0), new Vector3(0.36f, 0.9f, 0.3f), PropColors.Panel);
            var radio = Group(root, "Handheld Radio", new Vector3(0, 0.99f, 0));
            var body = Block(radio, "Body", Vector3.zero, new Vector3(0.08f, 0.17f, 0.04f), PropColors.Rubber);
            Block(radio, "Antenna", new Vector3(0.02f, 0.13f, 0), new Vector3(0.014f, 0.1f, 0.014f), PropColors.Rubber);
            _actions[0] = StationBuilder.Interactable(radio.gameObject, "Radio", new Vector3(0.24f, 0.32f, 0.16f), body, PropColors.Rubber);
            Label(root, "Radio", new Vector3(0, 1.2f, 0));
        }

        private void BuildControls(Transform root)
        {
            if (controlsModel != null)
            {
                // A normal flush STOP for the orderly shutdown; an e-stop is for emergencies, not lockout.
                var station = StationBuilder.Model(root, "Controls Model", controlsModel, Vector3.zero, Vector3.one).transform;
                _actions[1] = PushButton(root, station.Find("Stop Button").gameObject, "STOP", PropColors.SafetyRed, 1.28f);
                _actions[4] = PushButton(root, station.Find("Start Button").gameObject, "START", PropColors.SafetyGreen, 1.1f);
                Label(root, "Conveyor 3", new Vector3(0, 1.47f, -0.08f));
                return;
            }

            Block(root, "Post", new Vector3(0, 0.55f, 0.05f), new Vector3(0.07f, 1.1f, 0.07f), PropColors.DarkSteel);
            Block(root, "Control Box", new Vector3(0, 1.18f, 0), new Vector3(0.34f, 0.44f, 0.14f), PropColors.SafetyYellow);
            _actions[1] = StationBuilder.Button(root, "STOP", new Vector3(0, 1.28f, -0.08f), new Vector2(0.24f, 0.12f), material, PropColors.SafetyRed, 0.5f);
            _actions[4] = StationBuilder.Button(root, "START", new Vector3(0, 1.1f, -0.08f), new Vector2(0.24f, 0.12f), material, PropColors.SafetyGreen, 0.5f);
            Label(root, "Conveyor 3", new Vector3(0, 1.47f, -0.08f));
        }

        private void BuildDisconnect(Transform root)
        {
            var box = Group(root, "Switch", new Vector3(0, 1.25f, 0));
            _handle = Group(box, "Handle Pivot", new Vector3(0.2f, -0.02f, -0.04f));
            GameObject lever;
            if (disconnectModel != null)
            {
                var model = StationBuilder.Model(root, "Disconnect Model", disconnectModel, Vector3.zero, Vector3.one).transform;
                lever = model.Find("Disconnect Lever").gameObject;
                lever.transform.SetParent(_handle, true);
            }
            else
            {
                Block(root, "Post", new Vector3(0, 0.6f, 0.05f), new Vector3(0.07f, 1.2f, 0.07f), PropColors.DarkSteel);
                Block(box, "Enclosure", Vector3.zero, new Vector3(0.32f, 0.42f, 0.16f), PropColors.Steel);
                lever = Block(_handle, "Lever", new Vector3(0, 0.1f, 0), new Vector3(0.07f, 0.22f, 0.07f), PropColors.SafetyRed);
            }
            _disconnectState = StationBuilder.Text(box, "", new Vector3(-0.03f, 0.06f, -0.088f), 0.5f, Color.white, 0.2f, 0.1f);
            _disconnectState.fontStyle = FontStyles.Bold;

            _actions[2] = StationBuilder.Interactable(box.gameObject, "Main disconnect", new Vector3(0.46f, 0.5f, 0.2f), lever,
                PropColors.SafetyRed, new Vector3(0.05f, 0, 0));
            // Where the padlock hangs once applied: on the end of the lever in its OFF position.
            _lockApplied = root.localPosition + box.localPosition + _handle.localPosition + new Vector3(0.06f, -0.13f, -0.05f);
            Label(root, "Main disconnect", new Vector3(0, 1.53f, -0.08f));
        }

        private StationInteractable PushButton(Transform root, GameObject cap, string label, Color color, float y)
        {
            StationBuilder.Text(root, $"<b>{label}</b>", new Vector3(-0.09f, y, -0.078f), 0.3f, UiKit.Ink, 0.1f, 0.04f);
            return StationBuilder.Interactable(cap, label, new Vector3(0.1f, 0.1f, 0.06f), cap, color);
        }

        private void BuildLockPoint(Transform root)
        {
            var header = 1.38f;
            if (lockBoardModel != null)
            {
                // A lockout board: spare locks, a hasp and a tag pad around the peg your lock and tag hang from.
                StationBuilder.Model(root, "Lock Board Model", lockBoardModel, Vector3.zero, Vector3.one);
                header = 1.435f;
            }
            else
            {
                Block(root, "Post", new Vector3(0, 0.6f, 0.05f), new Vector3(0.07f, 1.2f, 0.07f), PropColors.DarkSteel);
                Block(root, "Board", new Vector3(0, 1.22f, 0.01f), new Vector3(0.36f, 0.42f, 0.03f), PropColors.Panel);
                Block(root, "Header", new Vector3(0, 1.38f, -0.006f), new Vector3(0.36f, 0.1f, 0.004f), PropColors.SafetyRed);
            }
            StationBuilder.Text(root, "<b>LOCKOUT</b>", new Vector3(0, header, -0.004f), 0.4f, Color.white, 0.34f, 0.08f);

            _lock = Group(transform, "Lock and Tag", root.localPosition + new Vector3(0, 1.18f, -0.05f));
            _lockHome = _lock.localPosition;
            GameObject body;
            if (lockModel != null)
            {
                // The tag's red header band carries DANGER in white, as on a real lockout tag.
                var model = StationBuilder.Model(_lock, "Lock Model", lockModel, new Vector3(-0.06f, 0, 0), Vector3.one).transform;
                body = model.Find("Padlock Body").gameObject;
                StationBuilder.Text(_lock, "<b>DANGER</b>", new Vector3(0.07f, 0.03f, -0.006f), 0.16f, Color.white, 0.1f, 0.04f);
                StationBuilder.Text(_lock, "<b>DO NOT\nOPERATE</b>", new Vector3(0.07f, -0.06f, -0.006f), 0.15f, UiKit.Ink, 0.1f, 0.08f);
            }
            else
            {
                body = Block(_lock, "Padlock", new Vector3(-0.06f, 0, 0), new Vector3(0.08f, 0.09f, 0.04f), PropColors.SafetyRed);
                Block(_lock, "Shackle", new Vector3(-0.06f, 0.07f, 0), new Vector3(0.06f, 0.05f, 0.015f), PropColors.Steel);
                Block(_lock, "Tag", new Vector3(0.07f, -0.04f, 0), new Vector3(0.11f, 0.18f, 0.01f), Color.white);
                StationBuilder.Text(_lock, "<color=#D32F2F><b>DANGER</b></color>\n<color=#111111>DO NOT\nOPERATE</color>", new Vector3(0.07f, -0.04f, -0.007f), 0.16f, Color.white, 0.1f, 0.16f);
            }
            _actions[3] = StationBuilder.Interactable(_lock.gameObject, "Lock and tag", new Vector3(0.3f, 0.3f, 0.1f), body, PropColors.SafetyRed);
            Label(root, "Lock and tag", new Vector3(0, lockBoardModel != null ? 1.56f : 1.53f, -0.08f));
        }

        private void BuildBoard(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.84f, 0.76f), material, stand: false);
            _board = StationBuilder.Text(root, "", new Vector3(0, 0.02f, -0.025f), 0.62f, Color.white, 1.76f, 0.72f, TextAlignmentOptions.TopLeft);
            BeginButton = StationBuilder.Button(root, "Begin lockout", new Vector3(0.62f, -0.3f, -0.03f), new Vector2(0.5f, 0.13f), material, UiKit.Purple, 0.42f);
            BeginButton.Activated += _ => Begin();
        }

        private static Transform Group(Transform parent, string name, Vector3 localPosition) =>
            StationBuilder.Group(parent, name, localPosition);

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);

        private static void Label(Transform parent, string text, Vector3 localPosition) =>
            StationBuilder.Text(parent, $"<b>{text}</b>", localPosition, 0.36f, UiKit.Body, 0.6f, 0.08f);
    }
}
