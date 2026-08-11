using System.Collections.Generic;
using Game.Core.Economy;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// One card of a rolled draft: a boon at a rolled rarity. Rarity is per CARD, the Hades
    /// way — two drafts of the same boon can offer it at different qualities, and rarity still
    /// scales numbers only, never mechanics.
    /// </summary>
    public readonly struct TransmissionOffer
    {
        public readonly TransmissionBoonDefinition Definition;
        public readonly RewardTier Rarity;

        public TransmissionOffer(TransmissionBoonDefinition definition, RewardTier rarity)
        {
            Definition = definition;
            Rarity = rarity;
        }
    }

    /// <summary>
    /// Every transmission boon that can be offered, and the draft rules over them: the
    /// ordinary draft picks ONE giver and offers up to three of their boons; the elite draft
    /// (an EliteBoon door) puts TWO givers on the channel at once — one boon each, rolled at
    /// higher rarities. Draws use shuffle-and-take-front, never pick-and-reject, so a draft
    /// costs a deterministic number of draws from whatever stream pays for it.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Transmission Catalog", fileName = "TransmissionCatalog")]
    public sealed class TransmissionCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("The full offering pool across all givers.")]
        List<TransmissionBoonDefinition> boons = new List<TransmissionBoonDefinition>();
        [SerializeField, Tooltip("Offers per draft. A giver with fewer distinct boons offers what they have.")]
        int offerCount = 3;

        [Header("Per-card rarity roll (ordinary draft)")]
        [SerializeField] float normalRarityWeight = 6f;
        [SerializeField] float rareRarityWeight = 3f;
        [SerializeField] float epicRarityWeight = 1f;

        [Header("Per-card rarity roll (elite draft)")]
        [SerializeField, Tooltip("Zero by default: an elite card is never merely Normal.")]
        float eliteNormalRarityWeight;
        [SerializeField] float eliteRareRarityWeight = 7f;
        [SerializeField] float eliteEpicRarityWeight = 3f;

        readonly List<GiverId> giverScratch = new List<GiverId>();
        readonly List<GiverId> fullGiverScratch = new List<GiverId>();
        readonly List<TransmissionBoonDefinition> boonScratch = new List<TransmissionBoonDefinition>();
        readonly List<SlotClaim> claimScratch = new List<SlotClaim>();

        public IReadOnlyList<TransmissionBoonDefinition> Boons => boons;

        /// <summary>
        /// Rolls the ordinary draft: a random giver among those with at least one offerable
        /// boon, then up to <see cref="offerCount"/> of their boons, each card rolling its own
        /// rarity. Offerable means not owned AND allowed by
        /// <see cref="TransmissionDraftRules"/> — a slot claimed by one giver never shows
        /// another giver's boon for it again. Empty when nothing is offerable.
        /// </summary>
        public List<TransmissionOffer> RollDraft(
            IRandomSource random, IReadOnlyList<TransmissionBoonDefinition> owned)
        {
            var offer = new List<TransmissionOffer>();
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
                offer.Add(new TransmissionOffer(boonScratch[i],
                    RollRarity(random, normalRarityWeight, rareRarityWeight, epicRarityWeight)));

            return offer;
        }

        /// <summary>
        /// Rolls the elite draft (an EliteBoon door): two DIFFERENT givers, one offerable boon
        /// each, at the elite rarity weights — never merely Normal by default. With only one
        /// giver left it degrades to a single elite card rather than nothing.
        /// </summary>
        public List<TransmissionOffer> RollEliteDraft(
            IRandomSource random, IReadOnlyList<TransmissionBoonDefinition> owned)
        {
            var offer = new List<TransmissionOffer>();
            if (random == null)
                return offer;

            claimScratch.Clear();
            for (int i = 0; owned != null && i < owned.Count; i++)
            {
                if (owned[i] != null)
                    claimScratch.Add(new SlotClaim(owned[i].Giver, owned[i].Ability));
            }

            giverScratch.Clear();
            for (int i = 0; i < boons.Count; i++)
            {
                TransmissionBoonDefinition boon = boons[i];
                if (boon == null || IsOwned(owned, boon) ||
                    !TransmissionDraftRules.IsOfferable(boon.Giver, boon.Ability, claimScratch))
                    continue;

                if (!giverScratch.Contains(boon.Giver))
                    giverScratch.Add(boon.Giver);
            }

            if (giverScratch.Count == 0)
                return offer;

            random.Shuffle(giverScratch);

            int givers = Mathf.Min(2, giverScratch.Count);
            for (int g = 0; g < givers; g++)
            {
                boonScratch.Clear();
                for (int i = 0; i < boons.Count; i++)
                {
                    TransmissionBoonDefinition boon = boons[i];
                    if (boon != null && boon.Giver == giverScratch[g] && !IsOwned(owned, boon) &&
                        TransmissionDraftRules.IsOfferable(boon.Giver, boon.Ability, claimScratch))
                        boonScratch.Add(boon);
                }

                random.Shuffle(boonScratch);
                if (boonScratch.Count > 0)
                    offer.Add(new TransmissionOffer(boonScratch[0],
                        RollRarity(random, eliteNormalRarityWeight, eliteRareRarityWeight, eliteEpicRarityWeight)));
            }

            return offer;
        }

        readonly List<float> rarityScratch = new List<float>(3);

        RewardTier RollRarity(IRandomSource random, float normal, float rare, float epic)
        {
            rarityScratch.Clear();
            rarityScratch.Add(Mathf.Max(0f, normal));
            rarityScratch.Add(Mathf.Max(0f, rare));
            rarityScratch.Add(Mathf.Max(0f, epic));

            int index = random.PickWeighted(rarityScratch);
            return index < 0 ? RewardTier.Normal : (RewardTier)index;
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
