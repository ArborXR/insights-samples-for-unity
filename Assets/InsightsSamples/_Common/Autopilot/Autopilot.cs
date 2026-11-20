using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Implemented once per sample. Drives the station to the requested outcome through the same props a
    /// person uses. Never calls the SDK.
    /// </summary>
    public interface ISampleAutopilot
    {
        IEnumerator Run(SampleOutcome outcome, AutopilotDriver driver);
    }

    /// <summary>
    /// What an autopilot can do: move the visible cursor to a prop and click or drag it, or wait. At a fast
    /// pace (for automation) it skips the cursor animation and pauses: a frame per click.
    /// </summary>
    public sealed class AutopilotDriver
    {
        private const float MoveSeconds = 0.45f;
        private readonly RectTransform _cursor;
        private readonly bool _fast;
        private Action _onFirstAction;

        public AutopilotDriver(RectTransform cursor, bool fast, Action onFirstAction = null)
        {
            _cursor = cursor;
            _fast = fast;
            _onFirstAction = onFirstAction;
        }

        /// <summary>True for automation runs: autopilots skip their own animations too.</summary>
        public bool Fast => _fast;

        public IEnumerator Click(StationInteractable target)
        {
            if (target == null) yield break;
            Acting();

            if (_fast)
            {
                target.Activate();
                yield return null;
                yield break;
            }

            var camera = Camera.main;
            if (camera != null) yield return MoveCursor(camera, target.ClickPoint);

            target.SetHovered(true);
            yield return new WaitForSeconds(0.2f);
            target.Activate();
            target.SetHovered(false);
            yield return new WaitForSeconds(0.35f);
        }

        /// <summary>Clicks a UI button, such as one on the SDK's world-space poll or sign-in panels.</summary>
        public IEnumerator ClickUi(Button button)
        {
            if (button == null || !button.isActiveAndEnabled) yield break;
            Acting();
            if (!_fast) yield return MoveCursorToUi(button.transform);
            button.onClick.Invoke();
            yield return _fast ? null : new WaitForSeconds(0.35f);
        }

        /// <summary>
        /// Presses a UI control that acts on pointer-down rather than click, such as the keys and buttons of the
        /// SDK's sign-in and pairing prompt.
        /// </summary>
        public IEnumerator Press(GameObject control)
        {
            if (control == null || !control.activeInHierarchy) yield break;
            Acting();
            if (!_fast) yield return MoveCursorToUi(control.transform);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = ScreenPoint(control.transform) };
            ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(control, pointer, ExecuteEvents.pointerUpHandler);
            yield return _fast ? null : new WaitForSeconds(0.35f);
        }

        private IEnumerator MoveCursorToUi(Transform control)
        {
            if (Camera.main == null || _cursor == null) yield break;
            Vector2 to = ScreenPoint(control);
            Vector2 from = _cursor.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / MoveSeconds)
            {
                _cursor.position = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            _cursor.position = to;
            yield return new WaitForSeconds(0.2f);
        }

        private static Vector2 ScreenPoint(Transform control)
        {
            var canvas = control.GetComponentInParent<Canvas>();
            var worldSpace = canvas != null && canvas.renderMode == RenderMode.WorldSpace;
            return worldSpace && Camera.main != null ? (Vector2)Camera.main.WorldToScreenPoint(control.position) : (Vector2)control.position;
        }

        /// <summary>Holds the button on a draggable prop and moves it through world points over the given time.</summary>
        public IEnumerator Drag(StationDraggable target, IReadOnlyList<Vector3> path, float seconds)
        {
            var camera = Camera.main;
            if (target == null || camera == null || path.Count == 0) yield break;
            Acting();
            if (!_fast) yield return MoveCursor(camera, target.GrabPoint);

            target.BeginDrag(RayThrough(camera, path[0]));
            if (_fast)
            {
                var steps = path.Count * 2;
                for (var step = 1; step <= steps; step++)
                {
                    DragStep(target, camera, PointAlong(path, (float)step / steps));
                    yield return null;
                }
            }
            else
            {
                for (float elapsed = 0; elapsed < seconds; elapsed += Time.deltaTime)
                {
                    DragStep(target, camera, PointAlong(path, elapsed / seconds));
                    yield return null;
                }
                DragStep(target, camera, path[path.Count - 1]);
            }
            target.EndDrag();
        }

        private void DragStep(StationDraggable target, Camera camera, Vector3 point)
        {
            if (_cursor != null) _cursor.position = camera.WorldToScreenPoint(point);
            target.DragTo(RayThrough(camera, point));
        }

        private IEnumerator MoveCursor(Camera camera, Vector3 worldPoint)
        {
            if (_cursor == null) yield break;
            Vector2 from = _cursor.position;
            Vector2 to = camera.WorldToScreenPoint(worldPoint);
            for (float t = 0; t < 1f; t += Time.deltaTime / MoveSeconds)
            {
                _cursor.position = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            _cursor.position = to;
        }

        private static Ray RayThrough(Camera camera, Vector3 point) =>
            new Ray(camera.transform.position, (point - camera.transform.position).normalized);

        private static Vector3 PointAlong(IReadOnlyList<Vector3> path, float t)
        {
            if (path.Count == 1) return path[0];
            var scaled = Mathf.Clamp01(t) * (path.Count - 1);
            var index = Mathf.Min(Mathf.FloorToInt(scaled), path.Count - 2);
            return Vector3.Lerp(path[index], path[index + 1], scaled - index);
        }

        private void Acting()
        {
            _onFirstAction?.Invoke();
            _onFirstAction = null;
        }
    }
}
