using System;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Engine-free dash charge pool with <em>sequential</em> recharge: only one charge
    /// refills at a time. Spending a second charge while the first is still recharging
    /// does not restart or parallel the timer — the first returns on schedule, then the
    /// second starts its own full wait. Burning both charges therefore costs a genuinely
    /// longer dry spell than burning one.
    /// </summary>
    public sealed class DashCharges
    {
        readonly IDashSettings settings;

        int spent;
        float rechargeRemaining;

        public DashCharges(IDashSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public int Max => Mathf.Max(0, settings.MaxCharges);

        public int Available => Mathf.Clamp(Max - spent, 0, Max);

        public bool HasCharge => Available > 0;

        /// <summary>Recharge progress 0..1 of the single charge currently refilling; 1 when the pool is full.</summary>
        public float NextChargeProgress
        {
            get
            {
                float rechargeSeconds = settings.RechargeSeconds;
                if (spent <= 0 || rechargeSeconds <= 0f)
                    return 1f;

                return Mathf.Clamp01(1f - rechargeRemaining / rechargeSeconds);
            }
        }

        /// <summary>
        /// Fill level 0..1 of a single charge slot, for the HUD pips. Slots below
        /// <see cref="Available"/> read full, the next slot shows recharge progress, the
        /// rest read empty. Keeps pip logic testable instead of stranding it in a view.
        /// </summary>
        public float GetChargeFill(int index)
        {
            if (index < 0 || index >= Max)
                return 0f;
            if (index < Available)
                return 1f;

            return index == Available ? NextChargeProgress : 0f;
        }

        public bool TrySpend()
        {
            if (!HasCharge)
                return false;

            // Only start the clock if nothing was already refilling — recharge is sequential.
            if (spent == 0)
                rechargeRemaining = Mathf.Max(0f, settings.RechargeSeconds);

            spent++;
            return true;
        }

        /// <summary>
        /// Returns one charge immediately — the perfect-dodge reward. Any in-progress
        /// recharge keeps its accumulated progress and carries on toward the next charge.
        /// </summary>
        public void Refund()
        {
            if (spent <= 0)
                return;

            spent--;
            if (spent == 0)
                rechargeRemaining = 0f;
        }

        public void RefillAll()
        {
            spent = 0;
            rechargeRemaining = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (spent <= 0 || deltaTime <= 0f)
                return;

            float rechargeSeconds = settings.RechargeSeconds;
            if (rechargeSeconds <= 0f)
            {
                RefillAll();
                return;
            }

            rechargeRemaining -= deltaTime;

            // A long frame can complete more than one charge; roll the overshoot forward.
            while (rechargeRemaining <= 0f && spent > 0)
            {
                spent--;
                rechargeRemaining = spent > 0 ? rechargeRemaining + rechargeSeconds : 0f;
            }
        }
    }
}
