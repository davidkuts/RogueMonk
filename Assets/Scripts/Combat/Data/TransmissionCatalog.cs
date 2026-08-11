using System.Collections.Generic;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Every transmission boon that can be offered, and the draft rule over them: pick ONE
    /// giver at random, then offer up to three of that giver's boons (BOONS.md §4's
    /// single-giver draft). Draws use shuffle-and-take-front, never pick-and-reject, so a
    /// draft always costs a deterministic number of draws from whatever stream pays for it.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Transmission Catalog", fileName = "TransmissionCatalog")]
    public sealed class TransmissionCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("The full offering pool across all givers.")]
        List<TransmissionBoonDefinition> boons = new List<TransmissionBoonDefinition>();
        [SerializeField, Tooltip("Offers per draft. A giver with fewer distinct boons offers what they have.")]
        int offerCount = 3;

        readonly List<GiverId> giverScratch = new List<GiverId>();
        readonly List<GiverId> fullGiverScratch = new List<GiverId>();
        readonly List<TransmissionBoonDefinition> boonScratch = new List<TransmissionBoonDefinition>();
        readonly List<SlotClaim> claimScratch = new List<SlotClaim>();

        public IReadOnlyList<TransmissionBoonDefinition> Boons => boons;

        /// <summary>
        /// Rolls one draft: a random giver among those with at least one offerable boon, then
        /// up to <see cref="offerCount"/> of their boons. Offerable means not owned AND allowed
        /// by <see cref="TransmissionDraftRules"/> — a slot claimed by one giver never shows
        /// another giver's boon for it again. Empty when nothing is offerable.
        /// </summary>
        public List<TransmissionBoonDefinition> RollDraft(
            IRandomSource random, IReadOnlyList<TransmissionBoonDefinition> owned)
        {
            var offer = new List<TransmissionBoonDefinition>();
            if (random == null)
                return offer;

            claimScratch.Clear();
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                if (owned[i] != null)
                    claimScratch.Add(new SlotClaim(owned[i].Giver, owned[i].Ability));
            }

            // Count each giver's offerable pool, preferring givers who can still fill a WHOLE
            // draft: a three-card choice beats fictional variety, so a giver reduced to one or
            // two boons only calls when nobody can do better (human call 2026-08-11 — drafts
            // were shrinking to two cards by the second pick).
            giverScratch.Clear();
            fullGiverScratch.Clear();
            for (int i = 0; i < boons.Count; i++)
            {
                TransmissionBoonDefinition boon = boons[i];
                if (boon == null || IsOwned(owned, boon) ||
                    !TransmissionDraftRules.IsOfferable(boon.Giver, boon.Ability, claimScratch))
                    continue;

                if (!giverScratch.Contains(boon.Giver))
                    giverScratch.Add(boon.Giver);

                if (CountOfferable(boon.Giver, owned) >= Mathf.Max(1, offerCount) &&
                    !fullGiverScratch.Contains(boon.Giver))
                    fullGiverScratch.Add(boon.Giver);
            }

            if (giverScratch.Count == 0)
                return offer;

            List<GiverId> pickFrom = fullGiverScratch.Count > 0 ? fullGiverScratch : giverScratch;
            GiverId giver = pickFrom[random.NextInt(0, pickFrom.Count)];

            boonScratch.Clear();
            for (int i = 0; i < boons.Count; i++)
            {
                TransmissionBoonDefinition boon = boons[i];
                if (boon != null && boon.Giver == giver && !IsOwned(owned, boon) &&
                    TransmissionDraftRules.IsOfferable(boon.Giver, boon.Ability, claimScratch))
                    boonScratch.Add(boon);
            }

            random.Shuffle(boonScratch);

            int take = Mathf.Min(Mathf.Max(1, offerCount), boonScratch.Count);
            for (int i = 0; i < take; i++)
                offer.Add(boonScratch[i]);

            return offer;
        }

        /// <summary>How many of one giver's boons are currently offerable. Uses the claim list built by the caller.</summary>
        int CountOfferable(GiverId giver, IReadOnlyList<TransmissionBoonDefinition> owned)
        {
            int count = 0;
            for (int i = 0; i < boons.Count; i++)
            {
                TransmissionBoonDefinition boon = boons[i];
                if (boon != null && boon.Giver == giver && !IsOwned(owned, boon) &&
                    TransmissionDraftRules.IsOfferable(boon.Giver, boon.Ability, claimScratch))
                    count++;
            }

            return count;
        }

        static bool IsOwned(IReadOnlyList<TransmissionBoonDefinition> owned, TransmissionBoonDefinition boon)
        {
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                if (owned[i] == boon)
                    return true;
            }

            return false;
        }
    }
}
