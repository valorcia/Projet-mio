using System;
using System.Collections.Generic;
using Mio.Core.Session;
using Mio.Unity.Config;
using UnityEngine;

namespace Mio.Unity.Feedback
{
    /// <summary>
    /// Turns gameplay cues into things you can see, hear and feel.
    ///
    /// This is the only place that knows both the simulation's vocabulary and
    /// Unity's. Rules stay pure; feel stays tunable.
    /// </summary>
    [RequireComponent(typeof(BurstPool))]
    public sealed class FeedbackRouter : MonoBehaviour, IFeedbackChannel
    {
        private FeedbackProfile _profile;
        private PrototypePalette _palette;
        private BurstPool _bursts;
        private AudioSource _audio;
        private IHapticChannel _haptics = NullHapticChannel.Instance;
        private RectTransform _shakeTarget;

        private Vector2 _shakeOffset;
        private float _shakeAmount;

        /// <summary>
        /// Raised for every cue, so views can add their own reaction (a cell
        /// punching, a meter flashing) without the router knowing about them.
        /// </summary>
        public event Action<FeedbackCue> CueEmitted;

        public void Initialise(
            FeedbackProfile profile,
            PrototypePalette palette,
            RectTransform particleRoot,
            RectTransform shakeTarget)
        {
            _profile = profile;
            _palette = palette;
            _shakeTarget = shakeTarget;

            _bursts = GetComponent<BurstPool>();
            _bursts.Initialise(particleRoot);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            var device = new DeviceHapticChannel
            {
                Enabled = profile == null || profile.HapticsEnabled
            };
            _haptics = device;
        }

        public void Emit(in FeedbackCue cue)
        {
            var response = _profile != null ? _profile.Get(cue.Kind) : default;
            var at = new Vector2(cue.Position.X, cue.Position.Y);

            PlaySound(response);
            PlayBurst(cue, response, at);

            if (response.Shake > 0f)
            {
                // Intensity is how "big" the rules said the event was, so a
                // six-cell pop shakes harder than a two-cell one for free.
                _shakeAmount = Mathf.Max(_shakeAmount, response.Shake * Mathf.Lerp(0.5f, 1.5f, cue.Intensity));
            }

            _haptics.Play(response.Haptic);

            CueEmitted?.Invoke(cue);
        }

        private void PlaySound(CueResponse response)
        {
            if (_audio == null || response.Sound == null || response.Volume <= 0f) return;

            var master = _profile != null ? _profile.MasterVolume : 1f;
            _audio.pitch = 1f + UnityEngine.Random.Range(-response.PitchJitter, response.PitchJitter);
            _audio.PlayOneShot(response.Sound, response.Volume * master);
        }

        private void PlayBurst(in FeedbackCue cue, in CueResponse response, Vector2 at)
        {
            if (_bursts == null || response.BurstCount <= 0) return;

            var count = Mathf.RoundToInt(response.BurstCount * Mathf.Lerp(0.5f, 1.25f, cue.Intensity));
            _bursts.Emit(at, count, response.BurstSpeed, response.BurstSize, ColorFor(cue.Kind));
        }

        private Color ColorFor(FeedbackCueKind kind)
        {
            if (_palette == null) return Color.white;

            switch (kind)
            {
                case FeedbackCueKind.Fail:
                case FeedbackCueKind.Lose:
                    return _palette.Bad;

                case FeedbackCueKind.Hazard:
                    return _palette.Hazard;

                case FeedbackCueKind.Win:
                case FeedbackCueKind.Collect:
                case FeedbackCueKind.Clear:
                    return _palette.Good;

                default:
                    return _palette.Neutral;
            }
        }

        private void LateUpdate()
        {
            if (_shakeTarget == null) return;

            var decay = _profile != null ? Mathf.Max(0.01f, _profile.ShakeDecay) : 0.25f;
            _shakeAmount = Mathf.MoveTowards(_shakeAmount, 0f, Time.deltaTime / decay);

            if (_shakeAmount <= 0f)
            {
                if (_shakeOffset != Vector2.zero)
                {
                    _shakeOffset = Vector2.zero;
                    _shakeTarget.anchoredPosition = Vector2.zero;
                }

                return;
            }

            // Shake is expressed in field units; convert to the canvas pixels
            // the RectTransform actually lives in.
            var size = _shakeTarget.rect.size;
            _shakeOffset = new Vector2(
                UnityEngine.Random.Range(-1f, 1f) * _shakeAmount * size.x,
                UnityEngine.Random.Range(-1f, 1f) * _shakeAmount * size.y);

            _shakeTarget.anchoredPosition = _shakeOffset;
        }

        public void ResetState()
        {
            _shakeAmount = 0f;
            _shakeOffset = Vector2.zero;

            if (_shakeTarget != null) _shakeTarget.anchoredPosition = Vector2.zero;
            if (_bursts != null) _bursts.Clear();
        }
    }

    /// <summary>
    /// Small scale-punch animator for "this thing just reacted" moments.
    /// Kept dependency-free so the project needs no tweening package.
    /// </summary>
    public sealed class PunchAnimator : MonoBehaviour
    {
        private struct Punch
        {
            public Transform Target;
            public float Strength;
            public float Time;
            public float Duration;
        }

        private readonly List<Punch> _punches = new List<Punch>();

        public void Play(Transform target, float strength, float duration = 0.22f)
        {
            if (target == null || strength <= 0f) return;

            for (var i = 0; i < _punches.Count; i++)
            {
                if (_punches[i].Target != target) continue;

                // Re-punching an object restarts it rather than stacking, so a
                // rapid chain does not inflate one cell to absurd size.
                var existing = _punches[i];
                existing.Strength = Mathf.Max(existing.Strength, strength);
                existing.Time = 0f;
                existing.Duration = duration;
                _punches[i] = existing;
                return;
            }

            _punches.Add(new Punch { Target = target, Strength = strength, Time = 0f, Duration = duration });
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            for (var i = _punches.Count - 1; i >= 0; i--)
            {
                var punch = _punches[i];

                if (punch.Target == null)
                {
                    _punches.RemoveAt(i);
                    continue;
                }

                punch.Time += dt;
                var t = Mathf.Clamp01(punch.Time / punch.Duration);

                // One overshoot then settle: sin(pi * t) peaks in the middle and
                // returns to exactly 1, so nothing is left permanently scaled.
                var scale = 1f + punch.Strength * Mathf.Sin(t * Mathf.PI) * (1f - t);
                punch.Target.localScale = new Vector3(scale, scale, 1f);

                if (t >= 1f)
                {
                    punch.Target.localScale = Vector3.one;
                    _punches.RemoveAt(i);
                }
                else
                {
                    _punches[i] = punch;
                }
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _punches.Count; i++)
            {
                if (_punches[i].Target != null) _punches[i].Target.localScale = Vector3.one;
            }

            _punches.Clear();
        }
    }
}
