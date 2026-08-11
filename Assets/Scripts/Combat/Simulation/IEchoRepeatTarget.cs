namespace Game.Combat
{
    /// <summary>
    /// A body that can take an Echo repeat — damage arriving outside the hit resolver.
    ///
    /// <para>Deliberately separate from <see cref="IDamageable"/> rather than a method on it.
    /// A repeat is not a hit: it carries no attack, no direction, no knockback and no poise, so
    /// it cannot be expressed as a <see cref="HitContext"/> without inventing four values that
    /// would then be wrong. Keeping it its own interface also means the player — who is an
    /// <c>IDamageable</c> too — simply does not implement it, and can never be echoed.</para>
    ///
    /// <para>Implementors are responsible for the same things a burn tick is: refusing the damage
    /// while a guard is up, reporting the number so the player can see the lane working, and not
    /// re-entering the pipeline.</para>
    /// </summary>
    public interface IEchoRepeatTarget
    {
        void ApplyEchoRepeat(float damage, DamageType damageType);
    }
}
