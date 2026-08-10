namespace Game.Core.Economy
{
    /// <summary>
    /// Reward/boon quality tier, shared by the door-reward generator and the transmission
    /// (boon) system so the two can never disagree about what "Rare" means.
    ///
    /// Project rule (BOONS.md §3): rarity scales numbers only, NEVER mechanics. A Rare is a
    /// bigger Normal; an Epic is a bigger Rare. Perpetuals (legendaries) are deliberately not
    /// in this enum — they transform rather than scale, and are out of scope until the full
    /// boon system lands.
    /// </summary>
    public enum RewardTier
    {
        /// <summary>Reads as brass.</summary>
        Normal = 0,

        /// <summary>Reads as silver.</summary>
        Rare = 1,

        /// <summary>Reads as gold.</summary>
        Epic = 2,
    }
}
