using System.Collections.Generic;

namespace Game.Combat
{
    /// <summary>
    /// One owned boon as the draft rules see it: which giver holds which ability slot.
    /// </summary>
    public readonly struct SlotClaim
    {
        public readonly GiverId Giver;
        public readonly AbilityId Ability;

        public SlotClaim(GiverId giver, AbilityId ability)
        {
            Giver = giver;
            Ability = ability;
        }
    }

    /// <summary>
    /// The anti-stacking rule for transmission drafts (human call 2026-08-11): an ability slot
    /// is CLAIMED by the giver of the first boon installed on it, and further boons for that
    /// slot are only ever offered by that same giver. Without this, one attack could collect
    /// +damage from Mara, a chill from Percy and a DoT from Dr. Reeve and every other slot
    /// would stop mattering. Deepening one giver's lane on a slot stays legal — that is the
    /// build identity the rule protects.
    ///
    /// Engine-free and pure, so the rule itself is testable without assets.
    /// </summary>
    public static class TransmissionDraftRules
    {
        /// <summary>
        /// True when a boon by <paramref name="giver"/> for <paramref name="ability"/> may be
        /// offered given what is owned. Slotless boons (PASSIVE — <see cref="AbilityId.None"/>)
        /// claim nothing and conflict with nothing.
        /// </summary>
        public static bool IsOfferable(GiverId giver, AbilityId ability, IReadOnlyList<SlotClaim> owned)
        {
            if (ability == AbilityId.None || owned == null)
                return true;

            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].Ability == ability && owned[i].Giver != giver)
                    return false;
            }

            return true;
        }
    }
}
