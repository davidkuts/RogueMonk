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
        readonly List<TransmissionBoonDefinition> boonScratch = new List<TransmissionBoonDefinition>();

        public IReadOnlyList<TransmissionBoonDefinition> Boons => boons;

        /// <summary>
        /// Rolls one draft: a random giver among those with at least one not-yet-owned boon,
        /// then up to <see cref="offerCount"/> of their boons. Empty when everything is owned.
        /// </summary>
        public List<TransmissionBoonDefinition> RollDraft(
            IRandomSource random, IReadOnlyList<TransmissionBoonDefinition> owned)
        {
            var offer = new List<TransmissionBoonDefinition>();
            if (random == null)
                return offer;

            giverScratch.Clear();
            for (int i = 0; i < boons.Count; i++)
            {
                TransmissionBoonDefinition boon = boons[i];
                if (boon == null || IsOwned(owned, boon))
                    continue;

                if (!giverScratch.Contains(boon.Giver))
                    giverScratch.Add(boon.Giver);
            }

            if (giverScratch.Count == 0)
                return offer;

            GiverId giver = giverScratch[random.NextInt(0, giverScratch.Count)];

            boonScratch.Clear();
            for (int i = 0; i < boons.Count; i++)
            {
                TransmissionBoonDefinition boon = boons[i];
                if (boon != null && boon.Giver == giver && !IsOwned(owned, boon))
                    boonScratch.Add(boon);
            }

            random.Shuffle(boonScratch);

            int take = Mathf.Min(Mathf.Max(1, offerCount), boonScratch.Count);
            for (int i = 0; i < take; i++)
                offer.Add(boonScratch[i]);

            return offer;
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
