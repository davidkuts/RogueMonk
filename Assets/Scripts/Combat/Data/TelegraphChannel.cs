namespace Game.Combat
{
    /// <summary>
    /// What a telegraph <em>means</em>, as opposed to what colour it happens to be.
    ///
    /// <para>DESIGN.md's grammar rule is "same colour = same threat type". Holding a raw
    /// <c>Color</c> on every attack asset makes that rule a convention nobody can enforce: twenty
    /// assets each carry their own idea of red, and a new enemy can invent a hue that already
    /// means something else. Naming the channel instead makes the asset state a design intent and
    /// leaves exactly one place — <see cref="TelegraphPalette"/> — holding the values.</para>
    ///
    /// <para>Hue assignments are locked in DESIGN.md § Telegraph grammar. The two that moved on
    /// 2026-08-09, when ENEMIES_BIOME1.md § 1 claimed amber for hardened time, are recorded on the
    /// members below.</para>
    /// </summary>
    public enum TelegraphChannel
    {
        /// <summary>
        /// Use the colour authored on the asset. The default, so every attack that shipped before
        /// the palette existed keeps precisely the colour it had.
        /// </summary>
        Custom = 0,

        /// <summary>Red. A melee arc or a centred burst — "there is a swing coming".</summary>
        MeleeArc = 1,

        /// <summary>
        /// Magenta. An incoming projectile.
        ///
        /// <para>Was amber until 2026-08-09. ENEMIES_BIOME1.md § 1 reserves amber for solidified
        /// time across the whole game, and three meanings on one hue is what the grammar exists to
        /// prevent. The armour colour is load-bearing narratively; the projectile hue was one
        /// value in two assets, so the projectile moved.</para>
        /// </summary>
        Projectile = 2,

        /// <summary>Violet. A gap-closer — "it is coming to you".</summary>
        GapCloser = 3,

        /// <summary>Lime-yellow. A delayed static floor hazard — "this ground is about to hurt".</summary>
        GroundHazard = 4,

        /// <summary>
        /// Amber. Solidified time: armour plates, stall zones, boss armour patches. The player's
        /// rule is "that is hardened time — it blocks stagger or it blocks movement".
        /// </summary>
        HardenedTime = 5,

        /// <summary>
        /// Pale bone-cyan. A temporal echo replaying an attack that already happened.
        ///
        /// <para>Deliberately paler and less saturated than the dash hue. The dash blue is locked
        /// as the Second Hand's own colour, so an echo wearing it exactly would put the player's
        /// signature colour on the thing trying to kill them.</para>
        /// </summary>
        Echo = 6,
    }
}
