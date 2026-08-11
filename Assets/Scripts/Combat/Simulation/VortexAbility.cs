using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Availability of the Undertow: a cooldown that landed hits shorten.
    ///
    /// <para>Recharge is tied to <em>hits</em>, not to perfect dodges (DESIGN.md § The Vortex). A
    /// perfect dodge already pays three separate rewards, and hanging a fourth on it would make the
    /// whole kit degrade at once for players who struggle with timing — the exact frustration this
    /// ability exists to prevent. Landing hits is the accessible skill axis, so that is what this
    /// rides.</para>
    ///
    /// <para>Whiffs refund nothing, which is what stops the recharge becoming a mash. Engine-free so
    /// the timing is testable without entering play mode.</para>
    /// </summary>
    public sealed class VortexAbility
    {
        readonly float cooldownSeconds;
        readonly float perHitRefundSeconds;

        float remaining;

        public VortexAbility(float cooldownSeconds, float perHitRefundSeconds)
        {
            this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            this.perHitRefundSeconds = Mathf.Max(0f, perHitRefundSeconds);
        }

        /// <summary>Starts ready: the first room should not open with the answer to it on cooldown.</summary>
        public bool IsReady => remaining <= 0f;

        public float CooldownRemaining => Mathf.Max(0f, remaining);

        public float CooldownSeconds => cooldownSeconds;

        /// <summary>0 just spent, 1 ready. What a HUD dial draws.</summary>
        public float ReadyFraction =>
            cooldownSeconds <= 0f ? 1f : Mathf.Clamp01(1f - remaining / cooldownSeconds);

        /// <summary>
        /// Raised on the frame the ability becomes available again. Readiness has to be perceivable
        /// without looking at the HUD, or players sit on it — this is what the tick hangs off.
        /// </summary>
        public event Action BecameReady;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || remaining <= 0f)
                return;

            remaining -= deltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                BecameReady?.Invoke();
            }
        }

        /// <summary>
        /// Makes the ability available immediately, whatever it had left. The Wound Spring
        /// Stopgap (REWARDS.md §5) is "instant Vortex recharge" and this is that instant.
        ///
        /// <para>Fires <see cref="BecameReady"/> like an ordinary recharge would, so the readiness
        /// cue the player has learned means the same thing however the vortex came back.</para>
        /// </summary>
        public void Refresh()
        {
            if (remaining <= 0f)
                return;

            remaining = 0f;
            BecameReady?.Invoke();
        }

        /// <summary>Spends the ability. False when it is still recharging.</summary>
        public bool TryConsume()
        {
            if (!IsReady)
                return false;

            remaining = cooldownSeconds;
            return true;
        }

        /// <summary>
        /// A landed player hit shaves the cooldown. Deliberately silent when already ready, so
        /// aggression cannot bank credit against the <em>next</em> vortex — the reward is faster
        /// access to this one.
        /// </summary>
        public void RegisterLandedHit()
        {
            if (remaining <= 0f || perHitRefundSeconds <= 0f)
                return;

            remaining -= perHitRefundSeconds;
            if (remaining <= 0f)
            {
                remaining = 0f;
                BecameReady?.Invoke();
            }
        }

        /// <summary>Hands the ability straight back. For run restarts, not for gameplay.</summary>
        public void Reset() => remaining = 0f;
    }

    /// <summary>
    /// Spreads a fixed number of damage ticks across the spin's active window.
    ///
    /// <para>Ticks are counted rather than timed against a deadline so that a single long frame
    /// cannot swallow any of them — the same guarantee <see cref="AttackStateMachine"/> gives the
    /// hitbox itself. <see cref="Drain"/> exists for exactly that: whatever is still owed when the
    /// window closes is paid out rather than lost.</para>
    /// </summary>
    public sealed class VortexSpin
    {
        readonly int tickCount;
        readonly float activeSeconds;

        int fired;

        public VortexSpin(int tickCount, float activeSeconds)
        {
            this.tickCount = Mathf.Max(1, tickCount);
            this.activeSeconds = Mathf.Max(0f, activeSeconds);
        }

        public int TickCount => tickCount;

        public int Fired => fired;

        public void Reset() => fired = 0;

        /// <summary>
        /// How many ticks have come due since the last call, given how far into the active window
        /// the spin is. The first tick lands on the opening frame — under pressure the spin has to
        /// feel immediate, so nothing waits for a fraction of the window to elapse first.
        /// </summary>
        public int Due(float elapsedActiveSeconds)
        {
            int shouldHaveFired = activeSeconds <= 0f
                ? tickCount
                : Mathf.Clamp(Mathf.FloorToInt(elapsedActiveSeconds / activeSeconds * tickCount) + 1, 0, tickCount);

            int newly = shouldHaveFired - fired;
            if (newly <= 0)
                return 0;

            fired = shouldHaveFired;
            return newly;
        }

        /// <summary>Everything still owed when the window closes. Never lets a tick go missing.</summary>
        public int Drain()
        {
            int newly = tickCount - fired;
            fired = tickCount;
            return Mathf.Max(0, newly);
        }
    }
}
