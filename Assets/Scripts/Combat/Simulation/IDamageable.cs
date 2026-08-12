namespace Game.Combat
{
    /// <summary>
    /// Anything an attack can land on. Every damageable owns a status container from day one
    /// (DESIGN.md § Future system) even though stagger is the MVP's only status.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        StatusEffectContainer Statuses { get; }

        /// <summary>
        /// The stacking damage-over-time instances riding this target.
        ///
        /// <para>Separate from <see cref="Statuses"/> because the two answer different questions.
        /// A status is one flag with one duration that reapplication refreshes; a DoT is a pile of
        /// independent instances, each with its own clock and its own damage, that reapplication
        /// only ever adds to. Trying to make one container serve both is what the old burn model
        /// did, and it is why rarity used to buy a longer burn instead of a harder one.</para>
        /// </summary>
        DotContainer Dots { get; }

        /// <summary>
        /// Applies a fully resolved hit. By the time this runs the modifier pipeline has
        /// already had its say, so implementations must use the context's values rather than
        /// reading the attack definition again.
        /// </summary>
        void ApplyHit(in HitContext context);

        /// <summary>
        /// Forces a stagger for <paramref name="seconds"/> without going through poise.
        ///
        /// <para>Exists for the Undertow's arrival stagger. The ability voluntarily drags threats
        /// into hug range, so delivering them mid-wind-up would make it a self-inflicted wound
        /// rather than a tool — pull always means interrupt (DESIGN.md § The Vortex).</para>
        ///
        /// <para>The caller does not decide who is eligible: the implementation does, because
        /// eligibility is a stagger-tier question and the tier lives with the enemy.</para>
        /// </summary>
        void ApplyStagger(float seconds);
    }
}
