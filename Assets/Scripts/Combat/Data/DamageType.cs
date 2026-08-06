namespace Game.Combat
{
    /// <summary>
    /// Carried on every attack from day one so the elemental boon system (DESIGN.md
    /// § Future system) can key off it without touching the resolver. MVP only ever
    /// uses <see cref="Physical"/>.
    /// </summary>
    public enum DamageType
    {
        Physical = 0,
        Fire = 1,
        Ice = 2,
        Wind = 3,
        Earth = 4,
        Nature = 5,
        Force = 6,
    }
}
