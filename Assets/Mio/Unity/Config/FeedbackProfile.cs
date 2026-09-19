using System;
using System.Collections.Generic;
using Mio.Core.Session;
using UnityEngine;

namespace Mio.Unity.Config
{
    public enum HapticStrength
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }

    /// <summary>
    /// How one gameplay cue is expressed: sound, particles, shake and haptics.
    /// </summary>
    [Serializable]
    public struct CueResponse
    {
        public FeedbackCueKind Kind;

        [Header("Sound")]
        [Tooltip("Optional. The hook works with no clip assigned; it just stays silent.")]
        public AudioClip Sound;

        [Range(0f, 1f)] public float Volume;

        [Tooltip("Random pitch spread, so repeats do not become a machine-gun.")]
        [Range(0f, 0.5f)] public float PitchJitter;

        [Header("Particles")]
        public int BurstCount;
        public float BurstSpeed;
        public float BurstSize;

        [Header("Body")]
        [Tooltip("Camera-space shake, in play-field units.")]
        public float Shake;

        [Tooltip("Scale punch applied to whatever the cue points at.")]
        public float Punch;

        public HapticStrength Haptic;
    }

    /// <summary>
    /// The feel of the game in one asset.
    ///
    /// Design principle 3 says every meaningful interaction gives immediate
    /// feedback. Keeping that mapping as data means we can tune feel in the
    /// inspector during a playtest without touching a line of simulation code.
    /// </summary>
    [CreateAssetMenu(menuName = "MIO/Config/Feedback Profile", fileName = "FeedbackProfile")]
    public sealed class FeedbackProfile : ScriptableObject
    {
        [SerializeField] private CueResponse[] _responses = DefaultResponses();

        [Header("Global")]
        [Tooltip("Master switch for device vibration.")]
        public bool HapticsEnabled = true;

        [Range(0f, 1f)] public float MasterVolume = 0.8f;

        [Tooltip("Seconds a screen shake takes to settle.")]
        public float ShakeDecay = 0.25f;

        private Dictionary<FeedbackCueKind, CueResponse> _lookup;

        public CueResponse Get(FeedbackCueKind kind)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<FeedbackCueKind, CueResponse>();
                var source = _responses ?? DefaultResponses();
                for (var i = 0; i < source.Length; i++)
                {
                    _lookup[source[i].Kind] = source[i];
                }
            }

            return _lookup.TryGetValue(kind, out var response) ? response : default;
        }

        /// <summary>Clears the cache so inspector edits apply live during play.</summary>
        private void OnValidate() => _lookup = null;

        private static CueResponse Make(
            FeedbackCueKind kind,
            int burst,
            float burstSpeed,
            float burstSize,
            float shake,
            float punch,
            HapticStrength haptic,
            float volume = 0.8f)
        {
            return new CueResponse
            {
                Kind = kind,
                Volume = volume,
                PitchJitter = 0.08f,
                BurstCount = burst,
                BurstSpeed = burstSpeed,
                BurstSize = burstSize,
                Shake = shake,
                Punch = punch,
                Haptic = haptic
            };
        }

        /// <summary>
        /// Starting values, tuned so the prototypes feel alive before anyone has
        /// authored a single sound. Failures are deliberately quiet and gentle:
        /// this is a game an eight-year-old should not feel told off by.
        /// </summary>
        private static CueResponse[] DefaultResponses()
        {
            return new[]
            {
                Make(FeedbackCueKind.Begin, 0, 0f, 0f, 0f, 0.15f, HapticStrength.Light, 0.5f),
                Make(FeedbackCueKind.Success, 4, 0.30f, 0.018f, 0f, 0.18f, HapticStrength.Light, 0.5f),
                Make(FeedbackCueKind.Fail, 2, 0.12f, 0.012f, 0.004f, 0.06f, HapticStrength.None, 0.35f),
                Make(FeedbackCueKind.Pop, 12, 0.55f, 0.022f, 0.006f, 0.30f, HapticStrength.Light),
                Make(FeedbackCueKind.Chain, 16, 0.75f, 0.026f, 0.010f, 0.40f, HapticStrength.Medium),
                Make(FeedbackCueKind.Place, 6, 0.30f, 0.018f, 0.003f, 0.22f, HapticStrength.Light),
                Make(FeedbackCueKind.Clear, 22, 0.85f, 0.028f, 0.014f, 0.45f, HapticStrength.Medium),
                Make(FeedbackCueKind.Collect, 8, 0.45f, 0.020f, 0f, 0.25f, HapticStrength.Light),
                Make(FeedbackCueKind.Hazard, 10, 0.40f, 0.022f, 0.018f, 0.20f, HapticStrength.Medium),
                Make(FeedbackCueKind.Progress, 0, 0f, 0f, 0f, 0f, HapticStrength.None, 0f),
                Make(FeedbackCueKind.Win, 40, 0.95f, 0.030f, 0.010f, 0.50f, HapticStrength.Heavy),
                Make(FeedbackCueKind.Lose, 6, 0.25f, 0.020f, 0.012f, 0.15f, HapticStrength.Medium, 0.5f)
            };
        }
    }
}
