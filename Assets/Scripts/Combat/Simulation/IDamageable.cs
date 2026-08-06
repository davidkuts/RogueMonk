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
        /// Applies a fully resolved hit. By the time this runs the modifier pipeline has
        /// already had its say, so implementations must use the context's values rather than
        /// reading the attack definition again.
        /// </summary>
        void ApplyHit(in HitContext context);
    }
}
