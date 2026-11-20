using System;
using System.Collections.Generic;
using System.Text;
using InsightsSamples.Common;
using TMPro;
using UnityEngine;

namespace InsightsSamples.TimedEvents
{
    /// <summary>
    /// A warehouse pick face: six bins, a tote, and an order ticket for three items. Accept the order, pick
    /// each item into the tote, then scan out.
    /// </summary>
    public class PickFace : MonoBehaviour
    {
        private static readonly (string location, string item, Color color)[] Bins =
        {
            ("A1", "Safety glasses", UiKit.FromHex(0xBFD9E6)),
            ("A2", "Hi-vis vest", UiKit.FromHex(0xC6E83A)),
            ("A3", "Ear defenders", PropColors.SafetyRed),
            ("B1", "Hard hat", PropColors.SafetyYellow),
            ("B2", "Work gloves", UiKit.FromHex(0x8C6A4A)),
            ("B3", "Dust masks", Color.white),
        };

        private static readonly int[] Order = { 1, 4, 2 };

        [SerializeField] private Material material;
        [SerializeField] private GameObject shelvingModel;
        [SerializeField] private GameObject binModel;
        [SerializeField] private GameObject cartModel;
        [SerializeField] private GameObject toteModel;
        [Tooltip("One model per bin, in the order of Bins.")]
        [SerializeField] private GameObject[] itemModels;

        private readonly StationInteractable[] _bins = new StationInteractable[Bins.Length];
        private readonly List<GameObject> _tote = new List<GameObject>();
        private readonly bool[] _picked = new bool[Order.Length];
        private Transform _toteRoot;
        private TextMeshPro _ticket;
        private bool _picking;
        private int _mispicks;
        private string _message = "Click <b>Accept order</b> to start picking.";

        public event Action OrderAccepted;
        public event Action<int, int> ScannedOut;

        public StationInteractable AcceptButton { get; private set; }
        public StationInteractable ScanOutButton { get; private set; }

        /// <summary>Bins by index: 0-2 are A1-A3 (top shelf), 3-5 are B1-B3.</summary>
        public StationInteractable Bin(int index) => _bins[index];

        /// <summary>The bins on the order ticket, in ticket order.</summary>
        public IReadOnlyList<int> OrderedBins => Order;

        private void Awake()
        {
            BuildShelving(StationBuilder.Group(transform, "Shelving", new Vector3(-0.25f, 0, 0.8f)));
            BuildCart(StationBuilder.Group(transform, "Tote Cart", new Vector3(0.9f, 0, 0.1f)));
            BuildTicket(StationBuilder.Group(transform, "Order Ticket", new Vector3(0.25f, 2.0f, 1.05f)));
            Refresh();
        }

        private void Accept()
        {
            foreach (var item in _tote) Destroy(item);
            _tote.Clear();
            Array.Clear(_picked, 0, _picked.Length);
            _mispicks = 0;
            _picking = true;
            _message = "Pick each line into the tote, then click <b>Scan out</b>.";
            Refresh();
            OrderAccepted?.Invoke();
        }

        private void Pick(int bin)
        {
            if (!_picking) return;
            var line = Array.IndexOf(Order, bin);
            if (line >= 0 && !_picked[line]) _picked[line] = true;
            else _mispicks++;

            var slot = _tote.Count;
            if (HasItemModels)
            {
                // Picks land in the tote a little smaller than life, so a few fit side by side.
                var position = new Vector3(-0.13f + slot % 3 * 0.13f, 0.012f + slot / 6 * 0.06f, -0.08f + slot / 3 % 2 * 0.16f);
                var item = StationBuilder.Model(_toteRoot, Bins[bin].item, itemModels[bin], position, Vector3.one * 0.6f);
                item.transform.localRotation = Quaternion.Euler(0, slot * 37f, 0);
                _tote.Add(item);
            }
            else
            {
                var position = new Vector3(-0.14f + slot % 3 * 0.12f, 0.565f + slot / 6 * 0.05f, -0.07f + slot / 3 % 2 * 0.14f);
                _tote.Add(Block(_toteRoot, Bins[bin].item, position, new Vector3(0.1f, 0.05f, 0.08f), Bins[bin].color));
            }
            Refresh();
        }

