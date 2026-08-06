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
        bool refundedThisDash;

        public DashCharges Charges { get; }

        public bool IsDashing { get; private set; }

        /// <summary>Fixed planar direction of the current dash. Retains the last dash's direction when idle.</summary>
        public Vector3 Direction { get; private set; } = Vector3.forward;

        /// <summary>Progress through the current dash, 0..1. Zero when not dashing.</summary>
        public float NormalizedTime =>
            !IsDashing || settings.DurationSeconds <= 0f ? 0f : Mathf.Clamp01(elapsed / settings.DurationSeconds);

        /// <summary>True while the dash's i-frames are live.</summary>
        public bool IsInvulnerable => IsDashing && NormalizedTime <= Mathf.Clamp01(settings.IFrameFraction);

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
            refundedThisDash = false;
            return true;
        }

        /// <summary>Advances the dash and returns the displacement to apply this frame.</summary>
        public Vector3 Tick(float deltaTime)
        {
            Charges.Tick(deltaTime);

            if (!IsDashing || deltaTime <= 0f)
                return Vector3.zero;

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

            return step;
        }

        /// <summary>
        /// Called by combat when an attack's active frames overlap these i-frames. Refunds the
        /// charge once per dash — a multi-hit attack cannot farm charges.
        /// Returns true if this call was the refunding one.
        /// </summary>
        public bool TryRegisterPerfectDodge()
        {
            if (!IsInvulnerable || refundedThisDash)
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
        }
    }
}
