namespace Game.Core.Locomotion
{
    /// <summary>
    /// Tuning contract for the dash. Implemented by a ScriptableObject at runtime and by
    /// fakes in tests, so the dash simulation never depends on an asset type.
    /// </summary>
    public interface IDashSettings
    {
        /// <summary>Total ground distance covered by one dash, in metres.</summary>
        float DistanceMeters { get; }

        /// <summary>Wall-clock length of the dash.</summary>
        float DurationSeconds { get; }

        /// <summary>Fraction of the dash (0..1) covered by invulnerability frames.</summary>
        float IFrameFraction { get; }

        /// <summary>Number of charges the player holds.</summary>
        int MaxCharges { get; }

        /// <summary>Recharge time for a single spent charge. Charges recharge independently.</summary>
        float RechargeSeconds { get; }

        /// <summary>How long a dash press stays queued while the player cannot act on it.</summary>
        float BufferSeconds { get; }

        /// <summary>Speed the player carries out of a dash, as a fraction of walking top speed.</summary>
        float ExitSpeedFraction { get; }

        /// <summary>
        /// Distance travelled by normalized time, as a 0..1 fraction of <see cref="DistanceMeters"/>.
        /// Must be 0 at t=0 and 1 at t=1; the shape between is the dash's feel.
        /// </summary>
        float EvaluateTravel(float normalizedTime);
    }
}
