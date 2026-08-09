using System.Collections.Generic;
using System.Linq;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// The authored wave-composition path (ENEMIES_BIOME1.md §5): scripted rooms follow their
    /// script literally, swarm archetypes may share spawn points, everything else keeps the
    /// one-enemy-one-point guarantee, and uncovered rooms fall back to the budget generator.
    /// </summary>
    public class RoomScriptTests
    {
        static LevelPlan Generate(uint seed, ILevelGenerationSettings settings) =>
            new LevelGenerator(settings).Generate(new RunContext(seed));

        static FakeRoomScript Script(params IReadOnlyList<ScriptedSpawn>[][] variants)
        {
            var script = new FakeRoomScript();
            foreach (IReadOnlyList<ScriptedSpawn>[] waves in variants)
                script.Variants.Add(new FakeRoomScriptVariant { Waves = waves.ToList() });

            return script;
        }

        static IReadOnlyList<ScriptedSpawn> Wave(params (string id, int count)[] entries) =>
            entries.Select(e => new ScriptedSpawn(e.id, e.count)).ToList();

        static FakeGenerationSettings SettingsWithSwarm()
        {
            var settings = new FakeGenerationSettings();
            settings.Archetypes = new List<IEnemyArchetype>
            {
                new FakeArchetype { Id = "melee", Cost = 1f, SelectionWeight = 3f },
                new FakeArchetype { Id = "ranged", Cost = 2f, SelectionWeight = 1f },
                new FakeArchetype { Id = "bird", Cost = 0.3f, SelectionWeight = 0f, AllowsSharedSpawnPoints = true },
                new FakeArchetype { Id = "warden", Cost = 0f, SelectionWeight = 0f },
            };
            return settings;
        }

        [Test]
        public void AScriptedRoomFollowsItsScriptLiterally()
        {
            var settings = SettingsWithSwarm();
            settings.RoomScripts = new List<IRoomScript>
            {
                Script(new[] { Wave(("melee", 2)), Wave(("melee", 2), ("ranged", 1)) }),
            };

            for (uint seed = 1; seed <= 25; seed++)
            {
                RoomPlan room = Generate(seed, settings).Rooms[0];

                Assert.That(room.Waves.Count, Is.EqualTo(2), "wave count comes from the script");
                Assert.That(room.Waves[0].Spawns.Count(s => s.ArchetypeId == "melee"), Is.EqualTo(2));
                Assert.That(room.Waves[0].Spawns.Count, Is.EqualTo(2));
                Assert.That(room.Waves[1].Spawns.Count(s => s.ArchetypeId == "melee"), Is.EqualTo(2));
                Assert.That(room.Waves[1].Spawns.Count(s => s.ArchetypeId == "ranged"), Is.EqualTo(1));
            }
        }

        [Test]
        public void RoomsBeyondTheScriptListFallBackToTheBudgetPath()
        {
            var settings = SettingsWithSwarm();
            settings.RoomScripts = new List<IRoomScript>
            {
                Script(new[] { Wave(("melee", 1)) }),
            };

            LevelPlan plan = Generate(7u, settings);

            for (int r = 1; r < plan.RoomCount; r++)
            {
                Assert.That(plan.Rooms[r].Waves.Count, Is.GreaterThan(0), $"room {r} still generates waves");
                foreach (WavePlan wave in plan.Rooms[r].Waves)
                    Assert.That(wave.Count, Is.GreaterThan(0), $"room {r} has no empty waves");
            }
        }

        [Test]
        public void AnEmptyScriptSlotFallsBackToTheBudgetPath()
        {
            var settings = SettingsWithSwarm();
            settings.RoomScripts = new List<IRoomScript>
            {
                new FakeRoomScript(), // no variants — an authored hole, not an authored fight
            };

            RoomPlan room = Generate(11u, settings).Rooms[0];
            Assert.That(room.Waves.Count, Is.GreaterThan(0));
            Assert.That(room.Waves[0].Count, Is.GreaterThan(0));
        }

        [Test]
        public void TheVariantDrawIsSeededAndCoversEveryVariant()
        {
            var settings = SettingsWithSwarm();
            settings.RoomScripts = new List<IRoomScript>
            {
                Script(
                    new[] { Wave(("melee", 1)) },
                    new[] { Wave(("ranged", 1)) }),
            };

            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 60; seed++)
            {
                RoomPlan a = Generate(seed, settings).Rooms[0];
                RoomPlan b = Generate(seed, settings).Rooms[0];

                Assert.That(
                    b.Waves[0].Spawns[0].ArchetypeId, Is.EqualTo(a.Waves[0].Spawns[0].ArchetypeId),
                    "the same seed must pick the same variant");

                seen.Add(a.Waves[0].Spawns[0].ArchetypeId);
            }

            Assert.That(seen, Does.Contain("melee").And.Contain("ranged"),
                "across many seeds both variants should appear");
        }

        [Test]
        public void SwarmSpawnsMayShareSpawnPointsAndKeepTheirAuthoredSize()
        {
            var settings = SettingsWithSwarm();

            // 2 exclusive + 8 birds on templates with as few as 4 spawn points.
            settings.RoomScripts = new List<IRoomScript>
            {
                Script(new[] { Wave(("melee", 2), ("bird", 8)) }),
            };

            for (uint seed = 1; seed <= 25; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                WavePlan wave = plan.Rooms[0].Waves[0];

                Assert.That(wave.Spawns.Count(s => s.ArchetypeId == "bird"), Is.EqualTo(8),
                    "the swarm's size is authored, never capped by the room");

                var exclusivePoints = wave.Spawns
                    .Where(s => s.ArchetypeId == "melee")
                    .Select(s => s.SpawnPointIndex)
                    .ToList();
                Assert.That(exclusivePoints.Distinct().Count(), Is.EqualTo(exclusivePoints.Count),
                    "exclusive archetypes never share a point");

                Assert.That(LevelValidator.IsSolvable(plan, settings, out string reason), Is.True, reason);
            }
        }

        [Test]
        public void ExclusiveSpawnsBeyondTheRoomsPointsAreDroppedNotDoubled()
        {
            var settings = SettingsWithSwarm();
            settings.Templates = new List<IRoomTemplate>
            {
                new FakeRoomTemplate { Id = "tiny", SpawnPointCount = 3, SupportedRoles = RoomRole.Standard },
                new FakeRoomTemplate { Id = "vault", SpawnPointCount = 5, SupportedRoles = RoomRole.Boss },
            };
            settings.RoomScripts = new List<IRoomScript>
            {
                Script(new[] { Wave(("melee", 6)) }),
            };

            WavePlan wave = Generate(3u, settings).Rooms[0].Waves[0];

            Assert.That(wave.Spawns.Count, Is.EqualTo(3), "a 3-point room seats 3 exclusive enemies");
            Assert.That(wave.Spawns.Select(s => s.SpawnPointIndex).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TheValidatorRejectsASharedPointForAnExclusiveArchetype()
        {
            var settings = SettingsWithSwarm();
            settings.MinStandardRooms = 1;
            settings.MaxStandardRooms = 1;
            var rooms = new List<RoomPlan>
            {
                new RoomPlan("arena", 0, new List<WavePlan>
                {
                    new WavePlan(new List<SpawnAssignment>
                    {
                        new SpawnAssignment("melee", 2),
                        new SpawnAssignment("melee", 2),
                    }),
                }),
                new RoomPlan("vault", 1, new List<WavePlan>
                {
                    new WavePlan(new List<SpawnAssignment> { new SpawnAssignment("warden", 0) }),
                }, RoomRole.Boss),
            };

            Assert.That(LevelValidator.IsSolvable(new LevelPlan(1u, rooms), settings, out string reason), Is.False);
            Assert.That(reason, Does.Contain("spawn point"));
        }

        [Test]
        public void TheValidatorAcceptsASharedPointForASwarmArchetype()
        {
            var settings = SettingsWithSwarm();
            settings.MinStandardRooms = 1;
            settings.MaxStandardRooms = 1;
            var rooms = new List<RoomPlan>
            {
                new RoomPlan("arena", 0, new List<WavePlan>
                {
                    new WavePlan(new List<SpawnAssignment>
                    {
                        new SpawnAssignment("melee", 2),
                        new SpawnAssignment("bird", 2),
                        new SpawnAssignment("bird", 2),
                    }),
                }),
                new RoomPlan("vault", 1, new List<WavePlan>
                {
                    new WavePlan(new List<SpawnAssignment> { new SpawnAssignment("warden", 0) }),
                }, RoomRole.Boss),
            };

            Assert.That(LevelValidator.IsSolvable(new LevelPlan(1u, rooms), settings, out string reason), Is.True, reason);
        }

        [Test]
        public void ScriptedLevelsReplayIdenticallyFromTheSameSeed()
        {
            var settings = SettingsWithSwarm();
            settings.RoomScripts = new List<IRoomScript>
            {
                Script(new[] { Wave(("melee", 2)) }, new[] { Wave(("ranged", 2)) }),
                Script(new[] { Wave(("melee", 1), ("bird", 6)) }),
            };

            for (uint seed = 1; seed <= 20; seed++)
            {
                string a = Describe(Generate(seed, settings));
                string b = Describe(Generate(seed, settings));
                Assert.That(b, Is.EqualTo(a));
            }
        }

        static string Describe(LevelPlan plan)
        {
            var parts = new List<string>();
            foreach (RoomPlan room in plan.Rooms)
            {
                foreach (WavePlan wave in room.Waves)
                    parts.Add(room.TemplateId + ":" + string.Join(",", wave.Spawns));
            }

            return string.Join(" | ", parts);
        }
    }
}
