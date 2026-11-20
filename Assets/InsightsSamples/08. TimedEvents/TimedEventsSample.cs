using System.Collections.Generic;
using System.Globalization;
using InsightsSamples.Common;
using UnityEngine;

namespace InsightsSamples.TimedEvents
{
    /// <summary>
    /// Sample 08: time a task that isn't an assessment, objective, or interaction. StartTimedEvent when it
    /// starts; the next Event with the same name arrives with its duration.
    /// </summary>
    public class TimedEventsSample : MonoBehaviour, ISampleLifecycle
    {
        private const string OrderPick = "order_pick";

        [SerializeField] private PickFace station;

        private void OnEnable()
        {
            station.OrderAccepted += HandleOrderAccepted;
            station.ScannedOut += HandleScannedOut;
        }

        private void OnDisable()
        {
            station.OrderAccepted -= HandleOrderAccepted;
            station.ScannedOut -= HandleScannedOut;
        }

        private void HandleOrderAccepted()
        {
            // StartTimedEvent sends nothing. It notes the time, and the next Event named order_pick gets a
            // duration. Assessments, objectives, and interactions are timed already: don't add this to them.
            Abxr.StartTimedEvent(OrderPick);
        }

        private void HandleScannedOut(int itemsPicked, int mispicks)
        {
            // Event metadata: stable snake_case keys, string values. Make a new dictionary for every call,
            // because the SDK adds its own fields (duration, scene) to the one you pass.
            var meta = new Dictionary<string, string>
            {
                ["items_picked"] = itemsPicked.ToString(CultureInfo.InvariantCulture),
                ["mispicks"] = mispicks.ToString(CultureInfo.InvariantCulture),
            };
            Abxr.Event(OrderPick, meta);
        }

        public void OnSampleEnter()
        {
        }

        public void OnSampleExit()
        {
            // Nothing to close: a timed event that never finishes was never sent. The next StartTimedEvent
            // with the same name simply restarts the clock.
        }
    }
}
