namespace Game.Combat
{
    /// <summary>
    /// Which era an entity belongs to. Lives in the combat assembly because the hit pipeline
    /// filters on it (the Displaced Tooth Stray pays bonus damage against a tagged era);
    /// enemies implement <see cref="IEraTagged"/> to expose theirs.
    /// </summary>
    public enum Era
    {
        None = 0,
        Cretaceous = 1,
        Egypt = 2,
        Greece = 3,
        Medieval = 4,
        Present = 5,
    }

    /// <summary>A damageable that knows its era. Implemented by the enemy body.</summary>
    public interface IEraTagged
    {
        Era Era { get; }
    }
}
