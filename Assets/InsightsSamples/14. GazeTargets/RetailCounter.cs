using System;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.GazeTargets
{
    /// <summary>
    /// A shop counter with a customer waiting, a product shelf, and a phone lying on the counter. On desktop the
    /// centre of the view stands in for gaze: right-drag (or drag a finger) to look around, then greet the customer.
    /// </summary>
    public class RetailCounter : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private GameObject counterModel;
        [SerializeField] private GameObject shelfModel;
        [SerializeField] private GameObject customerModel;
        [SerializeField] private GameObject posModel;
        [SerializeField] private GameObject phoneModel;
        [SerializeField] private RuntimeAnimatorController customerAnimations;

        private TextMeshPro _sign;
        private Animator _customer;

        public event Action Greeted;

        public GameObject CustomerFace { get; private set; }
        public GameObject ProductShelf { get; private set; }
        public GameObject Phone { get; private set; }
        public StationInteractable GreetButton { get; private set; }

        private void Awake()
        {
            BuildCounter(StationBuilder.Group(transform, "Counter", new Vector3(0, 0, 0.35f)));
            BuildCustomer(StationBuilder.Group(transform, "Customer", new Vector3(0.05f, 0, 1.0f)));
            BuildShelf(StationBuilder.Group(transform, "Product Shelf", new Vector3(1.45f, 0, 1.15f)));
            BuildSign(StationBuilder.Group(transform, "Sign", new Vector3(-1.2f, 1.75f, 1.2f)));
            Show("Right-drag, or drag a finger, to look around. Greet the customer when you're ready.");
        }

        private void Greet()
        {
            Greeted?.Invoke();
            if (_customer != null) _customer.CrossFadeInFixedTime("Greet", 0.2f);

            // Desktop gaze is the centre of the view: find what it points at most directly.
            var view = Camera.main.transform;
            var lookedAt = Closest(view, (CustomerFace, "the customer"), (ProductShelf, "the shelf"), (Phone, "your phone"));
            var passed = lookedAt.prop == CustomerFace;
            Show(passed ? "<color=#05DA98><b>You greeted the customer while looking at them.</b></color>"
                : $"<color=#F5B841><b>You greeted the customer while looking at {lookedAt.name}.</b></color>");
            SampleEvents.ReportFinished(passed ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private static (GameObject prop, string name) Closest(Transform view, params (GameObject prop, string name)[] candidates)
        {
            var best = candidates[0];
            var bestDot = float.MinValue;
            foreach (var candidate in candidates)
            {
                var dot = Vector3.Dot(view.forward, (candidate.prop.transform.position - view.position).normalized);
                if (dot > bestDot) (best, bestDot) = (candidate, dot);
            }
            return best;
        }

        private void Show(string text) => _sign.text = $"<b>STORE 12 · CHECKOUT</b>\n<size=82%>{text}</size>";

        private void BuildCounter(Transform root)
        {
            // Models here import without colliders: AbxrTarget's line-of-sight check would count them as blocking.
            if (counterModel != null)
                StationBuilder.Model(root, "Counter Model", counterModel, Vector3.zero, Vector3.one);
            else
            {
                Block(root, "Front", new Vector3(0, 0.48f, 0), new Vector3(1.8f, 0.96f, 0.55f), PropColors.Panel);
                Block(root, "Top", new Vector3(0, 0.98f, 0), new Vector3(1.86f, 0.04f, 0.6f), PropColors.Wood);
            }
            if (posModel != null)
            {
                var pos = StationBuilder.Model(root, "POS Terminal", posModel, new Vector3(-0.55f, 1.0f, 0.1f), Vector3.one).transform;
                StationBuilder.Tint(pos.Find("POS Screen").gameObject, UiKit.Navy);
            }
            else
                Block(root, "Register", new Vector3(-0.55f, 1.12f, 0.1f), new Vector3(0.36f, 0.24f, 0.3f), PropColors.DarkSteel);

            Phone = StationBuilder.Group(root, "Phone", new Vector3(0.45f, 1.005f, -0.12f)).gameObject;
            if (phoneModel != null)
            {
                var phone = StationBuilder.Model(Phone.transform, "Phone Model", phoneModel, new Vector3(0, -0.006f, 0), Vector3.one).transform;
                StationBuilder.Tint(phone.Find("Phone Screen").gameObject, UiKit.FromHex(0x5A9BFF));
            }
            else
            {
                Block(Phone.transform, "Body", Vector3.zero, new Vector3(0.08f, 0.012f, 0.16f), PropColors.Rubber);
                Block(Phone.transform, "Screen", new Vector3(0, 0.007f, 0), new Vector3(0.068f, 0.002f, 0.14f), UiKit.FromHex(0x5A9BFF));
            }

            GreetButton = StationBuilder.Button(root, "Greet the customer", new Vector3(-0.05f, 1.04f, -0.29f), new Vector2(0.5f, 0.11f), material, UiKit.Purple, 0.34f);
            GreetButton.transform.localRotation = Quaternion.Euler(30f, 0, 0);
            GreetButton.Activated += _ => Greet();
        }

        private void BuildCustomer(Transform root)
        {
            if (customerModel != null)
            {
                // The model faces +z; she faces the counter and the learner.
                var model = StationBuilder.Model(root, "Customer Model", customerModel, Vector3.zero, Vector3.one).transform;
                model.localRotation = Quaternion.Euler(0, 180f, 0);
                _customer = model.GetComponent<Animator>();
                _customer.runtimeAnimatorController = customerAnimations;

                // The gaze target sits between her eyes and rides on the head bone, so it follows her nods and glances.
                var eyes = model.Find("Customer Eyes").GetComponent<SkinnedMeshRenderer>();
                CustomerFace = new GameObject("Face");
                CustomerFace.transform.position = eyes.transform.TransformPoint(eyes.sharedMesh.bounds.center);
                CustomerFace.transform.SetParent(FindBone(model, "head"), true);
                return;
            }

            Block(root, "Legs", new Vector3(0, 0.45f, 0), new Vector3(0.34f, 0.9f, 0.22f), PropColors.Rubber);
            Block(root, "Body", new Vector3(0, 1.22f, 0), new Vector3(0.46f, 0.64f, 0.26f), UiKit.FromHex(0x2F6FDE));
            Block(root, "Neck", new Vector3(0, 1.58f, 0), new Vector3(0.1f, 0.08f, 0.1f), UiKit.FromHex(0xC68E6A));
            CustomerFace = StationBuilder.Group(root, "Head", new Vector3(0, 1.74f, 0)).gameObject;
            Block(CustomerFace.transform, "Head", Vector3.zero, new Vector3(0.22f, 0.26f, 0.22f), UiKit.FromHex(0xC68E6A));
            Block(CustomerFace.transform, "Hair", new Vector3(0, 0.12f, 0.02f), new Vector3(0.23f, 0.06f, 0.23f), UiKit.FromHex(0x3B2A20));
            foreach (var x in new[] { -0.05f, 0.05f })
                Block(CustomerFace.transform, "Eye", new Vector3(x, 0.02f, -0.111f), new Vector3(0.025f, 0.025f, 0.004f), PropColors.Rubber);
            Block(CustomerFace.transform, "Smile", new Vector3(0, -0.06f, -0.111f), new Vector3(0.07f, 0.012f, 0.004f), UiKit.FromHex(0x8A4B3A));
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var bone in root.GetComponentsInChildren<Transform>())
                if (bone.name == name) return bone;
            return root;
        }

        private void BuildShelf(Transform root)
        {
            ProductShelf = StationBuilder.Group(root, "Shelf Centre", new Vector3(0, 1.0f, 0)).gameObject;
            if (shelfModel != null)
            {
                StationBuilder.Model(root, "Shelf Model", shelfModel, Vector3.zero, Vector3.one);
                root.localRotation = Quaternion.Euler(0, -25f, 0);
                return;
            }
            foreach (var x in new[] { -0.4f, 0.4f })
                Block(root, "Upright", new Vector3(x, 0.9f, 0), new Vector3(0.04f, 1.8f, 0.36f), PropColors.Steel);
            var colors = new[] { UiKit.FromHex(0xE8761E), UiKit.FromHex(0x2E9E5B), UiKit.FromHex(0x5A58EB), PropColors.SafetyYellow };
            for (var shelf = 0; shelf < 3; shelf++)
            {
                var y = 0.5f + shelf * 0.5f;
                Block(root, "Shelf", new Vector3(0, y, 0), new Vector3(0.84f, 0.03f, 0.36f), PropColors.Steel);
                for (var item = 0; item < 4; item++)
                    Block(root, "Product", new Vector3(-0.3f + item * 0.2f, y + 0.12f, 0), new Vector3(0.14f, 0.21f, 0.14f), colors[(item + shelf) % colors.Length]);
            }
            root.localRotation = Quaternion.Euler(0, -25f, 0);
        }

        private void BuildSign(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.24f, 0.44f), material, stand: true);
            _sign = StationBuilder.Text(root, "", new Vector3(0, 0, -0.025f), 0.52f, Color.white, 1.2f, 0.42f, TextAlignmentOptions.TopLeft);
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
