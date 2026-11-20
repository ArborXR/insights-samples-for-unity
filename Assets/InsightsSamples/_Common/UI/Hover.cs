using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Tints a graphic while the pointer is over it, and tells listeners, for the shell's buttons, rows, and cards.
    /// It also gives them their click sound.
    /// </summary>
    public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Graphic _target;
        private Selectable _selectable;
        private Color _normal;
        private Color _hover;

        public event Action<bool> Changed;

        public bool IsOver { get; private set; }

        public void Init(Graphic target, Color normal, Color hover)
        {
            _target = target;
            _selectable = GetComponent<Selectable>();
            SetColors(normal, hover);
        }

        public void SetColors(Color normal, Color hover)
        {
            _normal = normal;
            _hover = hover;
            Apply();
        }

        /// <summary>Puts the graphic back to its resting or hover colour, e.g. after something else animated it.</summary>
        public void Apply()
        {
            if (_target != null) _target.color = IsOver ? _hover : _normal;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            Set(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Clickable && eventData.button == PointerEventData.InputButton.Left) UiSounds.Press();
        }

        public void OnPointerExit(PointerEventData eventData) => Set(false);

        private void OnDisable() => Set(false);

        // Looked up each time: some callers add their button after this, or switch it off while it does nothing.
        private bool Clickable
        {
            get
            {
                var selectable = GetComponent<Selectable>();
                return selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable();
            }
        }

        private void Set(bool over)
        {
            if (IsOver == over) return;
            IsOver = over;
            Apply();
            Changed?.Invoke(over);
        }
    }
}