        private void ScanOut()
        {
            if (!_picking) return;
            _picking = false;
            var complete = Array.TrueForAll(_picked, picked => picked);
            var exact = complete && _mispicks == 0;
            _message = exact ? "<color=#05DA98><b>Scanned out: order picked exactly.</b></color>"
                : $"<color=#F5B841><b>Scanned out with {(complete ? "" : "missing items and ")}{_mispicks} {(_mispicks == 1 ? "mispick" : "mispicks")}.</b></color>";
            StationBuilder.SetLabel(AcceptButton, "Next order");
            Refresh();

            ScannedOut?.Invoke(_tote.Count, _mispicks);
            SampleEvents.ReportFinished(exact ? SampleOutcome.Pass : SampleOutcome.Fail);
        }

        private void Refresh()
        {
            foreach (var bin in _bins) bin.Interactable = _picking;
            AcceptButton.gameObject.SetActive(!_picking);
            ScanOutButton.gameObject.SetActive(_picking);

            var text = new StringBuilder("<b>ORDER 4471</b>  ·  3 lines\n<size=85%>");
            for (var line = 0; line < Order.Length; line++)
            {
                var (location, item, _) = Bins[Order[line]];
                text.Append(_picked[line] ? $"<color=#05DA98>{location}  {item} × 1  · picked</color>\n" : $"{location}  {item} × 1\n");
            }
            text.Append($"</size><size=75%>{_message}</size>");
            _ticket.text = text.ToString();
        }

        private void BuildShelving(Transform root)
        {
            if (shelvingModel != null)
                StationBuilder.Model(root, "Shelving Model", shelvingModel, Vector3.zero, Vector3.one);
            else
            {
                foreach (var x in new[] { -0.72f, 0.72f })
                    Block(root, "Upright", new Vector3(x, 0.8f, 0), new Vector3(0.05f, 1.6f, 0.45f), PropColors.SafetyYellow);
                foreach (var y in new[] { 0.62f, 1.12f })
                    Block(root, "Shelf", new Vector3(0, y, 0), new Vector3(1.44f, 0.03f, 0.45f), PropColors.Steel);
            }

            for (var index = 0; index < Bins.Length; index++)
            {
                var (location, item, color) = Bins[index];
                var row = index / 3;
                var bin = StationBuilder.Group(root, $"Bin {location}", new Vector3(-0.45f + index % 3 * 0.45f, row == 0 ? 1.25f : 0.75f, -0.04f));
                GameObject body;
                if (binModel != null)
                {
                    // An open-front picking bin; its label sits in the holder on the low front lip.
                    body = StationBuilder.Model(bin, "Bin", binModel, Vector3.zero, Vector3.one);
                    if (HasItemModels)
                        StationBuilder.Model(bin, item, itemModels[index], new Vector3(0, -0.08f, 0.04f), Vector3.one);
                    else
                        Block(bin, item, new Vector3(0, -0.04f, 0.04f), new Vector3(0.24f, 0.08f, 0.18f), color);
                    Block(bin, "Label", new Vector3(0, -0.06f, -0.1845f), new Vector3(0.3f, 0.07f, 0.002f), Color.white);
                    StationBuilder.Text(bin, $"<b>{location}</b>  {item}", new Vector3(0, -0.06f, -0.187f), 0.26f, UiKit.Ink, 0.29f, 0.065f);
                }
                else
                {
                    body = Block(bin, "Bin", Vector3.zero, new Vector3(0.4f, 0.22f, 0.36f), UiKit.FromHex(0x2F5DA8));
                    Block(bin, item, new Vector3(0, 0.1f, 0.04f), new Vector3(0.24f, 0.08f, 0.18f), color);
                    Block(bin, "Label", new Vector3(0, -0.02f, -0.181f), new Vector3(0.38f, 0.13f, 0.004f), Color.white);
                    StationBuilder.Text(bin, $"<b>{location}</b>  {item}", new Vector3(0, -0.02f, -0.186f), 0.34f, UiKit.Ink, 0.37f, 0.12f);
                }
                _bins[index] = StationBuilder.Interactable(bin.gameObject, $"Bin {location}", new Vector3(0.42f, 0.3f, 0.4f), body,
                    UiKit.FromHex(0x2F5DA8), new Vector3(0, 0.03f, 0));
                var binIndex = index;
                _bins[index].Activated += _ => Pick(binIndex);
            }
        }

