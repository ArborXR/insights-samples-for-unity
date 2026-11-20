using System;
using System.Collections;
using System.Collections.Generic;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace InsightsSamples.DiagnosticLogs
{
    /// <summary>A robot cell's job file, as stored in StreamingAssets/RobotJobs.</summary>
    [Serializable]
    public class JobFile
    {
        public const float Missing = -1f;

        public string cell;
        public string program;
        public float payload_kg;
        public float speed_pct = Missing;
    }

    public sealed class JobResult
    {
        public string FileName;
        public JobFile Job;
        public bool UsedDefaultSpeed;
        public float SpeedPct;
    }

    /// <summary>
    /// A pick-and-place robot cell. Loading a job file from StreamingAssets starts the robot on its program. One
    /// shipped file is complete, one is valid but leaves out an optional field, and one is genuinely malformed.
    /// </summary>
    public class RobotCell : MonoBehaviour
    {
        public const float DefaultSpeedPct = 25f;
        private static readonly string[] Files = { "job_a.json", "job_b.json", "job_c.json" };

        // Arm geometry, matching RobotArm.fbx: joint heights and link lengths in its zero pose.
        private const float ShoulderHeight = 0.62f, ShoulderOffset = 0.08f, LowerArm = 0.6f, UpperArm = 0.55f, GraspLength = 0.17f;
        private const float MaxSpeed = 1.1f;
        private const float PartSize = 0.07f, StandHeight = 0.75f, FingerOpen = 0.045f;
        private static readonly Vector3 Home = new Vector3(0, 1.05f, -0.5f);
        private static readonly Vector3[] Stations = { new Vector3(-0.5f, StandHeight + PartSize / 2f, -0.25f), new Vector3(0.5f, StandHeight + PartSize / 2f, -0.25f) };
        private static readonly Vector3 Approach = new Vector3(0, 0.18f, 0);

        [SerializeField] private Material material;
        [SerializeField] private GameObject robotModel;
        [SerializeField] private GameObject controllerModel;
        [SerializeField] private GameObject fenceModel;
        [SerializeField] private GameObject stackLightModel;

        private readonly Dictionary<string, StationInteractable> _fileButtons = new Dictionary<string, StationInteractable>();
        private TextMeshPro _screen;
        private Transform _waist, _lower, _upper, _wrist, _fingerLeft, _fingerRight;
        private Transform _robot, _part;
        private GameObject _green, _amber, _red;
        private Vector3 _grasp;
        private Coroutine _program;
        private bool _loading;

        public event Action<JobResult> JobLoaded;
        public event Action<string, string> JobFailed;

        public StationInteractable FileButton(string fileName) => _fileButtons[fileName];

        private void Awake()
        {
            _robot = StationBuilder.Group(transform, "Robot", new Vector3(0.55f, 0, 1.4f));
            BuildRobot(_robot);
            BuildStands(_robot);
            BuildFence(StationBuilder.Group(transform, "Fence", Vector3.zero));
            BuildController(StationBuilder.Group(transform, "Controller", new Vector3(2.3f, 0, 1.0f)));
            BuildPanel(StationBuilder.Group(transform, "Job Panel", new Vector3(-1.45f, 1.55f, 0.85f)));
            SetLights(green: false, amber: false, red: false);
            Pose(Home);
            Show("Load a job file to start the robot.");
        }

        private IEnumerator Load(string fileName)
        {
            _loading = true;
            Stop();
            Show($"Loading {fileName}…");
            var url = $"{Application.streamingAssetsPath}/RobotJobs/{fileName}";
            if (!url.Contains("://")) url = "file://" + url;

            using (var request = UnityWebRequest.Get(url))
            {
                yield return request.SendWebRequest();
                _loading = false;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail(fileName, request.error);
                    yield break;
                }
                Parse(fileName, request.downloadHandler.text);
            }
        }

        private void Parse(string fileName, string json)
        {
            var job = new JobFile();
            try
            {
                JsonUtility.FromJsonOverwrite(json, job);
            }
            catch (ArgumentException exception)
            {
                Fail(fileName, exception.Message);
                return;
            }

            var usedDefault = job.speed_pct == JobFile.Missing;
            var result = new JobResult
            {
                FileName = fileName,
                Job = job,
                UsedDefaultSpeed = usedDefault,
                SpeedPct = usedDefault ? DefaultSpeedPct : job.speed_pct,
            };

            SetLights(green: true, amber: usedDefault, red: false);
            Show($"<color=#05DA98><b>Running</b></color>  {job.program} from {fileName}\n<size=85%>Speed {result.SpeedPct:0}%" +
                 (usedDefault ? " <color=#F5B841>(default: the file sets no speed)</color>" : "") + "</size>",
                usedDefault ? "Logged a <b>warning</b> for the missing speed_pct, then <b>info</b> for the load."
                    : "Logged <b>info</b> for the load. The file is complete, so there's nothing to warn about.");
            _program = StartCoroutine(Run(result.SpeedPct));
            JobLoaded?.Invoke(result);
            SampleEvents.ReportFinished(SampleOutcome.Pass);
        }

        private void Fail(string fileName, string error)
        {
            SetLights(green: false, amber: false, red: true);
            Show($"<color=#F5B841><b>Job failed to load</b></color>\n<size=80%>{fileName} couldn't be read. The robot stays stopped.</size>",
                "Logged an <b>error</b> with the file name and the parser's message.");
            JobFailed?.Invoke(fileName, error);
            SampleEvents.ReportFinished(SampleOutcome.Fail);
        }

        private void Stop()
        {
            if (_program != null) StopCoroutine(_program);
            _program = null;
            if (_part.parent != _robot)
            {
                // A part still in the gripper goes back on the nearer stand.
                _part.SetParent(_robot, false);
                _part.localPosition = Stations[Vector3.Distance(_grasp, Stations[0]) < Vector3.Distance(_grasp, Stations[1]) ? 0 : 1];
                _part.localRotation = Quaternion.identity;
            }
            SetGrip(false);
        }

        /// <summary>Moves the part back and forth between the two stands, at the job's speed.</summary>
        private IEnumerator Run(float speedPct)
        {
            var speed = MaxSpeed * speedPct / 100f;
            var from = Vector3.Distance(_part.localPosition, Stations[0]) < 0.01f ? 0 : 1;
            while (true)
            {
                var pick = Stations[from];
                var place = Stations[1 - from];
                yield return Move(pick + Approach, speed);
                yield return Move(pick, speed);
                yield return Grip(true);
                _part.SetParent(_wrist, true);
                yield return Move(pick + Approach, speed);
                yield return Move(place + Approach, speed);
                yield return Move(place, speed);
                yield return Grip(false);
                _part.SetParent(_robot, true);
                _part.localPosition = place;
                _part.localRotation = Quaternion.identity;
                yield return Move(place + Approach, speed);
                from = 1 - from;
            }
        }

        /// <summary>Moves the grasp point along an arc around the base, so the waist swings smoothly.</summary>
        private IEnumerator Move(Vector3 target, float speed)
        {
            var start = _grasp;
            var duration = Mathf.Max(0.15f, Vector3.Distance(start, target) / speed);
            var a0 = Mathf.Atan2(start.z, start.x);
            var a1 = Mathf.Atan2(target.z, target.x);
            var r0 = new Vector2(start.x, start.z).magnitude;
            var r1 = new Vector2(target.x, target.z).magnitude;
            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var s = Mathf.SmoothStep(0, 1, t);
                var angle = Mathf.LerpAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg, s) * Mathf.Deg2Rad;
                var radius = Mathf.Lerp(r0, r1, s);
                Pose(new Vector3(radius * Mathf.Cos(angle), Mathf.Lerp(start.y, target.y, s), radius * Mathf.Sin(angle)));
                yield return null;
            }
            Pose(target);
        }

        private IEnumerator Grip(bool closed)
        {
            yield return new WaitForSeconds(0.1f);
            SetGrip(closed);
            yield return new WaitForSeconds(0.2f);
        }

        private void SetGrip(bool closed)
        {
            var z = closed ? PartSize / 2f + 0.006f : FingerOpen;
            var position = _fingerLeft.localPosition;
            _fingerLeft.localPosition = new Vector3(position.x, position.y, -z);
            _fingerRight.localPosition = new Vector3(position.x, position.y, z);
        }

        /// <summary>
        /// Solves the arm for a grasp point in the robot's own space, with the gripper pointing straight down: the waist
        /// turns to face it, then the lower and upper arm reach it as a two-link chain.
        /// </summary>
        private void Pose(Vector3 grasp)
        {
            _grasp = grasp;
            var yaw = Mathf.Atan2(-grasp.z, grasp.x) * Mathf.Rad2Deg;
            var reach = new Vector2(grasp.x, grasp.z).magnitude - ShoulderOffset;
            var height = grasp.y + GraspLength - ShoulderHeight;
            var distance = Mathf.Clamp(Mathf.Sqrt(reach * reach + height * height), Mathf.Abs(LowerArm - UpperArm) + 0.001f, LowerArm + UpperArm - 0.001f);
            var lean = Mathf.Atan2(reach, height) - Mathf.Acos((LowerArm * LowerArm + distance * distance - UpperArm * UpperArm) / (2f * LowerArm * distance));
            var elbow = new Vector2(LowerArm * Mathf.Sin(lean), LowerArm * Mathf.Cos(lean));
            var forearm = Mathf.Atan2(reach - elbow.x, height - elbow.y) * Mathf.Rad2Deg;
            lean *= Mathf.Rad2Deg;

            // Angles lean from vertical toward the arm's reach (+x in the zero pose), which is a negative turn about z.
            _waist.localRotation = Quaternion.Euler(0, yaw, 0);
            _lower.localRotation = Quaternion.Euler(0, 0, -lean);
            _upper.localRotation = Quaternion.Euler(0, 0, 90f + lean - forearm);
            _wrist.localRotation = Quaternion.Euler(0, 0, forearm - 180f);
        }

        private void SetLights(bool green, bool amber, bool red)
        {
            StationBuilder.Glow(_green, PropColors.SafetyGreen, green);
            StationBuilder.Glow(_amber, UiKit.Amber, amber);
            StationBuilder.Glow(_red, PropColors.SafetyRed, red);
        }

        private void Show(string status, string logged = null) =>
            _screen.text = $"<b>ROBOT CELL 2 · JOB LOADER</b>\n{status}" +
                           (logged == null ? "" : $"\n\n<size=80%>{logged} See <b>Logs</b> in the Insights Stream.</size>");

        private void Choose(string fileName)
        {
            if (!_loading) StartCoroutine(Load(fileName));
        }

        private void BuildRobot(Transform root)
        {
            if (robotModel != null)
            {
                // The model's parts sit flat under its root at their joint pivots; chain them so each joint carries the next.
                var model = StationBuilder.Model(root, "Robot Model", robotModel, Vector3.zero, Vector3.one).transform;
                _waist = model.Find("Robot Waist");
                _lower = model.Find("Robot Lower Arm");
                _upper = model.Find("Robot Upper Arm");
                _wrist = model.Find("Robot Wrist");
                _fingerLeft = model.Find("Robot Finger L");
                _fingerRight = model.Find("Robot Finger R");
                _lower.SetParent(_waist, true);
                _upper.SetParent(_lower, true);
                _wrist.SetParent(_upper, true);
                _fingerLeft.SetParent(_wrist, true);
                _fingerRight.SetParent(_wrist, true);
            }
            else
            {
                Block(root, "Base", new Vector3(0, 0.15f, 0), new Vector3(0.36f, 0.3f, 0.36f), PropColors.Panel);
                _waist = StationBuilder.Group(root, "Waist", new Vector3(0, 0.3f, 0));
                Block(_waist, "Turret", new Vector3(0.03f, 0.17f, 0), new Vector3(0.3f, 0.34f, 0.26f), PropColors.Panel);
                _lower = StationBuilder.Group(_waist, "Lower Arm", new Vector3(ShoulderOffset, ShoulderHeight - 0.3f, 0));
                Block(_lower, "Link", new Vector3(0, LowerArm / 2f, 0), new Vector3(0.14f, LowerArm + 0.1f, 0.16f), PropColors.Panel);
                _upper = StationBuilder.Group(_lower, "Upper Arm", new Vector3(0, LowerArm, 0));
                Block(_upper, "Link", new Vector3(UpperArm / 2f, 0, 0), new Vector3(UpperArm + 0.1f, 0.12f, 0.12f), PropColors.Panel);
                _wrist = StationBuilder.Group(_upper, "Wrist", new Vector3(UpperArm, 0, 0));
                Block(_wrist, "Gripper", new Vector3(0.1f, 0, 0), new Vector3(0.1f, 0.06f, 0.12f), PropColors.DarkSteel);
                _fingerLeft = Block(_wrist, "Finger", new Vector3(0.17f, 0, -FingerOpen), new Vector3(0.06f, 0.03f, 0.012f), PropColors.Rubber).transform;
                _fingerRight = Block(_wrist, "Finger", new Vector3(0.17f, 0, FingerOpen), new Vector3(0.06f, 0.03f, 0.012f), PropColors.Rubber).transform;
            }
        }

        private void BuildStands(Transform root)
        {
            foreach (var station in Stations)
            {
                var foot = new Vector3(station.x, 0, station.z);
                Block(root, "Stand", foot + new Vector3(0, StandHeight / 2f - 0.01f, 0), new Vector3(0.08f, StandHeight - 0.02f, 0.08f), PropColors.DarkSteel);
                Block(root, "Stand Foot", foot + new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.02f, 0.3f), PropColors.DarkSteel);
                Block(root, "Fixture", foot + new Vector3(0, StandHeight - 0.01f, 0), new Vector3(0.22f, 0.02f, 0.22f), PropColors.Steel);
                Block(root, "Locator", foot + new Vector3(0, StandHeight + 0.004f, 0), new Vector3(PartSize + 0.02f, 0.008f, PartSize + 0.02f), PropColors.SafetyYellow);
            }
            _part = Block(root, "Part", Stations[0], Vector3.one * PartSize, UiKit.FromHex(0x2F5DA8)).transform;
        }

        private void BuildFence(Transform root)
        {
            // Mesh fencing behind and beside the robot; the operator's controls stay outside it.
            const float panel = 1.2f;
            // The side runs end on the back run's corner posts.
            var runs = new[]
            {
                (start: new Vector3(-0.65f, 0, 2.35f), turn: 0f, panels: 2, lastPost: true),
                (start: new Vector3(-0.65f, 0, 1.15f), turn: -90f, panels: 1, lastPost: false),
                (start: new Vector3(1.75f, 0, 1.15f), turn: -90f, panels: 1, lastPost: false),
            };
            foreach (var run in runs)
            {
                var rotation = Quaternion.Euler(0, run.turn, 0);
                for (var i = 0; i <= (run.lastPost ? run.panels : run.panels - 1); i++)
                {
                    var position = run.start + rotation * new Vector3(i * panel, 0, 0);
                    if (fenceModel == null)
                    {
                        Block(root, "Fence Post", position + new Vector3(0, 1f, 0), new Vector3(0.06f, 2f, 0.06f), PropColors.SafetyYellow);
                        continue;
                    }
                    var module = StationBuilder.Model(root, "Fence", fenceModel, position, Vector3.one).transform;
                    module.localRotation = rotation;
                    if (i == run.panels) module.Find("Fence Panel").gameObject.SetActive(false);
                }
            }
        }

        private void BuildController(Transform root)
        {
            if (controllerModel != null)
                StationBuilder.Model(root, "Controller Model", controllerModel, Vector3.zero, Vector3.one);
            else
                Block(root, "Cabinet", new Vector3(0, 0.63f, 0), new Vector3(0.6f, 1.26f, 0.4f), PropColors.Panel);

            var light = new Vector3(-0.42f, 0, 0.05f);
            if (stackLightModel != null)
            {
                var stack = StationBuilder.Model(root, "Stack Light", stackLightModel, light, Vector3.one).transform;
                _green = stack.Find("Lens Green").gameObject;
                _amber = stack.Find("Lens Amber").gameObject;
                _red = stack.Find("Lens Red").gameObject;
            }
            else
            {
                Block(root, "Stack Post", light + new Vector3(0, 0.55f, 0), new Vector3(0.04f, 1.1f, 0.04f), PropColors.DarkSteel);
                _green = Block(root, "Green", light + new Vector3(0, 1.14f, 0), new Vector3(0.09f, 0.07f, 0.09f), PropColors.Off);
                _amber = Block(root, "Amber", light + new Vector3(0, 1.21f, 0), new Vector3(0.09f, 0.07f, 0.09f), PropColors.Off);
                _red = Block(root, "Red", light + new Vector3(0, 1.28f, 0), new Vector3(0.09f, 0.07f, 0.09f), PropColors.Off);
            }
        }

        private void BuildPanel(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.3f, 0.8f), material);
            _screen = StationBuilder.Text(root, "", new Vector3(0, 0.12f, -0.025f), 0.46f, Color.white, 1.18f, 0.45f, TextAlignmentOptions.TopLeft);
            for (var i = 0; i < Files.Length; i++)
            {
                var fileName = Files[i];
                var button = StationBuilder.Button(root, $"Load {fileName}", new Vector3(-0.42f + i * 0.42f, -0.26f, -0.03f), new Vector2(0.38f, 0.12f), material, UiKit.Purple, 0.32f);
                button.Activated += _ => Choose(fileName);
                _fileButtons[fileName] = button;
            }
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
