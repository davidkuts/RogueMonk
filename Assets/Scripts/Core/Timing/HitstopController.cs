using System;
using UnityEngine;

namespace Game.Core.Timing
{
    /// <summary>
    /// Engine-free freeze timer. Overlapping requests take the longer remaining time rather
    /// than summing, so a flurry of hits cannot stack into a freeze. Must be ticked with
    /// <em>unscaled</em> delta time — it is the thing holding the game clock at zero.
    ///
    /// Lives in Game.Core rather than Game.Combat because <see cref="GameClock"/> owns it and
    /// the pause menu has to coordinate with it; combat is only one of its callers.
    /// </summary>
    public sealed class HitstopController
    {
        public float Remaining { get; private set; }

        public bool IsActive => Remaining > 0f;

        /// <summary>Raised when a request starts or extends the freeze. Drives screenshake and rumble.</summary>
        public event Action<float> Requested;

        public void Request(float seconds)
        {
            if (seconds <= 0f)
                return;

            Remaining = Mathf.Max(Remaining, seconds);
            Requested?.Invoke(seconds);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsActive || unscaledDeltaTime <= 0f)
                return;

            Remaining = Mathf.Max(0f, Remaining - unscaledDeltaTime);
        }

        public void Clear() => Remaining = 0f;
    }
}
