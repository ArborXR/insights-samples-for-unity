using System;
using UnityEngine;

namespace InsightsSamples.Common
{
    /// <summary>
    /// The app's few interface sounds: a click when something is pressed, and a short cue when a run ends. The
    /// clips are from Kenney's Interface Sounds (CC0), in Assets/Art/ThirdParty. The header's sound menu sets the
    /// volume and mute, which are remembered. The sound=off deep link silences one launch, and so does pace=fast,
    /// which only unattended runs use.
    /// </summary>
    public static class UiSounds
    {
        private const string Folder = "InterfaceSounds/";
        private const string VolumeKey = "insights_samples_sound_volume";
        private const string MutedKey = "insights_samples_sound_muted";
        private const float DefaultVolume = 0.5f;
        private const float PreviewGap = 0.15f;
        // A run usually ends on a press: its cue follows the click instead of landing on it.
        private const float ResultDelay = 0.12f;

        // The clips come at full level. These set each one's level with the volume all the way up.
        private static readonly Sound PressSound = new Sound("click_002", 0.6f);
        private static readonly Sound PassSound = new Sound("confirmation_001", 0.5f);
        private static readonly Sound FailSound = new Sound("error_005", 0.7f);

        private static AudioSource _clicks;
        private static AudioSource _results;
        private static float _volume = DefaultVolume;
        private static bool _muted;
        private static bool _silencedByLink;
        private static float _lastPreview;

        /// <summary>Raised when the volume or mute changes.</summary>
        public static event Action Changed;

        /// <summary>From 0 to 1. Remembered between launches, once <see cref="Save"/> is called.</summary>
        public static float Volume
        {
            get => _volume;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_volume, value)) return;
                _volume = value;
                PlayerPrefs.SetFloat(VolumeKey, value);
                Apply();
            }
        }

        /// <summary>Remembered between launches, once <see cref="Save"/> is called.</summary>
        public static bool Muted
        {
            get => _muted;
            set
            {
                if (_muted == value) return;
                _muted = value;
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                Apply();
            }
        }

        private static bool Silent => _muted || _silencedByLink || _volume <= 0f;

        // Before the shell exists, so its speaker button and sound menu start from the remembered settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Load()
        {
            _volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            _silencedByLink = DeepLinks.Get(DeepLinks.Sound) == "off" || DeepLinks.Get(DeepLinks.Pace) == "fast";
            SampleEvents.Finished -= PlayResult;
            SampleEvents.Finished += PlayResult;
            // Loaded now, so the first click isn't late or lost.
            foreach (var sound in new[] { PressSound, PassSound, FailSound }) sound.Warm();
        }

        public static void Press() => Play(PressSound);

        /// <summary>Plays the click at the current volume, for the volume slider: at most a few times a second.</summary>
        public static void Preview()
        {
            if (Time.unscaledTime - _lastPreview < PreviewGap) return;
            _lastPreview = Time.unscaledTime;
            Play(PressSound);
        }

        private static void Play(Sound sound)
        {
            if (Silent || sound.Clip == null) return;
            EnsureSources();
            _clicks.PlayOneShot(sound.Clip, sound.Level);
        }

        private static void PlayResult(SampleOutcome outcome)
        {
            var sound = outcome == SampleOutcome.Pass ? PassSound : FailSound;
            if (Silent || sound.Clip == null) return;
            EnsureSources();
            _results.clip = sound.Clip;
            _results.volume = sound.Level * _volume;
            _results.PlayDelayed(ResultDelay);
        }

        /// <summary>Writes the volume and mute to storage. The sound menu calls it once the slider is let go.</summary>
        public static void Save() => PlayerPrefs.Save();

        private static void Apply()
        {
            if (_clicks != null) _clicks.volume = _volume;
            if (_results != null && Silent) _results.Stop();
            Changed?.Invoke();
        }

        private static void EnsureSources()
        {
            if (_clicks != null) return;
            var host = new GameObject("UI Sounds");
            UnityEngine.Object.DontDestroyOnLoad(host);
            _clicks = Source(host);
            _clicks.volume = _volume;
            _results = Source(host);
        }

        private static AudioSource Source(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private sealed class Sound
        {
            private readonly string _name;
            private AudioClip _clip;

            public readonly float Level;

            public Sound(string name, float level)
            {
                _name = name;
                Level = level;
            }

            public AudioClip Clip => _clip != null ? _clip : _clip = Resources.Load<AudioClip>(Folder + _name);

            public void Warm()
            {
                if (Clip != null && Clip.loadState == AudioDataLoadState.Unloaded) Clip.LoadAudioData();
            }
        }
    }
}
