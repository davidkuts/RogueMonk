using System;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Engine-free dash simulation: a fixed-direction burst along a travel curve, covering
    /// exactly <see cref="IDashSettings.DistanceMeters"/> over
    /// <see cref="IDashSettings.DurationSeconds"/>. Invulnerability covers the leading
    /// fraction of the dash; surviving an attack inside that window refunds the charge
    /// (perfect dodge). Produces per-frame displacement — the adapter feeds it to the
    /// CharacterController, which is what actually stops the dash at walls.
    /// </summary>
    public sealed class PlayerDash
    {
        readonly IDashSettings settings;

        float elapsed;
        float travelled;

        /// <summary>Seconds since the grace opened. Compared against each threat's own allowance.</summary>
        float graceElapsed;
        bool graceOpen;
        bool refundedThisDash;

        public DashCharges Charges { get; }

        public bool IsDashing { get; private set; }

        /// <summary>Fixed planar direction of the current dash. Retains the last dash's direction when idle.</summary>
        public Vector3 Direction { get; private set; } = Vector3.forward;

        /// <summary>Progress through the current dash, 0..1. Zero when not dashing.</summary>
        public float NormalizedTime =>
            !IsDashing || settings.DurationSeconds <= 0f ? 0f : Mathf.Clamp01(elapsed / settings.DurationSeconds);

        /// <summary>
        /// Ground distance still left to cover this dash. Zero when not dashing. The adapter uses
        /// this to judge whether a dash-phaseable prop ahead can actually be cleared, rather than
        /// against the dash's full distance, which would let a prop hit near the very end of the
        /// dash count as clearable when there is no travel left to carry the player past it.
        /// </summary>
        public float RemainingDistance => IsDashing ? Mathf.Max(0f, settings.DistanceMeters - travelled) : 0f;

        /// <summary>
        /// Runtime multiplier on the settings' i-frame fraction, for boons that extend the
        /// Blink's protection (Ward's lane). 1 leaves the asset value untouched; the product is
        /// clamped to 0..1 like the fraction itself. Owned wholesale by the boon system, which
        /// recomputes it from its loadout rather than incrementing it.
        /// </summary>
        public float IFrameFractionMultiplier { get; set; } = 1f;

        /// <summary>
        /// Runtime multiplier on the perfect-dodge grace window (Ward's Read the Room). Same
        /// ownership rule as <see cref="IFrameFractionMultiplier"/>: recomputed, never nudged.
        /// </summary>
        public float DodgeGraceMultiplier { get; set; } = 1f;

        /// <summary>
        /// Flux's Skip Frame: chance a started dash hands its own charge straight back. Zero
        /// without the boon. Same ownership rule as the multipliers above.
        /// </summary>
        public float RefundChance { get; set; }

        /// <summary>
        /// Where the Skip Frame roll comes from. Never the run stream — how often the player
        /// dashes would otherwise change what level a quoted seed generates.
        /// </summary>
        public Game.Core.Rng.IRandomSource RefundStream { get; set; }

        /// <summary>True while the dash's i-frames proper are live.</summary>
        public bool IsInvulnerable =>
            IsDashing && NormalizedTime <= Mathf.Clamp01(settings.IFrameFraction * Mathf.Max(0f, IFrameFractionMultiplier));

        /// <summary>
        /// The grace this threat class gets, after the boon multiplier. Melee is the generous
        /// window; a projectile, which any instant of the window would catch, gets the short one.
        /// </summary>
        public float GraceSecondsFor(ThreatType threat)
        {
            float authored = threat == ThreatType.Projectile
                ? settings.ProjectileDodgeGraceSeconds
                : settings.PerfectDodgeGraceSeconds;

            return Mathf.Max(0f, authored * Mathf.Max(0f, DodgeGraceMultiplier));
        }

        /// <summary>
        /// True during the trailing grace against <paramref name="threat"/>. Kept separate from
        /// <see cref="IsInvulnerable"/> so the two can be read and tuned independently, even though
        /// both protect the player.
        ///
        /// <para>ONE timer backs both windows rather than two running in parallel: the grace opens
        /// once, when the i-frames close, and each threat class simply has a different allowance
        /// against the same elapsed clock. Two timers would let a dash be simultaneously inside and
        /// outside its own grace depending on who asked.</para>
        /// </summary>
        public bool IsInDodgeGraceFor(ThreatType threat) =>
            graceOpen && graceElapsed < GraceSecondsFor(threat);

        /// <summary>The generous window, for callers with no threat to name (the debug overlay, HUD).</summary>
        public bool IsInDodgeGrace => IsInDodgeGraceFor(ThreatType.Melee);

        /// <summary>
        /// The window that actually matters to combat: i-frames plus the grace for this threat.
        /// Anything asking "can this hit the player right now" wants this, not the raw i-frames.
        /// </summary>
        public bool IsProtectedAgainst(ThreatType threat) => IsInvulnerable || IsInDodgeGraceFor(threat);

        /// <summary>The most protective reading, for display and for callers with no threat in hand.</summary>
        public bool IsProtected => IsProtectedAgainst(ThreatType.Melee);

        /// <summary>Seconds of melee grace left, for the debug overlay.</summary>
        public float DodgeGraceRemaining =>
            graceOpen ? Mathf.Max(0f, GraceSecondsFor(ThreatType.Melee) - graceElapsed) : 0f;

        /// <summary>True when a dash could be started right now.</summary>
        public bool CanStart => !IsDashing && Charges.HasCharge;

        /// <summary>Raised when a perfect dodge refunds a charge — hook for SFX/flash.</summary>
        public event Action PerfectDodged;

        public PlayerDash(IDashSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Charges = new DashCharges(settings);
        }

        /// <summary>
        /// Spends a charge and starts a dash along <paramref name="direction"/>. Returns false
        /// if already dashing, out of charges, or the direction is degenerate.
        /// </summary>
        public bool TryStart(Vector3 direction)
        {
            direction.y = 0f;
            if (IsDashing || direction.sqrMagnitude <= 0f || !Charges.TrySpend())
                return false;

            Direction = direction.normalized;
            IsDashing = true;
            elapsed = 0f;
            travelled = 0f;

            // A fresh dash owns the protection outright; leftover grace from the previous one
            // would let a second dash inherit a window it did not earn.
            graceOpen = false;
            graceElapsed = 0f;
            refundedThisDash = false;

            // Skip Frame rolls on the START of the dash, not the end: the player has to know
            // immediately whether they still hold a charge, because that is what the next half
            // second of decisions depends on.
            //
            // It deliberately does NOT set refundedThisDash — a free dash that also gave up its
            // perfect-dodge refund would punish the player for the boon proccing.
            if (RefundChance > 0f && RefundStream != null && RefundStream.NextFloat() < RefundChance)
            {
                Charges.Refund();
                SkipFrames++;
            }

            return true;
        }

        /// <summary>How many times Skip Frame has handed a charge back, for the debug overlay.</summary>
        public int SkipFrames { get; private set; }

        /// <summary>Advances the dash and returns the displacement to apply this frame.</summary>
        public Vector3 Tick(float deltaTime)
        {
            Charges.Tick(deltaTime);

            // Runs whether or not a dash is live: the grace deliberately outlasts the dash itself.
            // Closed once the LONGEST allowance has run out, so the shorter one expiring first
            // does not take the other down with it.
            if (deltaTime > 0f && graceOpen)
            {
                graceElapsed += deltaTime;

                float longest = Mathf.Max(
                    GraceSecondsFor(ThreatType.Melee), GraceSecondsFor(ThreatType.Projectile));
                if (graceElapsed >= longest)
                    graceOpen = false;
            }

            if (!IsDashing || deltaTime <= 0f)
                return Vector3.zero;

            bool wasInvulnerable = IsInvulnerable;

            elapsed += deltaTime;

            float duration = Mathf.Max(1e-5f, settings.DurationSeconds);
            bool finished = elapsed >= duration;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            // Force the final sample to the full distance so the dash always covers exactly
            // DistanceMeters, whatever shape the curve has.
            float targetDistance = finished
                ? settings.DistanceMeters
                : settings.EvaluateTravel(normalizedTime) * settings.DistanceMeters;

            Vector3 step = Direction * (targetDistance - travelled);
            travelled = targetDistance;

            if (finished)
                IsDashing = false;

            // Open the grace on the frame the i-frames close, whether they ran out mid-dash or the
            // dash simply ended. Opening it here rather than at dash start means the two windows
            // are contiguous and the total is exactly i-frames + grace.
            if (wasInvulnerable && !IsInvulnerable)
            {
                graceOpen = true;
                graceElapsed = 0f;
            }

            return step;
        }

        /// <summary>
        /// Called by combat when an attack's active frames overlap these i-frames. Refunds the
        /// charge once per dash — a multi-hit attack cannot farm charges.
        /// Returns true if this call was the refunding one.
        /// </summary>
        public bool TryRegisterPerfectDodge(ThreatType threat = ThreatType.Melee)
        {
            if (!IsProtectedAgainst(threat) || refundedThisDash)
                return false;

            refundedThisDash = true;
            Charges.Refund();
            PerfectDodged?.Invoke();
            return true;
        }

        /// <summary>Ends the dash immediately without refunding. The charge stays spent.</summary>
        public void Cancel()
        {
            IsDashing = false;
            elapsed = 0f;
            travelled = 0f;
            graceOpen = false;
            graceElapsed = 0f;
        }
    }
}
