using System.Collections.Generic;
using Game.Core.Economy;
using Game.Core.Rng;

namespace Game.Level
{
    /// <summary>
    /// The tier-parity door-reward generator (REWARDS.md §8): at every fork, roll ONE quality
    /// tier, then give each door a DIFFERENT reward type at that tier. The player chooses what
    /// KIND of help — never whether they're getting scammed.
    ///
    /// Engine-free and deterministic: a fork of N doors always costs exactly 1 + N draws
    /// (one tier roll, one weighted type pick per door), so a seed reproduces every fork.
    /// </summary>
    public sealed class RewardRoller
    {
        readonly IRewardConfig config;
        readonly List<float> weightScratch = new List<float>();
        readonly List<RewardType> usedScratch = new List<RewardType>();

        public RewardRoller(IRewardConfig config)
        {
            this.config = config;
        }

        /// <summary>How many doors a fork can offer before types would have to repeat.</summary>
        public int MaxDistinctTypes
        {
            get
            {
                int count = 0;
                IReadOnlyList<RewardTypeOption> options = config.TypeOptions;
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].Enabled && options[i].Weight > 0f)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Rolls one fork. <paramref name="doorCount"/> is clamped to the number of enabled
        /// types, because duplicate types on one fork are exactly what parity forbids.
        /// </summary>
        public List<RewardChoice> RollFork(IRandomSource random, int doorCount)
        {
            var choices = new List<RewardChoice>();
            if (random == null || config == null)
                return choices;

            int doors = System.Math.Min(System.Math.Max(1, doorCount), MaxDistinctTypes);
            if (doors <= 0)
                return choices;

            RewardTier tier = RollTier(random);

            usedScratch.Clear();
            for (int door = 0; door < doors; door++)
            {
                RewardType? type = PickDistinctType(random);
                if (type == null)
                    break;

                usedScratch.Add(type.Value);
                choices.Add(new RewardChoice(type.Value, tier));
            }

            return choices;
        }

        RewardTier RollTier(IRandomSource random)
        {
            weightScratch.Clear();
            weightScratch.Add(Positive(config.TierWeight(RewardTier.Normal)));
            weightScratch.Add(Positive(config.TierWeight(RewardTier.Rare)));
            weightScratch.Add(Positive(config.TierWeight(RewardTier.Epic)));

            int index = random.PickWeighted(weightScratch);
            return index < 0 ? RewardTier.Normal : (RewardTier)index;
        }

        /// <summary>
        /// One weighted draw over the enabled types not yet used on this fork. Already-used
        /// types keep their list slot at weight zero, so the draw count per door is always
        /// exactly one whatever was picked before — the determinism rule the run seed leans on.
        /// </summary>
        RewardType? PickDistinctType(IRandomSource random)
        {
            weightScratch.Clear();
            IReadOnlyList<RewardTypeOption> options = config.TypeOptions;

            for (int i = 0; i < options.Count; i++)
            {
                RewardTypeOption option = options[i];
                bool usable = option.Enabled && option.Weight > 0f && !usedScratch.Contains(option.Type);
                weightScratch.Add(usable ? option.Weight : 0f);
            }

            int index = random.PickWeighted(weightScratch);
            return index < 0 ? (RewardType?)null : options[index].Type;
        }

        static float Positive(float value) => value > 0f ? value : 0f;
    }
}
