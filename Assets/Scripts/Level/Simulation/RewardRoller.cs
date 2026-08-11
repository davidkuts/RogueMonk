using System.Collections.Generic;
using Game.Core.Rng;

namespace Game.Level
{
    /// <summary>
    /// The door-reward generator, band-first (human redesign 2026-08-11): a fork rolls ONE
    /// quality band, and the band decides what kind of doors appear. Basic and Valuable forks
    /// offer 1–N doors of DISTINCT types from that band's pool; Boon and EliteBoon forks offer
    /// exactly one transmission door — when the fork rolls boons, boons are the only option,
    /// and how good the cards are is the draft's own rarity roll, not the door's.
    ///
    /// Engine-free and deterministic: a fork costs exactly one band roll plus one weighted
    /// pick per door, so a seed reproduces every fork.
    /// </summary>
    public sealed class RewardRoller
    {
        readonly IRewardConfig config;
        readonly List<float> weightScratch = new List<float>();
        readonly List<RewardType> usedScratch = new List<RewardType>();
        readonly List<Game.Combat.GiverId> giverScratch = new List<Game.Combat.GiverId>();

        public RewardRoller(IRewardConfig config)
        {
            this.config = config;
        }

        /// <summary>Distinct enabled types available in one band.</summary>
        public int DistinctTypesInBand(RewardBand band)
        {
            int count = 0;
            IReadOnlyList<RewardTypeOption> options = config.TypeOptions;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Enabled && options[i].Weight > 0f && options[i].Band == band)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Rolls one fork. <paramref name="doorCount"/> is an upper bound; the band's pool
        /// clamps it, and the two boon bands always produce a single door.
        /// </summary>
        public List<RewardChoice> RollFork(IRandomSource random, int doorCount)
        {
            var choices = new List<RewardChoice>();
            if (random == null || config == null)
                return choices;

            RewardBand band = RollBand(random);

            // A boon fork offers a CHOICE of givers (human rule 2026-08-11: always at least
            // two boon options): each door is a Transmission pinned to a different giver, and
            // the door wears that giver's colour. The elite band stays a single door — the
            // two-giver choice happens inside its draft.
            if (band == RewardBand.Boon)
            {
                IReadOnlyList<Game.Combat.GiverId> givers = config.BoonGivers;
                if (givers == null || givers.Count == 0)
                {
                    choices.Add(new RewardChoice(RewardType.Transmission, band));
                    return choices;
                }

                giverScratch.Clear();
                for (int i = 0; i < givers.Count; i++)
                    giverScratch.Add(givers[i]);
                random.Shuffle(giverScratch);

                // Exactly two whenever two givers exist — a real choice without diluting the
                // fork into a boon buffet.
                int boonDoors = System.Math.Min(2, giverScratch.Count);
                for (int i = 0; i < boonDoors; i++)
                    choices.Add(new RewardChoice(RewardType.Transmission, band, giverScratch[i]));

                return choices;
            }

            if (band == RewardBand.EliteBoon)
            {
                choices.Add(new RewardChoice(RewardType.Transmission, band));
                return choices;
            }

            int doors = System.Math.Min(System.Math.Max(1, doorCount), DistinctTypesInBand(band));
            if (doors <= 0)
            {
                // A band with nothing enabled degrades to one Basic minutes door rather than a
                // doorless room.
                choices.Add(new RewardChoice(RewardType.MinutesCache, RewardBand.Basic));
                return choices;
            }

            usedScratch.Clear();
            for (int door = 0; door < doors; door++)
            {
                RewardType? type = PickDistinctType(random, band);
                if (type == null)
                    break;

                usedScratch.Add(type.Value);
                choices.Add(new RewardChoice(type.Value, band));
            }

            return choices;
        }

        RewardBand RollBand(IRandomSource random)
        {
            weightScratch.Clear();
            weightScratch.Add(Positive(config.BandWeight(RewardBand.Basic)));
            weightScratch.Add(Positive(config.BandWeight(RewardBand.Valuable)));
            weightScratch.Add(Positive(config.BandWeight(RewardBand.Boon)));
            weightScratch.Add(Positive(config.BandWeight(RewardBand.EliteBoon)));

            int index = random.PickWeighted(weightScratch);
            return index < 0 ? RewardBand.Basic : (RewardBand)index;
        }

        /// <summary>
        /// One weighted draw over the band's enabled types not yet used on this fork.
        /// Already-used types keep their list slot at weight zero, so the draw count per door
        /// is always exactly one whatever was picked before — the determinism the seed leans on.
        /// </summary>
        RewardType? PickDistinctType(IRandomSource random, RewardBand band)
        {
            weightScratch.Clear();
            IReadOnlyList<RewardTypeOption> options = config.TypeOptions;

            for (int i = 0; i < options.Count; i++)
            {
                RewardTypeOption option = options[i];
                bool usable = option.Enabled && option.Weight > 0f && option.Band == band &&
                              !usedScratch.Contains(option.Type);
                weightScratch.Add(usable ? option.Weight : 0f);
            }

            int index = random.PickWeighted(weightScratch);
            return index < 0 ? (RewardType?)null : options[index].Type;
        }

        static float Positive(float value) => value > 0f ? value : 0f;
    }
}
