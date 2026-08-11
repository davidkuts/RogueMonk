using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The anti-stacking rule: a slot belongs to the giver of its first boon. No cross-giver
    /// pile-up of damage + chill + DoT on one attack; deepening one giver's lane stays legal.
    /// </summary>
    public class TransmissionDraftRulesTests
    {
        static List<SlotClaim> Claims(params SlotClaim[] claims) => new List<SlotClaim>(claims);

        [Test]
        public void AnUnclaimedSlotIsOpenToEveryGiver()
        {
            var owned = Claims(new SlotClaim(GiverId.Overclock, AbilityId.VORTEX));

            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Stasis, AbilityId.ATK, owned), Is.True);
            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Fray, AbilityId.SPLIT, owned), Is.True);
        }

        [Test]
        public void AClaimedSlotRefusesEveryOtherGiver()
        {
            var owned = Claims(new SlotClaim(GiverId.Overclock, AbilityId.ATK));

            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Stasis, AbilityId.ATK, owned), Is.False,
                "Percy's chill must never stack onto Mara's combo");
            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Fray, AbilityId.ATK, owned), Is.False,
                "nor Dr. Reeve's DoT");
        }

        [Test]
        public void TheClaimingGiverMayKeepDeepeningItsOwnSlot()
        {
            var owned = Claims(new SlotClaim(GiverId.Overclock, AbilityId.ATK));

            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Overclock, AbilityId.ATK, owned), Is.True,
                "more Overclock on an Overclock slot is the build identity the rule protects");
        }

        [Test]
        public void ClaimsArePerSlotNotPerGiver()
        {
            var owned = Claims(
                new SlotClaim(GiverId.Overclock, AbilityId.ATK),
                new SlotClaim(GiverId.Stasis, AbilityId.SPLIT));

            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Stasis, AbilityId.VORTEX, owned), Is.True,
                "a giver blocked on one slot is still welcome on another");
            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Overclock, AbilityId.SPLIT, owned), Is.False,
                "even the giver of your first boon respects another giver's slot");
        }

        [Test]
        public void PassivesNeitherClaimNorConflict()
        {
            var owned = Claims(new SlotClaim(GiverId.Overclock, AbilityId.None));

            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Stasis, AbilityId.None, owned), Is.True,
                "slotless passives are a category, not a slot");
            Assert.That(TransmissionDraftRules.IsOfferable(GiverId.Stasis, AbilityId.ATK, owned), Is.True,
                "owning a passive claims nothing");
        }
    }
}
