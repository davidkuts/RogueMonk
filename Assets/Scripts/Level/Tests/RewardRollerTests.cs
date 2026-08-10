using System.Collections.Generic;
using System.Linq;
using Game.Core.Economy;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// The tier-parity generator on its own (REWARDS.md §8): one tier per fork, distinct
    /// types, enable flags respected, weights honoured, deterministic per stream.
    /// </summary>
    public class RewardRollerTests
    {
        static RewardRoller Roller(FakeRewardConfig config = null) =>
            new RewardRoller(config ?? new FakeRewardConfig());

        [Test]
        public void AForkSharesOneTierAndNeverRepeatsAType()
        {
            var roller = Roller();
            var random = new XorShiftRandom(123u);

            for (int fork = 0; fork < 500; fork++)
            {
                List<RewardChoice> choices = roller.RollFork(random, 4);

                Assert.That(choices.Count, Is.EqualTo(4));
                Assert.That(choices.Select(c => c.Tier).Distinct().Count(), Is.EqualTo(1),
                    "tier parity: the player chooses what KIND of help, never quality");
                Assert.That(choices.Select(c => c.Type).Distinct().Count(), Is.EqualTo(choices.Count),
                    "no duplicate types on one fork");
            }
        }

        [Test]
        public void DisabledAndZeroWeightTypesAreNeverRolled()
        {
            var config = new FakeRewardConfig();
            config.Options = new List<RewardTypeOption>
            {
                new RewardTypeOption(RewardType.MinutesCache, true, 1f),
                new RewardTypeOption(RewardType.Splice, true, 0f),
                new RewardTypeOption(RewardType.Stray, false, 5f),
                new RewardTypeOption(RewardType.Stopgap, true, 1f),
            };

            var roller = Roller(config);
            var random = new XorShiftRandom(7u);

            for (int fork = 0; fork < 200; fork++)
            {
                List<RewardChoice> choices = roller.RollFork(random, 4);
                Assert.That(choices.Count, Is.EqualTo(2), "only two types are actually available");
                Assert.That(choices.Any(c => c.Type == RewardType.Stray), Is.False, "disabled");
                Assert.That(choices.Any(c => c.Type == RewardType.Splice), Is.False, "zero weight");
            }
        }

        [Test]
        public void DoorCountIsClampedToTheDistinctTypesAvailable()
        {
            var config = new FakeRewardConfig();
            config.Options = new List<RewardTypeOption>
            {
                new RewardTypeOption(RewardType.MinutesCache, true, 1f),
                new RewardTypeOption(RewardType.Transmission, true, 1f),
            };

            List<RewardChoice> choices = Roller(config).RollFork(new XorShiftRandom(1u), 4);
            Assert.That(choices.Count, Is.EqualTo(2),
                "a fork can never offer more doors than there are distinct types");
        }

        [Test]
        public void TierWeightsShapeTheDistribution()
        {
            var config = new FakeRewardConfig { NormalWeight = 1f, RareWeight = 0f, EpicWeight = 0f };
            var roller = Roller(config);
            var random = new XorShiftRandom(99u);

            for (int fork = 0; fork < 100; fork++)
            {
                foreach (RewardChoice choice in roller.RollFork(random, 3))
                    Assert.That(choice.Tier, Is.EqualTo(RewardTier.Normal),
                        "with only Normal weighted, every fork is Normal");
            }
        }

        [Test]
        public void TheSameStreamProducesTheSameForks()
        {
            var a = Roller().RollFork(new XorShiftRandom(42u), 3);
            var b = Roller().RollFork(new XorShiftRandom(42u), 3);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].Type, Is.EqualTo(b[i].Type));
                Assert.That(a[i].Tier, Is.EqualTo(b[i].Tier));
            }
        }

        [Test]
        public void EveryEnabledTypeEventuallyAppears()
        {
            var roller = Roller();
            var random = new XorShiftRandom(5u);
            var seen = new HashSet<RewardType>();

            for (int fork = 0; fork < 300; fork++)
            {
                foreach (RewardChoice choice in roller.RollFork(random, 3))
                    seen.Add(choice.Type);
            }

            Assert.That(seen, Is.SupersetOf(new[]
            {
                RewardType.Transmission, RewardType.MinutesCache, RewardType.HoursCache,
                RewardType.Splice, RewardType.Stray, RewardType.Stopgap,
            }), "all six launch types should appear across many forks");
        }
    }
}
