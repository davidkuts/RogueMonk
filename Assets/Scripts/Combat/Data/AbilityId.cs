namespace Game.Combat
{
    /// <summary>
    /// The player's ability slots (BOONS.md §4). Boons, rewards and every piece of gameplay
    /// logic reference THESE, never buttons: input rebinding is a planned feature, and buttons
    /// exist only in the input-binding layer. Current defaults are Square=ATK, X=BLINK,
    /// Circle=VORTEX, Triangle=SPLIT — defaults, not identities.
    /// </summary>
    public enum AbilityId
    {
        /// <summary>No slot — hits that belong to no player ability, or "applies to everything".</summary>
        None = 0,

        /// <summary>The auto-attack combo (punch, punch, kick).</summary>
        ATK = 1,

        /// <summary>The Blink — the time-dash with i-frames.</summary>
        BLINK = 2,

        /// <summary>The Undertow — spinning crane kick, AoE pull.</summary>
        VORTEX = 3,

        /// <summary>The Split Second's Riposte — the perfect-dodge-gated counter.</summary>
        SPLIT = 4,

        /// <summary>The Thrown Second. Reserved; the ability does not exist yet.</summary>
        CAST = 5,
    }

    /// <summary>
    /// An attack that knows which ability slot it belongs to, so slot-scoped boons can filter
    /// hits without the pipeline ever learning about buttons.
    /// </summary>
    public interface IAbilityTagged
    {
        AbilityId Ability { get; }
    }
}
