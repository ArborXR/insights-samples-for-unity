using System;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.CriticalEvents
{
    /// <summary>
    /// A conveyor with a carton jammed against its rail, still running, and an emergency stop beside it.
    /// Clearing the jam is safe only once the conveyor is stopped.
    /// </summary>
    public class JammedConveyor : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject conveyorModel;
        [SerializeField] private GameObject cartonModel;
        [SerializeField] private GameObject crushedCartonModel;
        [SerializeField] private GameObject stackLightModel;
        [SerializeField] private GameObject emergencyStopModel;
        [SerializeField] private GameObject cautionSignModel;

        private Conveyor _conveyor;
        private Transform _jam;
        private Vector3 _jamPosition;
        private Quaternion _jamRotation;
        private Transform _mushroom;
        private GameObject _beacon;
        private GameObject _alarm;
        private TextMeshPro _board;
        private bool _working;
        private bool _jammed = true;
        private bool _beaconLit;
        private string _message = "Click <b>Begin</b>, then clear the jammed carton.";

        public event Action TaskStarted;
        public event Action ReachedIntoRunningConveyor;
        public event Action JamCleared;

        public StationInteractable BeginButton { get; private set; }
        public StationInteractable EmergencyStop { get; private set; }
        public StationInteractable JammedCarton { get; private set; }

        private void Awake()
        {
            _conveyor = Conveyor.Build(transform, new Vector3(0.35f, 0, 0.55f), 2.6f, material, conveyorModel);
            _conveyor.AddCarton(-0.35f, new Vector3(0.3f, 0.24f, 0.3f), riding: false, model: cartonModel);
            _conveyor.AddCarton(-0.7f, new Vector3(0.28f, 0.26f, 0.28f), riding: false, model: cartonModel);
            BuildJam(_conveyor.transform);
            BuildBeacon();
            BuildEmergencyStop(StationBuilder.Group(transform, "Emergency Stop", new Vector3(-1.2f, 0, 0.1f)));
            BuildSign(StationBuilder.Group(transform, "Caution Sign", new Vector3(1.0f, 1.32f, 0.2f)));
            BuildBoard(StationBuilder.Group(transform, "Task Board", new Vector3(0.1f, 1.95f, 1.0f)));
            Refresh();
        }

        private void Update()
        {
            var lit = _jammed && Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            if (lit == _beaconLit) return;
            _beaconLit = lit;
            StationBuilder.Glow(_beacon, UiKit.Amber, lit);
        }

        public void ShowResult(bool passed)
        {
            _working = false;
            _message += $"\n<b>{(passed ? "<color=#05DA98>Passed</color>" : "<color=#F5B841>Not passed: a critical safety violation fails the task</color>")}</b>";
            StationBuilder.SetLabel(BeginButton, "Start again");
            Refresh();
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Begin()
        {
            _conveyor.Running = true;
            _mushroom.localPosition = new Vector3(0, 0, -0.1f);
            _jam.localPosition = _jamPosition;
            _jam.localRotation = _jamRotation;
            _jammed = true;
            _working = true;
            _message = "Carton jammed against the rail. Clear it.";
            Refresh();
            TaskStarted?.Invoke();
        }

        private void Stop()
        {
            if (!_working || !_conveyor.Running) return;
            _conveyor.Running = false;
            _mushroom.localPosition = new Vector3(0, 0, -0.07f);
            _message = "Emergency stop pressed. The conveyor has stopped.";
            Refresh();
        }

        private void ClearJam()
        {
            if (!_working) return;
            if (_conveyor.Running)
            {
                _message = "<color=#E5484D><b>You reached into a running conveyor.</b> Belts pull hands into the rollers. Stop it first.</color>";
                Refresh();
                ReachedIntoRunningConveyor?.Invoke();
                return;
            }

            _jam.localPosition = new Vector3(_jamPosition.x + 0.1f, 0.13f, -0.75f);
            _jam.localRotation = Quaternion.identity;
            _jammed = false;
            _message = "Jam cleared with the conveyor stopped.";
            Refresh();
            JamCleared?.Invoke();
        }

        private void Refresh()
        {
            EmergencyStop.Interactable = _working && _conveyor.Running;
            JammedCarton.Interactable = _working;
            BeginButton.gameObject.SetActive(!_working);
            if (_alarm != null) StationBuilder.Glow(_alarm, PropColors.SafetyRed, _working && _jammed && !_conveyor.Running);
            _board.text = $"<b>CONVEYOR 7 · JAM</b>\n<size=82%>{_message}</size>";
        }

        private void BuildJam(Transform conveyor)
        {
            _jamPosition = new Vector3(0.05f, 0.86f, 0.06f);
            _jamRotation = Quaternion.Euler(10f, 34f, 6f);
            _jam = StationBuilder.Group(conveyor, "Jammed Carton", _jamPosition);
            _jam.localRotation = _jamRotation;
            var size = new Vector3(0.34f, 0.26f, 0.32f);
            GameObject carton;
            Color color;
            if (crushedCartonModel != null)
            {
                // Wedged with its front edge up on the front guide rail and the far corner on the belt.
                _jamPosition = new Vector3(0.05f, 0.892f, -0.046f);
                _jamRotation = Quaternion.Euler(14f, 14f, 3f);
                _jam.localPosition = _jamPosition;
                _jam.localRotation = _jamRotation;
                carton = StationBuilder.Model(_jam, "Carton", crushedCartonModel, Vector3.zero, size / Conveyor.CartonModelSize);
                color = PropColors.Textured;
            }
            else
            {
                carton = Block(_jam, "Carton", Vector3.zero, size, PropColors.Cardboard);
                Block(_jam, "Tape", new Vector3(0, 0.131f, 0), new Vector3(0.06f, 0.004f, 0.32f), UiKit.FromHex(0xD9C49A));
                color = PropColors.Cardboard;
            }
            JammedCarton = StationBuilder.Interactable(_jam.gameObject, "Jammed carton", new Vector3(0.4f, 0.32f, 0.38f), carton, color);
            JammedCarton.Activated += _ => ClearJam();
        }

        private void BuildBeacon()
        {
            if (stackLightModel == null)
            {
                Block(transform, "Beacon Post", new Vector3(1.85f, 0.86f, 0.55f), new Vector3(0.04f, 0.24f, 0.04f), PropColors.DarkSteel);
                _beacon = Block(transform, "Jam Beacon", new Vector3(1.85f, 1.02f, 0.55f), new Vector3(0.11f, 0.12f, 0.11f), PropColors.Off);
                return;
            }

            // A stack light on its own pole at the head end: green while running, amber while jammed, red once stopped on the jam.
            var stack = StationBuilder.Model(transform, "Stack Light", stackLightModel, new Vector3(1.85f, 0, 0.55f), Vector3.one).transform;
            _beacon = stack.Find("Lens Amber").gameObject;
            _alarm = stack.Find("Lens Red").gameObject;
            _conveyor.AddRunningLamp(stack.Find("Lens Green").gameObject);
            StationBuilder.Glow(_beacon, UiKit.Amber, false);
        }

        private void BuildEmergencyStop(Transform root)
        {
            var box = StationBuilder.Group(root, "Station", new Vector3(0, 1.18f, 0));
            GameObject head;
            if (emergencyStopModel != null)
            {
                var model = StationBuilder.Model(box, "Emergency Stop Model", emergencyStopModel, Vector3.zero, Vector3.one).transform;
                // The post stays on the root so the press pulse on the station doesn't bounce it.
                model.Find("E-Stop Post").SetParent(root, true);
                _mushroom = model.Find("E-Stop Head");
                head = _mushroom.gameObject;
                StationBuilder.Text(root, "<b>EMERGENCY STOP</b>", new Vector3(0, 1.045f, -0.085f), 0.14f, UiKit.Ink, 0.16f, 0.04f);
            }
            else
            {
                Block(root, "Post", new Vector3(0, 0.55f, 0.05f), new Vector3(0.07f, 1.1f, 0.07f), PropColors.DarkSteel);
                Block(box, "Box", Vector3.zero, new Vector3(0.24f, 0.24f, 0.14f), PropColors.SafetyYellow);
                _mushroom = StationBuilder.Group(box, "Mushroom", new Vector3(0, 0, -0.1f));
                head = StationBuilder.Cylinder(_mushroom, "Head", Vector3.zero, new Vector3(0.15f, 0.06f, 0.15f), material, PropColors.SafetyRed);
                head.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                StationBuilder.Text(root, "<b>EMERGENCY\nSTOP</b>", new Vector3(0, 1.4f, -0.08f), 0.3f, UiKit.Ink, 0.4f, 0.12f);
            }
            EmergencyStop = StationBuilder.Interactable(box.gameObject, "Emergency stop", new Vector3(0.3f, 0.3f, 0.3f), head,
                PropColors.SafetyRed, new Vector3(0, 0, -0.06f));
            EmergencyStop.Activated += _ => Stop();
        }

        private void BuildSign(Transform root)
        {
            if (cautionSignModel != null)
            {
                // The sign face carries the header band and alert symbol; the words stay live text so they read sharply in VR.
                StationBuilder.Model(root, "Sign Model", cautionSignModel, Vector3.zero, Vector3.one);
                StationBuilder.Text(root, "<b>CAUTION</b>", new Vector3(0.03f, 0.145f, -0.012f), 0.42f, PropColors.SafetyYellow, 0.4f, 0.1f);
                StationBuilder.Text(root, "<b>Stop the conveyor before clearing jams</b>", new Vector3(0, -0.055f, -0.012f), 0.34f, UiKit.Ink, 0.48f, 0.24f);
                return;
            }

            Block(root, "Post", new Vector3(0, -0.62f, 0.02f), new Vector3(0.05f, 1.25f, 0.05f), PropColors.DarkSteel);
            Block(root, "Sign", Vector3.zero, new Vector3(0.56f, 0.4f, 0.02f), PropColors.SafetyYellow);
            Block(root, "Band", new Vector3(0, 0.13f, -0.011f), new Vector3(0.56f, 0.11f, 0.002f), PropColors.Rubber);
            StationBuilder.Text(root, "<b>CAUTION</b>", new Vector3(0, 0.13f, -0.014f), 0.42f, PropColors.SafetyYellow, 0.52f, 0.1f);
            StationBuilder.Text(root, "<b>Stop the conveyor before clearing jams</b>", new Vector3(0, -0.06f, -0.012f), 0.32f, UiKit.Ink, 0.5f, 0.22f);
        }

        private void BuildBoard(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.8f, 0.55f), material);
            _board = StationBuilder.Text(root, "", new Vector3(-0.12f, 0.02f, -0.025f), 0.6f, Color.white, 1.48f, 0.48f, TextAlignmentOptions.TopLeft);
            BeginButton = StationBuilder.Button(root, "Begin", new Vector3(0.68f, -0.17f, -0.03f), new Vector2(0.3f, 0.12f), material, UiKit.Purple, 0.4f);
            BeginButton.Activated += _ => Begin();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
