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
                        Assert.That(room.ExitDoorCount, Is.EqualTo(1),
                            "a beaten boss's arena ends at its level-exit door");
                        Assert.That(room.ExitRewards[0].IsLevelExit, Is.True,
                            "the one door out of a boss room is the level exit, every time");
                        continue;
                    }

                    if (r == lastStandard)
                        continue; // covered by TheDoorToTheBossIsAlwaysAloneAndMarked

                    Assert.That(room.ExitDoorCount, Is.InRange(1, LevelGenerator.MaxExitDoors), $"seed {seed} room {r}");

                    if (room.ExitRewards[0].Band == RewardBand.Boon)
                        Assert.That(room.ExitRewards.Select(c => c.PinnedGiver).Distinct().Count(),
                            Is.EqualTo(room.ExitDoorCount),
                            $"seed {seed} room {r}: boon doors must offer distinct givers");
                    else
                        Assert.That(room.ExitRewards.Select(c => c.Type).Distinct().Count(), Is.EqualTo(room.ExitDoorCount),
                            $"seed {seed} room {r}: reward types on one fork must be distinct");

                    Assert.That(room.ExitRewards.All(c => !c.IsBossDoor), Is.True,
                        $"seed {seed} room {r}: ordinary doors never carry the boss mark");
                }
            }
        }

        [Test]
        public void BandParityHoldsOnEveryFork()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount - 2; r++)
                {
                    RoomPlan room = plan.Rooms[r];
                    Assert.That(room.ExitRewards.Select(c => c.Band).Distinct().Count(), Is.EqualTo(1),
                        $"seed {seed} room {r}: every door on a fork shares one quality band");

                    if (room.ExitRewards[0].Band == RewardBand.Boon)
                    {
                        Assert.That(room.ExitDoorCount, Is.EqualTo(2),
                            $"seed {seed} room {r}: a boon fork is a choice of two givers");
                        Assert.That(room.ExitRewards.All(c => c.Type == RewardType.Transmission), Is.True,
                            $"seed {seed} room {r}");
                    }
                    else if (room.ExitRewards[0].Band == RewardBand.EliteBoon)
                    {
                        Assert.That(room.ExitDoorCount, Is.EqualTo(1),
                            $"seed {seed} room {r}: the elite fork is one door");
                    }
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
                            c => !c.IsBossDoor && !c.IsLevelExit &&
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
        public void EveryReachableDoorCountAppearsAcrossManySeeds()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount - 2; r++)
                    seen.Add(plan.Rooms[r].ExitDoorCount);
            }

            // The widest possible fork equals the largest band pool: Basic holds three
            // enabled types, so three doors is the ceiling until a band grows.
            Assert.That(seen, Is.EquivalentTo(new[] { 1, 2, 3 }),
                "across many seeds every reachable door count should occur");
        }

        [Test]
        public void EveryBandAppearsAcrossManySeeds()
        {
            var seen = new System.Collections.Generic.HashSet<RewardBand>();
            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed);
                for (int r = 0; r < plan.RoomCount - 2; r++)
                {
                    foreach (RewardChoice choice in plan.Rooms[r].ExitRewards)
                        seen.Add(choice.Band);
                }
            }

            Assert.That(seen.Count, Is.EqualTo(4),
                "across many seeds Basic, Valuable, Boon and EliteBoon forks should all occur");
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
                        Assert.That(b.Rooms[r].ExitRewards[d].Band, Is.EqualTo(a.Rooms[r].ExitRewards[d].Band), $"seed {seed} room {r} door {d}");
                        Assert.That(b.Rooms[r].ExitRewards[d].IsBossDoor, Is.EqualTo(a.Rooms[r].ExitRewards[d].IsBossDoor), $"seed {seed} room {r} door {d}");
                        Assert.That(b.Rooms[r].ExitRewards[d].IsLevelExit, Is.EqualTo(a.Rooms[r].ExitRewards[d].IsLevelExit), $"seed {seed} room {r} door {d}");
                        Assert.That(b.Rooms[r].ExitRewards[d].PinnedGiver, Is.EqualTo(a.Rooms[r].ExitRewards[d].PinnedGiver), $"seed {seed} room {r} door {d}");
                    }
                }
            }
        }
    }
}
