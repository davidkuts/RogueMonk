using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// Whether a landed hit cancels the wind-up it interrupted (human call 2026-08-11): a
    /// read answered with the combo or the Riposte stops a charging attack dead. Engine-free
    /// so the rule itself is testable; the TIER questions (Immune never interrupted, intact
    /// armour shrugging it off) deliberately stay in <c>EnemyActor.ApplyStagger</c>, which
    /// already owns them — this only decides whether the hit even asks.
    /// </summary>
    public static class WindupInterrupt
    {
        /// <summary>
        /// True when this hit should request an interrupt: the target is mid-wind-up, the
        /// struck zone is not intact amber, and the hit came from the player's combo (ATK) or
        /// Riposte (SPLIT). Untagged hits — every enemy attack, friendly fire, the echo — never
        /// interrupt; the Undertow has its own arrival stagger and stays out of this.
        /// </summary>
        public static bool ShouldInterrupt(IAttackDefinition attack, bool windingUp, bool zoneBlocksStagger)
        {
            if (!windingUp || zoneBlocksStagger)
                return false;

            return attack is IAbilityTagged tagged &&
                   (tagged.Ability == AbilityId.ATK || tagged.Ability == AbilityId.SPLIT);
        }
    }
}
