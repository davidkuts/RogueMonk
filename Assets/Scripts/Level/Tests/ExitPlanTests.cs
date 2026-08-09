using System.Linq;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// The door plan: a cleared room offers a seeded 1–4 doors with distinct placeholder
    /// reward numbers (1–8), the room before the boss offers exactly one door marked 9,
    /// and the whole arrangement replays from the seed like everything else.
    /// </summary>
    public class ExitPlanTests
    {
        static LevelPlan Generate(uint seed) =>
            new LevelGenerator(new FakeGenerationSettings()).Generate(new RunContext(seed));

        [Test]
        public void DoorCountsStayBetweenOneAndFourAndLabelsAreDistinct()
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
                        continue; // covered by TheDoorToTheBossIsAlwaysAloneAndMarkedNine

                    Assert.That(room.ExitDoorCount, Is.InRange(1, LevelGenerator.MaxExitDoors), $"seed {seed} room {r}");
                    Assert.That(room.ExitLabels.Distinct().Count(), Is.EqualTo(room.ExitDoorCount),
                        $"seed {seed} room {r}: labels must be distinct");
                    Assert.That(room.ExitLabels.All(l => l >= 1 && l < LevelGenerator.BossDoorLabel), Is.True,
                        $"seed {seed} room {r}: ordinary labels stay off the boss's number");
                }
            }
        }

        [Test]
        public void TheDoorToTheBossIsAlwaysAloneAndMarkedNine()
        {
            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed);
                RoomPlan beforeBoss = plan.Rooms[plan.RoomCount - 2];

                Assert.That(beforeBoss.ExitLabels, Is.EqualTo(new[] { LevelGenerator.BossDoorLabel }),
                    $"seed {seed}: the boss is never optional and never a surprise");
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
        public void TheDoorPlanReplaysFromTheSeed()
        {
            for (uint seed = 1; seed <= 30; seed++)
            {
                LevelPlan a = Generate(seed);
                LevelPlan b = Generate(seed);

                for (int r = 0; r < a.RoomCount; r++)
                    Assert.That(b.Rooms[r].ExitLabels, Is.EqualTo(a.Rooms[r].ExitLabels), $"seed {seed} room {r}");
            }
        }
    }
}
