using System.Linq;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// The door plan: a cleared room offers a seeded 1–4 reward doors obeying tier parity
    /// (one tier per fork, distinct types), the room before the boss offers exactly one
    /// boss-marked door, and the whole arrangement replays from the seed like everything else.
    /// </summary>
    public class ExitPlanTests
    {
        static LevelPlan Generate(uint seed) =>
            new LevelGenerator(new FakeGenerationSettings()).Generate(new RunContext(seed));

        [Test]
        public void DoorCountsStayBetweenOneAndFourAndTypesAreDistinct()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed);
                int lastStandard = plan.RoomCount - 2;

                for (int r = 0; r < plan.RoomCount; r++)
                {
                    RoomPlan room = plan.Rooms[r];

                    if (room.IsBossRoom)
                    {
                        Assert.That(room.ExitDoorCount, Is.EqualTo(0), "the boss room needs no door");
                        continue;
                    }

                    if (r == lastStandard)
                        continue; // covered by TheDoorToTheBossIsAlwaysAloneAndMarked

                    Assert.That(room.ExitDoorCount, Is.InRange(1, LevelGenerator.MaxExitDoors), $"seed {seed} room {r}");
                    Assert.That(room.ExitRewards.Select(c => c.Type).Distinct().Count(), Is.EqualTo(room.ExitDoorCount),
                        $"seed {seed} room {r}: reward types on one fork must be distinct");
                    Assert.That(room.ExitRewards.All(c => !c.IsBossDoor), Is.True,
                        $"seed {seed} room {r}: ordinary doors never carry the boss mark");
                }
            }
        }

        [Test]
        public void TierParityHoldsOnEveryFork()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount - 2; r++)
                {
                    RoomPlan room = plan.Rooms[r];
                    Assert.That(room.ExitRewards.Select(c => c.Tier).Distinct().Count(), Is.EqualTo(1),
                        $"seed {seed} room {r}: every door on a fork offers the same tier");
                }
            }
        }

        [Test]
        public void DisabledTypesNeverAppear()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount; r++)
                {
                    Assert.That(plan.Rooms[r].ExitRewards.Any(
                            c => !c.IsBossDoor &&
                                 (c.Type == RewardType.Recalibration || c.Type == RewardType.SupplyDrop)),
                        Is.False,
                        $"seed {seed} room {r}: disabled reward types must never be generated");
                }
            }
        }

        [Test]
        public void TheDoorToTheBossIsAlwaysAloneAndMarked()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed);
                RoomPlan beforeBoss = plan.Rooms[plan.RoomCount - 2];

                Assert.That(beforeBoss.ExitDoorCount, Is.EqualTo(1),
                    $"seed {seed}: the boss is never optional and never a surprise");
                Assert.That(beforeBoss.ExitRewards[0].IsBossDoor, Is.True,
                    $"seed {seed}: the one door before the boss carries the boss mark");
            }
        }

        [Test]
        public void EveryDoorCountAppearsAcrossManySeeds()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount - 2; r++)
                    seen.Add(plan.Rooms[r].ExitDoorCount);
            }

            Assert.That(seen, Is.EquivalentTo(new[] { 1, 2, 3, 4 }),
                "across many seeds every door count from 1 to 4 should occur");
        }

        [Test]
        public void EveryTierAppearsAcrossManySeeds()
        {
            var seen = new System.Collections.Generic.HashSet<Game.Core.Economy.RewardTier>();
            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount - 2; r++)
                {
                    foreach (RewardChoice choice in plan.Rooms[r].ExitRewards)
                        seen.Add(choice.Tier);
                }
            }

            Assert.That(seen.Count, Is.EqualTo(3),
                "across many seeds Normal, Rare and Epic forks should all occur");
        }

        [Test]
        public void TheDoorPlanReplaysFromTheSeed()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                LevelPlan a = Generate(seed);
                LevelPlan b = Generate(seed);

                for (int r = 0; r < a.RoomCount; r++)
                {
                    Assert.That(b.Rooms[r].ExitDoorCount, Is.EqualTo(a.Rooms[r].ExitDoorCount), $"seed {seed} room {r}");
                    for (int d = 0; d < a.Rooms[r].ExitDoorCount; d++)
                    {
                        Assert.That(b.Rooms[r].ExitRewards[d].Type, Is.EqualTo(a.Rooms[r].ExitRewards[d].Type), $"seed {seed} room {r} door {d}");
                        Assert.That(b.Rooms[r].ExitRewards[d].Tier, Is.EqualTo(a.Rooms[r].ExitRewards[d].Tier), $"seed {seed} room {r} door {d}");
                        Assert.That(b.Rooms[r].ExitRewards[d].IsBossDoor, Is.EqualTo(a.Rooms[r].ExitRewards[d].IsBossDoor), $"seed {seed} room {r} door {d}");
                    }
                }
            }
        }
    }
}
