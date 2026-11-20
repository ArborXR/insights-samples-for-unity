using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Lets the learner look around a station by right-dragging (or dragging a finger on a touch screen), within
    /// limits, with a reticle at the centre of the view. On a flat screen the view centre stands in for gaze. Add it
    /// to a station's camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class StationLook : MonoBehaviour
    {
        private const float DegreesPerPixel = 0.15f;
        private const float MaxYaw = 40f;
        private const float MaxPitch = 25f;

        private Camera _camera;
        private RectTransform _reticle;
        private Quaternion _rest;
        private float _yaw;
        private float _pitch;
        private bool _touchLooks;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _rest = transform.localRotation;
            BuildReticle();
        }

        private void LateUpdate() => _reticle.position = _camera.pixelRect.center;

        private void Update()
        {
            if (ShellSignals.SdkPromptOpen)
            {
                _touchLooks = false;
                return;
            }
            var mouse = Mouse.current;
            var touch = Touchscreen.current?.primaryTouch;
            // A touch turns the view only if it starts on the scene itself: not on UI (the Stream scrolls, the
            // header has buttons) and not on a prop, which a tap activates.
            if (touch != null && touch.press.wasPressedThisFrame)
            {
                var start = touch.position.ReadValue();
                _touchLooks = _camera.pixelRect.Contains(start) && !StationPointer.PointerOverUi()
                              && StationPointer.TargetAt(_camera, start) == null;
            }
            Vector2 delta;
            if (mouse != null && mouse.rightButton.isPressed) delta = mouse.delta.ReadValue();
            else if (touch != null && touch.press.isPressed && _touchLooks) delta = touch.delta.ReadValue();
            else return;
            Turn(delta.x * DegreesPerPixel, -delta.y * DegreesPerPixel);
        }

        /// <summary>Turns the view toward a point over the given time, the way a right-drag would.</summary>
        public IEnumerator TurnTowards(Vector3 point, float seconds)
        {
            var local = Quaternion.Inverse(_rest) * Quaternion.Inverse(ParentRotation()) * (point - transform.position);
            var targetYaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            var targetPitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            var startYaw = _yaw;
            var startPitch = _pitch;
            for (float t = 0; t < 1f; t += Time.deltaTime / Mathf.Max(seconds, 0.0001f))
            {
                var eased = Mathf.SmoothStep(0, 1, t);
                Turn(Mathf.Lerp(startYaw, targetYaw, eased) - _yaw, Mathf.Lerp(startPitch, targetPitch, eased) - _pitch);
                yield return null;
            }
            Turn(targetYaw - _yaw, targetPitch - _pitch);
        }

        private void Turn(float yawDelta, float pitchDelta)
        {
            _yaw = Mathf.Clamp(_yaw + yawDelta, -MaxYaw, MaxYaw);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, -MaxPitch, MaxPitch);
            transform.localRotation = _rest * Quaternion.Euler(_pitch, _yaw, 0);
        }

        private Quaternion ParentRotation() => transform.parent != null ? transform.parent.rotation : Quaternion.identity;

        private void BuildReticle()
        {
            var canvas = UiKit.CreateScreenCanvas(transform, "Gaze Reticle", sortingOrder: 40);
            var ring = UiKit.Panel(canvas.transform, "Ring", UiKit.White);
            ring.raycastTarget = false;
            ring.rectTransform.sizeDelta = new Vector2(16, 16);
            UiKit.Round(ring, 8f);
            var dot = UiKit.Panel(ring.transform, "Dot", UiKit.Purple);
            dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(10, 10);
            UiKit.Round(dot, 5f);
            _reticle = ring.rectTransform;
        }
    }
}
