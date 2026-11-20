using AbxrLib.Runtime.Services.Telemetry;
using UnityEngine;

namespace InsightsSamples.GazeTargets
{
    /// <summary>
    /// Sample 14: measure where the learner looks. AbxrTarget marks what to watch; every event then carries a
    /// gaze score per target, and the SDK sends gaze telemetry with it.
    /// </summary>
    public class GazeTargetsSample : MonoBehaviour
    {
        private const string CustomerGreeted = "customer_greeted";

        [SerializeField] private RetailCounter station;

        private void OnEnable() => station.Greeted += HandleGreeted;

        private void OnDisable() => station.Greeted -= HandleGreeted;

        private void Start()
        {
            // Mark what to measure attention on. Each target's name becomes the metadata key
            // gaze_score_<name>, so use stable snake_case names. Usually you'd use Analytics for XR > Create
            // Abxr Target in the Editor; the station builds its props at runtime, so they're added here. The
            // Configuration's Target Gaze Tracking settings set the maximum distance and trigger colliders.
            AddTarget(station.CustomerFace, "customer_face", 0.14f);
            AddTarget(station.ProductShelf, "product_shelf", 0.45f);
            AddTarget(station.Phone, "phone", 0.09f);
        }

        private static void AddTarget(GameObject prop, string targetName, float radius)
        {
            // An AbxrTarget lives on its own child object, as the Editor menu makes it. Created in code, set it up
            // before it wakes: the SDK sizes a target's trigger collider to its object only in the Editor, and
            // the 1 m default can hide other targets behind it.
            var marker = new GameObject(targetName);
            marker.SetActive(false);
            marker.transform.SetParent(prop.transform, false);
            var target = marker.AddComponent<AbxrTarget>();
            target.SetTargetName(targetName);
            target.triggerColliderSize = radius;
            marker.SetActive(true);
        }

        private void HandleGreeted()
        {
            // A plain event. The SDK adds a gaze score from 0 to 1 for each target: 1 when the view points
            // straight at it, 0.5 when it's at a right angle, 0 when it's directly behind the learner or something blocks it.
            // A target near the edge of the view still scores high, so compare targets rather than using a cutoff.
            Abxr.Event(CustomerGreeted);
        }
    }
}
