using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.UserData
{
    /// <summary>An employee from the site's own directory (fictional).</summary>
    public sealed class Employee
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public string SiteId;
    }

    /// <summary>
    /// A shared-headset cart with a badge reader. Employees tap their badge before training; visitor badges
    /// aren't in the employee directory.
    /// </summary>
    public class BadgeReader : MonoBehaviour
    {
        private static readonly Employee[] Directory =
        {
            new Employee { Id = "E-1042", FirstName = "Sam", LastName = "Rivera", SiteId = "denver_dc" },
            new Employee { Id = "E-2077", FirstName = "Priya", LastName = "Shah", SiteId = "leeds_dc" },
        };

        private static readonly string[] Badges = { "E-1042", "E-2077", "V-311" };

        [SerializeField] private Material material;
        [SerializeField] private GameObject cartModel;
        [SerializeField] private GameObject headsetModel;
        [SerializeField] private GameObject badgeModel;

        private readonly Dictionary<string, StationInteractable> _badges = new Dictionary<string, StationInteractable>();
        private TextMeshPro _screen;
        private string _welcomed;

        public event Action<Employee> EmployeeIdentified;
        public event Action UserDataCheckRequested;

        public StationInteractable CheckUserDataButton { get; private set; }

        public StationInteractable Badge(string badgeId) => _badges[badgeId];

        private void Awake()
        {
            BuildCart(StationBuilder.Group(transform, "Headset Cart", new Vector3(0.8f, 0, 0.5f)));
            BuildReader(StationBuilder.Group(transform, "Badge Reader", new Vector3(-0.2f, 0, 0.45f)));
            BuildBadges(StationBuilder.Group(transform, "Badges", new Vector3(-0.2f, 0.9f, -0.15f)));
            _screen.text = "<b>SHARED HEADSETS</b>\nTap your badge to start training.";
        }

        public void Welcome(Employee employee)
        {
            _welcomed = employee.Id;
            _screen.text = $"<b>Welcome, {employee.FirstName}</b>\n<size=80%>Headset 3 is yours. Training starts when you put it on.</size>";
            SampleEvents.ReportFinished(SampleOutcome.Pass);
        }

        /// <summary>Shows what the SDK knows about the user: GetUserId and GetUserData (null when not signed in).</summary>
        public void ShowUserData(string userId, IReadOnlyDictionary<string, string> userData)
        {
            var text = new StringBuilder("<b>USER DATA</b>\n<size=75%>");
            text.Append($"GetUserId(): <b>{userId ?? "null"}</b>\n");
            if (userData == null) text.Append("GetUserData(): <b>null</b> (not signed in)");
            else if (userData.Count == 0) text.Append("GetUserData(): <b>empty</b>");
            else text.Append("GetUserData(): ").Append(string.Join(", ", userData.Select(pair => $"{pair.Key} <b>{pair.Value}</b>")));
            if (userData != null && _welcomed != null && userId != _welcomed)
                text.Append($"\n<color=#F5B841>GetUserId() isn't {_welcomed}: the sync hasn't finished, was skipped (the tap came during another), or failed. The SDK log says which.</color>");
            _screen.text = text.Append("</size>").ToString();
        }

        private void Tap(string badgeId)
        {
            var employee = Directory.FirstOrDefault(entry => entry.Id == badgeId);
            if (employee != null)
            {
                EmployeeIdentified?.Invoke(employee);
                return;
            }

            _screen.text = $"<color=#F5B841><b>Badge {badgeId} isn't an employee badge.</b></color>\n<size=80%>Visitors train with a supervisor.</size>";
            SampleEvents.ReportFinished(SampleOutcome.Fail);
        }

        private void BuildCart(Transform root)
        {
            if (cartModel != null)
            {
                var cart = StationBuilder.Model(root, "Cart Model", cartModel, Vector3.zero, Vector3.one).transform;
                for (var i = 0; i < 4; i++)
                {
                    if (headsetModel != null)
                        StationBuilder.Model(root, "Headset", headsetModel, new Vector3(-0.36f + i * 0.24f, 0.915f, -0.06f), Vector3.one);
                    StationBuilder.Glow(cart.Find($"Bay Light {i + 1}").gameObject, PropColors.SafetyGreen, true);
                }
                StationBuilder.Text(root, "<b>SHARED HEADSETS</b>", new Vector3(0, 0.62f, -0.262f), 0.36f, UiKit.Ink, 0.9f, 0.1f);
                return;
            }

            Block(root, "Cart", new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.82f, 0.5f), PropColors.Steel);
            Block(root, "Top", new Vector3(0, 0.88f, 0), new Vector3(1.04f, 0.04f, 0.54f), PropColors.DarkSteel);
            for (var i = 0; i < 4; i++)
            {
                var x = -0.36f + i * 0.24f;
                Block(root, "Headset", new Vector3(x, 0.97f, 0), new Vector3(0.18f, 0.1f, 0.2f), PropColors.Rubber);
                Block(root, "Strap", new Vector3(x, 1.04f, 0.05f), new Vector3(0.16f, 0.04f, 0.1f), PropColors.Off);
                Block(root, "Charging Light", new Vector3(x + 0.07f, 0.97f, -0.101f), new Vector3(0.02f, 0.02f, 0.004f), PropColors.SafetyGreen);
            }
            StationBuilder.Text(root, "<b>SHARED HEADSETS</b>", new Vector3(0, 0.62f, -0.255f), 0.36f, UiKit.Ink, 0.9f, 0.1f);
        }

        private void BuildReader(Transform root)
        {
            StationBuilder.Kiosk(root, new Vector3(0, 1.42f, 0), new Vector3(0.9f, 0.64f, 0.06f), new Vector3(0, 1.5f, -0.031f), new Vector2(0.84f, 0.42f), material);
            _screen = StationBuilder.Text(root, "", new Vector3(0, 1.5f, -0.036f), 0.5f, Color.white, 0.78f, 0.36f);
            CheckUserDataButton = StationBuilder.Button(root, "Check user data", new Vector3(0.18f, 1.205f, -0.04f), new Vector2(0.4f, 0.09f), material, UiKit.Purple, 0.3f);
            Block(root, "Reader Pad", new Vector3(-0.27f, 1.205f, -0.04f), new Vector3(0.2f, 0.09f, 0.03f), PropColors.Off);
            Block(root, "Reader Light", new Vector3(-0.27f, 1.205f, -0.057f), new Vector3(0.04f, 0.04f, 0.004f), UiKit.Purple);
            CheckUserDataButton.Activated += _ => UserDataCheckRequested?.Invoke();
        }

        private void BuildBadges(Transform root)
        {
            Block(root, "Tray", new Vector3(0, -0.02f, 0.02f), new Vector3(1.0f, 0.03f, 0.2f), PropColors.DarkSteel);
            Block(root, "Tray Leg", new Vector3(0, -0.46f, 0.02f), new Vector3(0.06f, 0.86f, 0.06f), PropColors.DarkSteel);
            for (var i = 0; i < Badges.Length; i++)
            {
                var id = Badges[i];
                var employee = Directory.FirstOrDefault(entry => entry.Id == id);
                var badge = StationBuilder.Group(root, $"Badge {id}", new Vector3(-0.32f + i * 0.32f, 0.1f, 0));
                var stripe = employee != null ? UiKit.Purple : PropColors.SafetyYellow;
                GameObject card;
                if (badgeModel != null)
                {
                    var model = StationBuilder.Model(badge, "Badge Model", badgeModel, Vector3.zero, Vector3.one).transform;
                    card = model.Find("Badge Card").gameObject;
                    StationBuilder.Tint(model.Find("Badge Stripe").gameObject, stripe);
                }
                else
                {
                    card = Block(badge, "Card", Vector3.zero, new Vector3(0.24f, 0.17f, 0.01f), Color.white);
                    Block(badge, "Stripe", new Vector3(0, 0.06f, -0.006f), new Vector3(0.24f, 0.04f, 0.002f), stripe);
                }
                var label = employee != null ? $"<b>{employee.FirstName} {employee.LastName}</b>\n{id}" : $"<b>VISITOR</b>\n{id}";
                StationBuilder.Text(badge, label, new Vector3(0, -0.015f, -0.007f), 0.26f, UiKit.Ink, 0.22f, 0.1f);
                var interactable = StationBuilder.Interactable(badge.gameObject, $"Badge {id}", new Vector3(0.26f, 0.19f, 0.04f), card, Color.white);
                interactable.Activated += _ => Tap(id);
                _badges[id] = interactable;
            }
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
