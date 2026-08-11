using System.Collections.Generic;
using System.Linq;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// The band-first generator (human redesign 2026-08-11): a fork rolls one quality band,
    /// the band decides what kinds of doors appear, boon bands are a single door, distinct
    /// types within a band, enable flags respected, deterministic per stream.
    /// </summary>
    public class RewardRollerTests
    {
        static RewardRoller Roller(FakeRewardConfig config = null) =>
            new RewardRoller(config ?? new FakeRewardConfig());

        [Test]
        public void AForkSharesOneBandAndNeverRepeatsAType()
        {
            var roller = Roller();
            var random = new XorShiftRandom(123u);

            for (int fork = 0; fork < 500; fork++)
            {
                List<RewardChoice> choices = roller.RollFork(random, 4);

                Assert.That(choices.Count, Is.GreaterThan(0));
                Assert.That(choices.Select(c => c.Band).Distinct().Count(), Is.EqualTo(1),
                    "one quality band per fork — the band IS the quality");
                Assert.That(choices.Select(c => c.Type).Distinct().Count(), Is.EqualTo(choices.Count),
                    "no duplicate types on one fork");
            }
        }

        [Test]
        public void EveryDoorMatchesItsBandsPool()
        {
            var roller = Roller();
            var random = new XorShiftRandom(9u);

            for (int fork = 0; fork < 500; fork++)
            {
                foreach (RewardChoice choice in roller.RollFork(random, 4))
                {
                    switch (choice.Band)
                    {
                        case RewardBand.Basic:
                            Assert.That(new[] { RewardType.Splice, RewardType.MinutesCache, RewardType.Stopgap },
                                Does.Contain(choice.Type),
                                "Basic is healing / run currency / consumables");
                            break;
                        case RewardBand.Valuable:
                            Assert.That(new[] { RewardType.HoursCache, RewardType.Stray },
                                Does.Contain(choice.Type),
                                "Valuable is meta currency and Strays");
                            break;
                        default:
                            Assert.That(choice.Type, Is.EqualTo(RewardType.Transmission),
                                "boon bands only ever offer the draft");
                            break;
                    }
                }
            }
        }

        [Test]
        public void BoonBandsAreASingleDoor()
        {
            var roller = Roller();
            var random = new XorShiftRandom(31u);

            for (int fork = 0; fork < 500; fork++)
            {
                List<RewardChoice> choices = roller.RollFork(random, 4);
                if (choices[0].Band == RewardBand.Boon || choices[0].Band == RewardBand.EliteBoon)
                {
                    Assert.That(choices.Count, Is.EqualTo(1),
                        "when the fork rolls boons, boons are the only option");
                    Assert.That(choices[0].Type, Is.EqualTo(RewardType.Transmission));
                }
            }
        }

        [Test]
        public void DisabledAndZeroWeightTypesAreNeverRolled()
        {
            var config = new FakeRewardConfig();
            config.Options = new List<RewardTypeOption>
            {
                new RewardTypeOption(RewardType.MinutesCache, RewardBand.Basic, true, 1f),
                new RewardTypeOption(RewardType.Splice, RewardBand.Basic, true, 0f),
                new RewardTypeOption(RewardType.Stray, RewardBand.Valuable, false, 5f),
                new RewardTypeOption(RewardType.Stopgap, RewardBand.Basic, true, 1f),
                new RewardTypeOption(RewardType.Transmission, RewardBand.Boon, true, 1f),
            };

            var roller = Roller(config);
            var random = new XorShiftRandom(7u);

            for (int fork = 0; fork < 300; fork++)
            {
                List<RewardChoice> choices = roller.RollFork(random, 4);
                Assert.That(choices.Any(c => c.Type == RewardType.Stray), Is.False, "disabled");
                Assert.That(choices.Any(c => c.Type == RewardType.Splice), Is.False, "zero weight");

                if (choices[0].Band == RewardBand.Basic)
                    Assert.That(choices.Count, Is.LessThanOrEqualTo(2),
                        "only two Basic types are actually available");
            }
        }

        [Test]
        public void AnEmptyBandDegradesToOneMinutesDoor()
        {
            var config = new FakeRewardConfig
            {
                BasicWeight = 0f,
                BoonWeight = 0f,
                EliteBoonWeight = 0f,
                ValuableWeight = 1f,
            };
            config.Options = new List<RewardTypeOption>
            {
                // Valuable can be rolled but holds nothing enabled.
                new RewardTypeOption(RewardType.MinutesCache, RewardBand.Basic, true, 1f),
                new RewardTypeOption(RewardType.HoursCache, RewardBand.Valuable, false, 1f),
            };

            List<RewardChoice> choices = Roller(config).RollFork(new XorShiftRandom(1u), 4);
            Assert.That(choices.Count, Is.EqualTo(1));
            Assert.That(choices[0].Type, Is.EqualTo(RewardType.MinutesCache),
                "a band with nothing enabled must not produce a doorless room");
        }

        [Test]
        public void BandWeightsShapeTheDistribution()
        {
            var config = new FakeRewardConfig
            {
                BasicWeight = 1f,
                ValuableWeight = 0f,
                BoonWeight = 0f,
                EliteBoonWeight = 0f,
            };
            var roller = Roller(config);
            var random = new XorShiftRandom(99u);

            for (int fork = 0; fork < 100; fork++)
            {
                foreach (RewardChoice choice in roller.RollFork(random, 3))
                    Assert.That(choice.Band, Is.EqualTo(RewardBand.Basic),
                        "with only Basic weighted, every fork is Basic");
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
                Assert.That(a[i].Band, Is.EqualTo(b[i].Band));
            }
        }

        [Test]
        public void EveryBandAndEnabledTypeEventuallyAppears()
        {
            var roller = Roller();
            var random = new XorShiftRandom(5u);
            var seenTypes = new HashSet<RewardType>();
            var seenBands = new HashSet<RewardBand>();

            for (int fork = 0; fork < 500; fork++)
            {
                foreach (RewardChoice choice in roller.RollFork(random, 4))
                {
                    seenTypes.Add(choice.Type);
                    seenBands.Add(choice.Band);
                }
            }

            Assert.That(seenBands, Is.EquivalentTo(new[]
            {
                RewardBand.Basic, RewardBand.Valuable, RewardBand.Boon, RewardBand.EliteBoon,
            }), "all four bands should appear across many forks");

            Assert.That(seenTypes, Is.SupersetOf(new[]
            {
                RewardType.Transmission, RewardType.MinutesCache, RewardType.HoursCache,
                RewardType.Splice, RewardType.Stray, RewardType.Stopgap,
            }), "all six launch types should appear across many forks");
        }
    }
}
