using System.Collections.Generic;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Core.Audio
{
    /// <summary>
    /// Plays one-shots through a small pool of AudioSources.
    ///
    /// Two decisions worth knowing. Pitch variation draws from a **separate** RNG, not the
    /// RunContext one: a seeded run must replay identically, and if cosmetic audio consumed
    /// gameplay randomness then simply hearing more hits would change the level. And repeats
    /// are rate-limited per sound, because a wave of enemies dying on the same frame otherwise
    /// stacks into a single loud smear.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [SerializeField] SoundSet sounds;
        [SerializeField, Tooltip("Simultaneous one-shots. Beyond this, the oldest is reused.")]
        int voiceCount = 12;
        [SerializeField, Range(0f, 1f)] float masterVolume = 0.8f;

        readonly Dictionary<GameSound, float> lastPlayedAt = new Dictionary<GameSound, float>();

        AudioSource[] voices;
        int nextVoice;

        // Deliberately not the run RNG - see the class comment.
        readonly XorShiftRandom cosmeticRandom = new XorShiftRandom(0xC0FFEEu);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < voices.Length; i++)
            {
                var go = new GameObject($"Voice_{i}");
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // 2D: a top-down camera makes panning by world position more confusing than helpful
                voices[i] = source;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Play(GameSound sound, float volumeScale = 1f)
        {
            if (sounds == null || voices == null)
                return;

            if (!sounds.TryGet(sound, out SoundSet.Entry entry))
                return;

            // Unscaled: hits land during hitstop, when the game clock is stopped.
            float now = Time.unscaledTime;
            if (lastPlayedAt.TryGetValue(sound, out float last) &&
                now - last < entry.MinIntervalSeconds)
                return;

            lastPlayedAt[sound] = now;

            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;

            voice.clip = entry.Clip;
            voice.volume = Mathf.Clamp01(entry.Volume * volumeScale * masterVolume);
            voice.pitch = 1f + cosmeticRandom.NextFloat(-entry.PitchVariance, entry.PitchVariance);
            voice.Play();
        }

        /// <summary>Convenience for callers that may run before the director exists.</summary>
        public static void PlaySound(GameSound sound, float volumeScale = 1f)
        {
            if (Instance != null)
                Instance.Play(sound, volumeScale);
        }
    }
}
