using System.Globalization;
using AbxrLib.Runtime.Services.Telemetry;
using UnityEngine;

namespace InsightsSamples.Telemetry
{
    /// <summary>
    /// Sample 13: telemetry for continuous measurements. TrackObject samples an object's position on its own;
    /// Abxr.Telemetry sends your own measurements at a steady rate while they matter.
    /// </summary>
    public class TelemetrySample : MonoBehaviour
    {
        private const string TorchTechnique = "torch_technique";
        private const float SamplesPerSecond = 5f;

        [SerializeField] private WeldStation station;

        private float _nextSample;

        private void OnEnable() => station.WeldStarted += HandleWeldStarted;

        private void OnDisable() => station.WeldStarted -= HandleWeldStarted;

        private void Start()
        {
            // TrackObject records this object's position every Telemetry Tracking Period (in the Configuration,
            // 10 seconds by default), named after the GameObject: "weld_torch Position". Usually you add it in
            // the Inspector; the station builds the torch at runtime, so it's added here.
            station.Torch.gameObject.AddComponent<TrackObject>();
        }

        private void HandleWeldStarted() => _nextSample = Time.time;

        private void Update()
        {
            // Sample only while the measurement matters (here, while welding), at a steady rate. Insights can
            // then chart it over time.
            if (!station.Welding || Time.time < _nextSample) return;
            _nextSample = Time.time + 1f / SamplesPerSecond;

            // Values are strings: format numbers with the invariant culture, so 12.5 never arrives as "12,5".
            var sample = new Abxr.Dict
            {
                ["travel_angle_deg"] = station.TravelAngle.ToString("0.0", CultureInfo.InvariantCulture),
                ["distance_to_seam_mm"] = station.DistanceToSeamMm.ToString("0.0", CultureInfo.InvariantCulture),
            };
            Abxr.Telemetry(TorchTechnique, sample);
        }
    }
}