        private bool HasItemModels => itemModels != null && itemModels.Length == Bins.Length && Array.TrueForAll(itemModels, model => model != null);

        private void BuildCart(Transform root)
        {
            if (cartModel != null && toteModel != null)
            {
                StationBuilder.Model(root, "Cart Model", cartModel, Vector3.zero, Vector3.one);
                _toteRoot = StationBuilder.Model(root, "Tote", toteModel, new Vector3(-0.02f, 0.52f, 0), Vector3.one).transform;
                return;
            }

            Block(root, "Deck", new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.04f, 0.45f), PropColors.DarkSteel);
            Block(root, "Handle", new Vector3(0.28f, 0.8f, 0), new Vector3(0.03f, 0.6f, 0.45f), PropColors.DarkSteel);
            foreach (var x in new[] { -0.25f, 0.25f })
            foreach (var z in new[] { -0.18f, 0.18f })
                StationBuilder.Cylinder(root, "Wheel", new Vector3(x, 0.07f, z), new Vector3(0.1f, 0.04f, 0.1f), material, PropColors.Rubber).transform.localRotation = Quaternion.Euler(90f, 0, 0);
            foreach (var x in new[] { -0.25f, 0.25f })
                Block(root, "Leg", new Vector3(x, 0.28f, 0), new Vector3(0.03f, 0.42f, 0.4f), PropColors.DarkSteel);

            _toteRoot = StationBuilder.Group(root, "Tote", Vector3.zero);
            Block(_toteRoot, "Tote Base", new Vector3(-0.02f, 0.53f, 0), new Vector3(0.48f, 0.02f, 0.36f), UiKit.FromHex(0x5B6470));
            Block(_toteRoot, "Tote Front", new Vector3(-0.02f, 0.63f, -0.17f), new Vector3(0.48f, 0.2f, 0.02f), UiKit.FromHex(0x5B6470));
            Block(_toteRoot, "Tote Back", new Vector3(-0.02f, 0.63f, 0.17f), new Vector3(0.48f, 0.2f, 0.02f), UiKit.FromHex(0x5B6470));
            Block(_toteRoot, "Tote Side", new Vector3(-0.25f, 0.63f, 0), new Vector3(0.02f, 0.2f, 0.36f), UiKit.FromHex(0x5B6470));
            Block(_toteRoot, "Tote Side", new Vector3(0.21f, 0.63f, 0), new Vector3(0.02f, 0.2f, 0.36f), UiKit.FromHex(0x5B6470));
        }

        private void BuildTicket(Transform root)
        {
            StationBuilder.Display(root, new Vector2(1.64f, 0.56f), material, stand: false);
            _ticket = StationBuilder.Text(root, "", new Vector3(-0.15f, 0.02f, -0.025f), 0.6f, Color.white, 1.3f, 0.55f, TextAlignmentOptions.TopLeft);
            AcceptButton = StationBuilder.Button(root, "Accept order", new Vector3(0.58f, -0.2f, -0.03f), new Vector2(0.44f, 0.12f), material, UiKit.Purple, 0.38f);
            AcceptButton.Activated += _ => Accept();
            ScanOutButton = StationBuilder.Button(root, "Scan out", new Vector3(0.58f, -0.2f, -0.03f), new Vector2(0.44f, 0.12f), material, UiKit.Purple, 0.38f);
            ScanOutButton.Activated += _ => ScanOut();
        }

        private GameObject Block(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color) =>
            StationBuilder.Block(parent, name, localPosition, size, material, color);
    }
}
