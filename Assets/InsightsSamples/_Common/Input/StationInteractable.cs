using System;
using System.Collections;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// Something on a station the learner can click (or tap, or point at in VR). Autopilot activates it
    /// through the same Activate call, so a scripted run and a person look identical to the lesson script.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StationInteractable : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static StationInteractable _lastActivated;
        private static int _lastActivatedFrame = -1;

        [SerializeField] private string label;
        [SerializeField] private Renderer tintTarget;
        [SerializeField] private Color baseColor = Color.white;
        [Tooltip("Grey out while not interactable. On for buttons; off for props, which should keep their real colours.")]
        [SerializeField] private bool dimWhenInactive = true;

        private MaterialPropertyBlock _block;
        private Vector3 _restScale = Vector3.one;
        private bool _hovered;
        private bool _interactable = true;
        private float _flash;
        private Coroutine _flashing;

        public event Action<StationInteractable> Activated;

        /// <summary>The prop activated during this frame, if any, so a record made in response can point back at it.</summary>
        public static StationInteractable ActivatedThisFrame =>
            _lastActivatedFrame == Time.frameCount && _lastActivated != null ? _lastActivated : null;

        public string Label => label;
        public Vector3 ClickPoint => GetComponent<Collider>().bounds.center;

        public bool Interactable
        {
            get => _interactable && isActiveAndEnabled;
            set
            {
                _interactable = value;
                ApplyTint();
            }
        }

        public void Configure(string newLabel, Renderer target, Color color, bool dimWhenNotInteractable)
        {
            label = newLabel;
            tintTarget = target;
            baseColor = color;
            dimWhenInactive = dimWhenNotInteractable;
            ApplyTint();
        }

        public void SetBaseColor(Color color)
        {
            baseColor = color;
            ApplyTint();
        }

        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            ApplyTint();
        }

        public void Activate()
        {
            if (!Interactable) return;
            _lastActivated = this;
            _lastActivatedFrame = Time.frameCount;
            UiSounds.Press();
            Activated?.Invoke(this);
            if (isActiveAndEnabled) StartCoroutine(Pulse());
        }

        /// <summary>Glows briefly, in step with the Insights Stream row this prop caused.</summary>
        public void Flash()
        {
            if (!isActiveAndEnabled) return;
            if (_flashing != null) StopCoroutine(_flashing);
            _flashing = StartCoroutine(FlashRoutine());
        }

        private void Awake() => _restScale = transform.localScale;

        private void OnEnable() => ApplyTint();

        private void OnDisable()
        {
            transform.localScale = _restScale;
            _flash = 0;
            _flashing = null;
        }

        private IEnumerator Pulse()
        {
            transform.localScale = _restScale * 0.94f;
            yield return new WaitForSeconds(0.08f);
            transform.localScale = _restScale;
        }

        private IEnumerator FlashRoutine()
        {
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
            {
                _flash = 1f - t;
                ApplyTint();
                yield return null;
            }
            _flash = 0;
            ApplyTint();
            _flashing = null;
        }

        private void ApplyTint()
        {
            if (tintTarget == null) return;
            _block ??= new MaterialPropertyBlock();
            var color = !_interactable && dimWhenInactive ? Color.Lerp(baseColor, Color.gray, 0.6f)
                : _hovered && _interactable ? Color.Lerp(baseColor, Color.white, 0.35f)
                : baseColor;
            color = Color.Lerp(color, UiKit.Lavender, _flash * 0.85f);
            tintTarget.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            tintTarget.SetPropertyBlock(_block);
        }
    }
}
