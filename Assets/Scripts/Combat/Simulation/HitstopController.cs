using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Engine-free hitstop timer. Overlapping requests take the longer remaining time rather
    /// than summing, so a flurry of hits cannot stack into a freeze. Must be ticked with
    /// <em>unscaled</em> delta time — the adapter zeroes the game clock while this is active.
    /// </summary>
    public sealed class HitstopController
    {
        public float Remaining { get; private set; }

        public bool IsActive => Remaining > 0f;

        /// <summary>Raised when a request starts or extends hitstop. Drives screenshake and rumble.</summary>
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
