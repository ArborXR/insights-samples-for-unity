using System;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// A prop the learner drags while holding the button down, such as a welding torch. The pointer and the
    /// autopilot both drive it with rays through the drag position, so the station can't tell them apart.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StationDraggable : MonoBehaviour
    {
        public event Action<Ray> DragStarted;
        public event Action<Ray> Dragged;
        public event Action DragEnded;

        public bool Interactable { get; set; } = true;
        public bool Dragging { get; private set; }
        public Vector3 GrabPoint => GetComponent<Collider>().bounds.center;

        public void BeginDrag(Ray ray)
        {
            if (!Interactable || !isActiveAndEnabled || Dragging) return;
            Dragging = true;
            DragStarted?.Invoke(ray);
        }

        public void DragTo(Ray ray)
        {
            if (Dragging) Dragged?.Invoke(ray);
        }

        public void EndDrag()
        {
            if (!Dragging) return;
            Dragging = false;
            DragEnded?.Invoke();
        }

        private void OnDisable() => EndDrag();
    }
}
