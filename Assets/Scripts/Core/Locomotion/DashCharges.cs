using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Engine-free dash charge pool. Each spent charge recharges on its own independent
    /// timer (staggered recharge, per DESIGN.md) rather than sharing one cooldown, so
    /// spending two charges a second apart returns them a second apart.
    /// </summary>
    public sealed class DashCharges
    {
        readonly IDashSettings settings;

        /// <summary>Remaining recharge time per spent charge, oldest spend first.</summary>
        readonly List<float> recharging = new List<float>();

        public DashCharges(IDashSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public int Max => Mathf.Max(0, settings.MaxCharges);

        public int Available => Mathf.Max(0, Max - recharging.Count);

        public bool HasCharge => Available > 0;

        /// <summary>Recharge progress 0..1 of the charge nearest to returning, or 1 when full.</summary>
        public float NextChargeProgress
        {
            get
            {
                if (recharging.Count == 0 || settings.RechargeSeconds <= 0f)
                    return 1f;

                float shortestRemaining = float.MaxValue;
                for (int i = 0; i < recharging.Count; i++)
                    shortestRemaining = Mathf.Min(shortestRemaining, recharging[i]);

                return Mathf.Clamp01(1f - shortestRemaining / settings.RechargeSeconds);
            }
        }

        public bool TrySpend()
        {
            if (!HasCharge)
                return false;

            recharging.Add(Mathf.Max(0f, settings.RechargeSeconds));
            return true;
        }

        /// <summary>
        /// Returns the most recently spent charge immediately — the perfect-dodge reward.
        /// No-op when nothing is recharging.
        /// </summary>
        public void Refund()
        {
            if (recharging.Count > 0)
                recharging.RemoveAt(recharging.Count - 1);
        }

        public void RefillAll() => recharging.Clear();

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            for (int i = recharging.Count - 1; i >= 0; i--)
            {
                float remaining = recharging[i] - deltaTime;
                if (remaining <= 0f)
                    recharging.RemoveAt(i);
                else
                    recharging[i] = remaining;
            }
        }
    }
}
