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

        /// <summary>
        /// Extra protection carried past the end of the i-frames, during which a hit is still
        /// nullified and still counts as a perfect dodge.
        ///
        /// It exists because melee and projectiles are not equally dodgeable. A bolt's hitbox
        /// travels toward the player, so any instant of the i-frame window can catch it; a melee
        /// swing is live for barely a tenth of a second and the player has to still be standing in
        /// it. Without this, one is comfortable and the other is frame-perfect.
        ///
        /// <para>This is the MELEE window — the generous one. See
        /// <see cref="ProjectileDodgeGraceSeconds"/> for why there are two.</para>
        /// </summary>
        float PerfectDodgeGraceSeconds { get; }

        /// <summary>
        /// The grace against a hitbox that travels to the player, which is the easier of the two
        /// to dodge and therefore gets the shorter window.
        ///
        /// <para>One number for both was the original shipping compromise and it broke the wrong
        /// way: sized for melee to be fair, it made projectiles trivial. Splitting it is what lets
        /// the melee window stay generous without a bolt becoming free.</para>
        /// </summary>
        float ProjectileDodgeGraceSeconds { get; }

        /// <summary>Number of charges the player holds.</summary>
        int MaxCharges { get; }

        /// <summary>Recharge time for a single spent charge. Charges recharge independently.</summary>
        float RechargeSeconds { get; }

        /// <summary>How long a dash press stays queued while the player cannot act on it.</summary>
        float BufferSeconds { get; }

        /// <summary>Speed the player carries out of a dash, as a fraction of walking top speed.</summary>
        float ExitSpeedFraction { get; }

        /// <summary>
        /// Extra clearance, past a dash-phaseable prop's far edge, required before the Blink is
        /// allowed to carry the player through it instead of stopping against it. Guards against
        /// popping out flush with the object's silhouette, and against phasing through something
        /// the dash does not actually have enough distance left to clear.
        /// </summary>
        float ObstaclePhaseLeewayMeters { get; }

        /// <summary>
        /// Distance travelled by normalized time, as a 0..1 fraction of <see cref="DistanceMeters"/>.
        /// Must be 0 at t=0 and 1 at t=1; the shape between is the dash's feel.
        /// </summary>
        float EvaluateTravel(float normalizedTime);
    }
}
