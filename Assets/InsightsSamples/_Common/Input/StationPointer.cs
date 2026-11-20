using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Mouse and touch input for stations: hover highlights, click or tap activates, and holding the button on
    /// a draggable prop drags it. No pointer lock.
    /// </summary>
    public class StationPointer : MonoBehaviour
    {
        private StationInteractable _hovered;
        private StationDraggable _dragging;
        private InputAction _press;
        private Vector2? _pressedAt;
        private bool _released;

        private void OnEnable()
        {
            // An action sees every press, even one released within the same frame (a quick tap, or a click
            // from a browser automation tool), which polling wasPressedThisFrame can miss.
            _press = new InputAction("Station Press", InputActionType.Button, "<Pointer>/press");
            _press.performed += HandlePress;
            _press.canceled += HandleRelease;
            _press.Enable();
        }

        private void OnDisable()
        {
            _press.performed -= HandlePress;
            _press.canceled -= HandleRelease;
            _press.Dispose();
            _press = null;
            EndDrag();
        }

        private void Update()
        {
            var pointer = Pointer.current;
            var camera = Camera.main;
            var pressedAt = _pressedAt;
            var released = _released;
            _pressedAt = null;
            _released = false;
            if (pointer == null || camera == null || ShellSignals.SdkPromptOpen)
            {
                SetHovered(null);
                EndDrag();
                return;
            }

            var position = pointer.position.ReadValue();
            if (_dragging != null)
            {
                _dragging.DragTo(camera.ScreenPointToRay(position));
                if (released) EndDrag();
                return;
            }

            SetHovered(TargetAt(camera, position)?.GetComponent<StationInteractable>());
            if (!pressedAt.HasValue) return;

            var target = TargetAt(camera, pressedAt.Value);
            var draggable = target != null ? target.GetComponent<StationDraggable>() : null;
            if (draggable != null && draggable.Interactable)
            {
                _dragging = draggable;
                draggable.BeginDrag(camera.ScreenPointToRay(pressedAt.Value));
                if (released) EndDrag();
            }
            else if (target != null)
            {
                target.GetComponent<StationInteractable>()?.Activate();
            }
        }

        private void HandlePress(InputAction.CallbackContext context)
        {
            if (context.control.device is Pointer pointer) _pressedAt = pointer.position.ReadValue();
        }

        private void HandleRelease(InputAction.CallbackContext context) => _released = true;

        private void EndDrag()
        {
            if (_dragging != null) _dragging.EndDrag();
            _dragging = null;
        }

        /// <summary>The station prop under a screen position, ignoring trigger colliders such as gaze targets'.</summary>
        internal static Component TargetAt(Camera camera, Vector2 position)
        {
            if (!camera.pixelRect.Contains(position) || PointerOverUi() ||
                !Physics.Raycast(camera.ScreenPointToRay(position), out var hit, 50f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return null;

            var interactable = hit.collider.GetComponentInParent<StationInteractable>();
            if (interactable != null && interactable.Interactable) return interactable;
            var draggable = hit.collider.GetComponentInParent<StationDraggable>();
            return draggable != null && draggable.Interactable ? draggable : null;
        }

        private void SetHovered(StationInteractable target)
        {
            if (_hovered == target) return;
            if (_hovered != null) _hovered.SetHovered(false);
            _hovered = target;
            if (_hovered != null) _hovered.SetHovered(true);
        }

        internal static bool PointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
