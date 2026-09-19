using Mio.Unity.Config;
using UnityEngine;

namespace Mio.Unity.Feedback
{
    /// <summary>
    /// Device vibration behind an interface.
    ///
    /// Unity ships only a single coarse Vibrate() call, so this is deliberately
    /// a hook rather than a real implementation: when we adopt a proper haptics
    /// plugin (Core Haptics / Android VibrationEffect), only this file changes.
    /// </summary>
    public interface IHapticChannel
    {
        void Play(HapticStrength strength);
    }

    public sealed class NullHapticChannel : IHapticChannel
    {
        public static readonly NullHapticChannel Instance = new NullHapticChannel();
        public void Play(HapticStrength strength) { }
    }

    public sealed class DeviceHapticChannel : IHapticChannel
    {
        /// <summary>
        /// Vibrate() is a blunt instrument; firing it on every pop would turn a
        /// good session into a buzzing phone. Light cues are throttled hard and
        /// only the weightier ones get through.
        /// </summary>
        private const float MinIntervalSeconds = 0.08f;

        private float _lastPlayTime = float.NegativeInfinity;

        public bool Enabled { get; set; } = true;

        public void Play(HapticStrength strength)
        {
            if (!Enabled || strength == HapticStrength.None) return;
            if (Time.unscaledTime - _lastPlayTime < MinIntervalSeconds) return;

            _lastPlayTime = Time.unscaledTime;

#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isMobilePlatform) return;

            // Light taps are the most common cue by far. Until a real haptics
            // plugin lands, they stay silent rather than becoming a constant buzz.
            if (strength == HapticStrength.Light) return;

            Handheld.Vibrate();
#endif
        }
    }
}
