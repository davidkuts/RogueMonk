using System;
using UnityEngine;

namespace Game.Core.Timing
{
    /// <summary>
    /// Engine-free slow-motion timer, the sibling of <see cref="HitstopController"/>.
    ///
    /// Overlapping requests take the <em>stronger</em> slow and the longer remaining time rather
    /// than multiplying or summing — same reasoning as hitstop. Two perfect dodges in quick
    /// succession should read as one clean moment of focus, not compound into a crawl the player
    /// cannot get out of.
    ///
    /// Ticked with <em>unscaled</em> delta time, because it is the thing scaling the other clock.
    /// </summary>
    public sealed class SlowMotionController
    {
        public float Remaining { get; private set; }

        /// <summary>The time scale to run at, 1 when nothing is slowing things down.</summary>
        public float Scale { get; private set; } = 1f;

        public bool IsActive => Remaining > 0f;

        /// <summary>0..1 through the current slow. Drives any visual treatment riding on it.</summary>
        public float Progress => Duration > 0f ? 1f - Mathf.Clamp01(Remaining / Duration) : 1f;

        public float Duration { get; private set; }

        /// <summary>Raised when a request starts or deepens the effect.</summary>
        public event Action<float, float> Requested;

        public void Request(float seconds, float scale)
        {
            if (seconds <= 0f)
                return;

            scale = Mathf.Clamp(scale, 0.05f, 1f);

            // Strongest slow wins, longest duration wins; neither compounds.
            Scale = IsActive ? Mathf.Min(Scale, scale) : scale;

            if (seconds > Remaining)
            {
                Remaining = seconds;
                Duration = seconds;
            }

            Requested?.Invoke(seconds, Scale);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsActive || unscaledDeltaTime <= 0f)
                return;

            Remaining = Mathf.Max(0f, Remaining - unscaledDeltaTime);

            if (Remaining <= 0f)
            {
                Scale = 1f;
                Duration = 0f;
            }
        }

        public void Clear()
        {
            Remaining = 0f;
            Duration = 0f;
            Scale = 1f;
        }
    }
}
