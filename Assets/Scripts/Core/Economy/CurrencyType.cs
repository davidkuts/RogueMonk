namespace Game.Core.Economy
{
    /// <summary>
    /// The four denominations of the run economy (REWARDS.md §1). The entire economy is
    /// denominated in time; what differs is scope and what each may buy.
    ///
    /// Hard rule from the spec: Seconds and Minutes are separate pipelines and never convert —
    /// boons must never compete with purchases for the same resource. Nothing in code may
    /// exchange one currency for another.
    /// </summary>
    public enum CurrencyType
    {
        /// <summary>Boon pipeline fuel. Run-scoped; shed by kills, auto-collected.</summary>
        Seconds = 0,

        /// <summary>Run currency for Supply Drops. Run-scoped; resets on death.</summary>
        Minutes = 1,

        /// <summary>Meta currency. Persists across runs (save data).</summary>
        Hours = 2,

        /// <summary>Premium meta currency. Persists across runs (save data).</summary>
        Amber = 3,
    }
}
