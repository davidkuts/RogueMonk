namespace Game.Core.Locomotion
{
    /// <summary>
    /// What KIND of thing is trying to hit the player, for the one rule that has to treat them
    /// differently: how long the perfect-dodge grace lasts.
    ///
    /// <para>A melee swing is live for barely a tenth of a second and the player must still be
    /// standing inside the arc when it fires — two conditions, both tight. A projectile's hitbox
    /// travels toward the player, so <em>any</em> instant of the protection window catches it and
    /// the position half is free. One grace number for both means either melee is frame-perfect or
    /// projectiles are trivial; the shipped 0.20 s picked the second, and playtest confirmed it
    /// ("perfect-dodging a projectile is far too easy").</para>
    ///
    /// <para>Deliberately coarse. This is not a taxonomy of attacks — it is the smallest split
    /// that makes the dodge fair, and it should stay that way unless a third class genuinely
    /// dodges differently.</para>
    /// </summary>
    public enum ThreatType
    {
        /// <summary>A hitbox that fires where the attacker is standing. The generous window.</summary>
        Melee = 0,

        /// <summary>A hitbox that travels to the player. The tight window.</summary>
        Projectile = 1,
    }
}
