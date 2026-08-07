using System.Collections.Generic;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Level.Tests
{
    public class LevelGeneratorTests
    {
        static LevelPlan Generate(uint seed, ILevelGenerationSettings settings) =>
            new LevelGenerator(settings).Generate(new RunContext(seed));

        [Test]
        public void SameSeedProducesAnIdenticalLevel()
        {
            // The whole point of the seeded RNG: quoting a seed reproduces a run exactly.
            var settings = new FakeGenerationSettings();
            LevelPlan a = Generate(12345u, settings);
            LevelPlan b = Generate(12345u, settings);

            Assert.That(b.RoomCount, Is.EqualTo(a.RoomCount));
            for (int r = 0; r < a.RoomCount; r++)
            {
                Assert.That(b.Rooms[r].TemplateId, Is.EqualTo(a.Rooms[r].TemplateId), $"room {r} template");
                Assert.That(b.Rooms[r].Waves.Count, Is.EqualTo(a.Rooms[r].Waves.Count), $"room {r} wave count");

                for (int w = 0; w < a.Rooms[r].Waves.Count; w++)
                {
                    IReadOnlyList<SpawnAssignment> expected = a.Rooms[r].Waves[w].Spawns;
                    IReadOnlyList<SpawnAssignment> actual = b.Rooms[r].Waves[w].Spawns;
                    Assert.That(actual.Count, Is.EqualTo(expected.Count));

                    for (int s = 0; s < expected.Count; s++)
                    {
                        Assert.That(actual[s].ArchetypeId, Is.EqualTo(expected[s].ArchetypeId));
                        Assert.That(actual[s].SpawnPointIndex, Is.EqualTo(expected[s].SpawnPointIndex));
                    }
                }
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentLevels()
        {
            var settings = new FakeGenerationSettings();
            int identical = 0;

            for (uint seed = 1; seed <= 40; seed++)
            {
                LevelPlan a = Generate(seed, settings);
                LevelPlan b = Generate(seed + 5000u, settings);
                if (Describe(a) == Describe(b))
                    identical++;
            }

            Assert.That(identical, Is.LessThan(3), "seeds should rarely collide into the same level");
        }

        [Test]
        public void RoomCountStaysInsideTheConfiguredRange()
        {
            var settings = new FakeGenerationSettings { MinStandardRooms = 5, MaxStandardRooms = 6 };

            for (uint seed = 1; seed <= 500; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                Assert.That(plan.RoomCount, Is.InRange(6, 7), $"seed {seed}");
            }
        }

        [Test]
        public void TheBossRoomIsAlwaysLastAndAlwaysExactlyOne()
        {
            var settings = new FakeGenerationSettings();

            for (uint seed = 1; seed <= 500; seed++)
            {
                LevelPlan plan = Generate(seed, settings);

                int bossCount = 0;
                foreach (RoomPlan room in plan.Rooms)
                {
                    if (room.IsBossRoom)
                        bossCount++;
                }

                Assert.That(bossCount, Is.EqualTo(1), $"seed {seed}");
                Assert.That(plan.FinalRoom.IsBossRoom, Is.True, $"seed {seed}");
                Assert.That(plan.BossRoomIndex, Is.EqualTo(plan.RoomCount - 1), $"seed {seed}");
            }
        }

        [Test]
        public void TheBossArrivesAfterTheConfiguredNumberOfOrdinaryRooms()
        {
            // The human asked for the boss after 5 rooms; that is what this pins.
            var settings = new FakeGenerationSettings { MinStandardRooms = 5, MaxStandardRooms = 5 };

            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                Assert.That(plan.RoomCount, Is.EqualTo(6), $"seed {seed}");
                Assert.That(plan.BossRoomIndex, Is.EqualTo(5), $"seed {seed}");

                for (int r = 0; r < 5; r++)
                    Assert.That(plan.Rooms[r].Role, Is.EqualTo(RoomRole.Standard), $"seed {seed} room {r}");
            }
        }

        [Test]
        public void OnlyTemplatesThatSupportBossCanHostIt()
        {
            var settings = new FakeGenerationSettings
            {
                Templates = new List<IRoomTemplate>
                {
                    new FakeRoomTemplate { Id = "arena", SpawnPointCount = 6, SupportedRoles = RoomRole.Standard },
                    new FakeRoomTemplate { Id = "throne", SpawnPointCount = 8, SupportedRoles = RoomRole.Boss },
                },
            };

            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                Assert.That(plan.FinalRoom.TemplateId, Is.EqualTo("throne"), $"seed {seed}");

                for (int r = 0; r < plan.RoomCount - 1; r++)
                    Assert.That(plan.Rooms[r].TemplateId, Is.EqualTo("arena"), $"seed {seed} room {r}");
            }
        }

        [Test]
        public void TheBossRoomFightIsDenserThanAnOrdinaryOne()
        {
            var settings = new FakeGenerationSettings { BossBudgetBonus = 6f, BossRoomWaves = 1 };
            float ordinaryPerWave = 0f, bossPerWave = 0f;

            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                RoomPlan first = plan.Rooms[0];
                RoomPlan boss = plan.FinalRoom;

                ordinaryPerWave += first.TotalEnemies / (float)first.Waves.Count;
                bossPerWave += boss.TotalEnemies / (float)boss.Waves.Count;
            }

            Assert.That(bossPerWave, Is.GreaterThan(ordinaryPerWave));
        }

        [Test]
        public void TheBossRoomUsesItsOwnWaveCount()
        {
            var settings = new FakeGenerationSettings { BossRoomWaves = 1, MinWavesPerRoom = 3, MaxWavesPerRoom = 3 };

            for (uint seed = 1; seed <= 100; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                Assert.That(plan.FinalRoom.Waves.Count, Is.EqualTo(1), $"seed {seed}");
                Assert.That(plan.Rooms[0].Waves.Count, Is.EqualTo(3), $"seed {seed}");
            }
        }

        [Test]
        public void NoRoomEverHasAnEmptyWave()
        {
            // An empty wave is a room that clears itself, which silently skips a fight.
            var settings = new FakeGenerationSettings();

            for (uint seed = 1; seed <= 500; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                foreach (RoomPlan room in plan.Rooms)
                {
                    foreach (WavePlan wave in room.Waves)
                        Assert.That(wave.Count, Is.GreaterThan(0), $"seed {seed}, room {room.Index}");
                }
            }
        }

        [Test]
        public void AWaveNeverExceedsTheRoomsSpawnPoints()
        {
            var settings = new FakeGenerationSettings();
            var byId = new Dictionary<string, IRoomTemplate>();
            foreach (IRoomTemplate template in settings.Templates)
                byId[template.Id] = template;

            for (uint seed = 1; seed <= 300; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                foreach (RoomPlan room in plan.Rooms)
                {
                    IRoomTemplate template = byId[room.TemplateId];
                    foreach (WavePlan wave in room.Waves)
                        Assert.That(wave.Count, Is.LessThanOrEqualTo(template.SpawnPointCount), $"seed {seed}");
                }
            }
        }

        [Test]
        public void TwoEnemiesNeverShareASpawnPointInOneWave()
        {
            var settings = new FakeGenerationSettings();

            for (uint seed = 1; seed <= 300; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                foreach (RoomPlan room in plan.Rooms)
                {
                    foreach (WavePlan wave in room.Waves)
                    {
                        var used = new HashSet<int>();
                        foreach (SpawnAssignment spawn in wave.Spawns)
                            Assert.That(used.Add(spawn.SpawnPointIndex), Is.True, $"seed {seed} double-booked a spawn point");
                    }
                }
            }
        }

        [Test]
        public void WavesEscalateAcrossTheLevel()
        {
            // Later rooms get a bigger budget, so on average they hold more enemies. Compared
            // between ordinary rooms only, so the boss bonus cannot mask a broken curve.
            var settings = new FakeGenerationSettings
            {
                MinStandardRooms = 6, MaxStandardRooms = 6, BudgetGrowthPerRoom = 2f,
            };
            float firstRoomTotal = 0f, lastStandardTotal = 0f;

            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                RoomPlan first = plan.Rooms[0];
                RoomPlan lastStandard = plan.Rooms[plan.RoomCount - 2];

                firstRoomTotal += first.TotalEnemies / (float)first.Waves.Count;
                lastStandardTotal += lastStandard.TotalEnemies / (float)lastStandard.Waves.Count;
            }

            Assert.That(lastStandardTotal, Is.GreaterThan(firstRoomTotal));
        }

        [Test]
        public void ConsecutiveRepeatsAreAvoidedWhenDisallowed()
        {
            var settings = new FakeGenerationSettings { AllowConsecutiveRepeats = false };
            int repeats = 0;

            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                for (int r = 1; r < plan.RoomCount; r++)
                {
                    if (plan.Rooms[r].TemplateId == plan.Rooms[r - 1].TemplateId)
                        repeats++;
                }
            }

            Assert.That(repeats, Is.EqualTo(0));
        }

        [Test]
        public void ASingleTemplateStillGeneratesRatherThanFailing()
        {
            // With one template the no-repeats rule is impossible to satisfy; relaxing it
            // beats refusing to build a level.
            var settings = new FakeGenerationSettings
            {
                AllowConsecutiveRepeats = false,
                Templates = new List<IRoomTemplate> { new FakeRoomTemplate { Id = "only", SpawnPointCount = 4 } },
            };

            LevelPlan plan = Generate(7u, settings);
            Assert.That(plan.RoomCount, Is.InRange(settings.MinRooms, settings.MaxRooms));
            foreach (RoomPlan room in plan.Rooms)
                Assert.That(room.TemplateId, Is.EqualTo("only"));
        }

        [Test]
        public void TheFinalRoomAlwaysUsesABossCapableTemplate()
        {
            var settings = new FakeGenerationSettings
            {
                Templates = new List<IRoomTemplate>
                {
                    new FakeRoomTemplate { Id = "arena", SpawnPointCount = 6, SupportedRoles = RoomRole.Standard },
                    new FakeRoomTemplate { Id = "corridor", SpawnPointCount = 4, SupportedRoles = RoomRole.Standard },
                    new FakeRoomTemplate { Id = "vault", SpawnPointCount = 5, SupportedRoles = RoomRole.Boss },
                },
            };

            for (uint seed = 1; seed <= 200; seed++)
            {
                LevelPlan plan = Generate(seed, settings);
                Assert.That(plan.FinalRoom.TemplateId, Is.EqualTo("vault"), $"seed {seed}");
            }
        }

        [Test]
        public void ExpensiveArchetypesProduceSmallerWaves()
        {
            var cheap = new FakeGenerationSettings
            {
                Archetypes = new List<IEnemyArchetype> { new FakeArchetype { Id = "cheap", Cost = 1f } },
            };
            var pricey = new FakeGenerationSettings
            {
                Archetypes = new List<IEnemyArchetype> { new FakeArchetype { Id = "pricey", Cost = 4f } },
            };

            int cheapTotal = 0, priceyTotal = 0;
            for (uint seed = 1; seed <= 100; seed++)
            {
                cheapTotal += Generate(seed, cheap).TotalEnemies;
                priceyTotal += Generate(seed, pricey).TotalEnemies;
            }

            Assert.That(priceyTotal, Is.LessThan(cheapTotal));
        }

        [Test]
        public void CanGenerateRejectsUnusableContentSets()
        {
            string reason;

            Assert.That(new LevelGenerator(null).CanGenerate(out reason), Is.False);

            var noTemplates = new FakeGenerationSettings { Templates = new List<IRoomTemplate>() };
            Assert.That(new LevelGenerator(noTemplates).CanGenerate(out reason), Is.False);
            Assert.That(reason, Does.Contain("template"));

            var noArchetypes = new FakeGenerationSettings { Archetypes = new List<IEnemyArchetype>() };
            Assert.That(new LevelGenerator(noArchetypes).CanGenerate(out reason), Is.False);
            Assert.That(reason, Does.Contain("archetype"));

            var noBossRoom = new FakeGenerationSettings
            {
                Templates = new List<IRoomTemplate> { new FakeRoomTemplate { Id = "a", SupportedRoles = RoomRole.Standard } },
            };
            Assert.That(new LevelGenerator(noBossRoom).CanGenerate(out reason), Is.False);
            Assert.That(reason, Does.Contain("boss"));

            Assert.That(new LevelGenerator(new FakeGenerationSettings()).CanGenerate(out reason), Is.True);
            Assert.That(reason, Is.Null);
        }

        static string Describe(LevelPlan plan)
        {
            var text = new System.Text.StringBuilder();
            foreach (RoomPlan room in plan.Rooms)
            {
                text.Append(room.TemplateId).Append(':');
                foreach (WavePlan wave in room.Waves)
                {
                    foreach (SpawnAssignment spawn in wave.Spawns)
                        text.Append(spawn).Append(',');

                    text.Append('|');
                }
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// The soak test DESIGN.md asks for: many seeds, every one playable. Any future change to
    /// room generation has to keep this passing.
    /// </summary>
    public class LevelSoakTests
    {
        [Test]
        public void TwoThousandSeedsAllProduceSolvableLevels()
        {
            var settings = new FakeGenerationSettings();
            var generator = new LevelGenerator(settings);

            for (uint seed = 1; seed <= 2000; seed++)
            {
                LevelPlan plan = generator.Generate(new RunContext(seed));
                bool solvable = LevelValidator.IsSolvable(plan, settings, out string reason);
                Assert.That(solvable, Is.True, $"seed {seed} produced an unplayable level: {reason}");
            }
        }

        [Test]
        public void SoakHoldsWithATightContentSet()
        {
            // The narrow case is the one that breaks: few templates, few spawn points,
            // expensive enemies.
            var settings = new FakeGenerationSettings
            {
                MinStandardRooms = 5,
                MaxStandardRooms = 6,
                MaxEnemiesPerWave = 2,
                BaseWaveBudget = 1f,
                BudgetGrowthPerRoom = 0f,
                Templates = new List<IRoomTemplate>
                {
                    new FakeRoomTemplate { Id = "tiny", SpawnPointCount = 1 },
                },
                Archetypes = new List<IEnemyArchetype>
                {
                    new FakeArchetype { Id = "expensive", Cost = 99f },
                },
            };
            var generator = new LevelGenerator(settings);

            for (uint seed = 1; seed <= 500; seed++)
            {
                LevelPlan plan = generator.Generate(new RunContext(seed));
                bool solvable = LevelValidator.IsSolvable(plan, settings, out string reason);
                Assert.That(solvable, Is.True, $"seed {seed}: {reason}");
            }
        }
    }

    public class LevelValidatorTests
    {
        [Test]
        public void RejectsALevelWithNoRooms()
        {
            var plan = new LevelPlan(1u, new List<RoomPlan>());
            Assert.That(LevelValidator.IsSolvable(plan, null, out string reason), Is.False);
            Assert.That(reason, Does.Contain("no rooms"));
        }

        [Test]
        public void RejectsARoomWithNoWaves()
        {
            var plan = new LevelPlan(1u, new List<RoomPlan> { new RoomPlan("vault", 0, new List<WavePlan>(), RoomRole.Boss) });
            Assert.That(LevelValidator.IsSolvable(plan, null, out string reason), Is.False);
            Assert.That(reason, Does.Contain("no waves"));
        }

        [Test]
        public void RejectsAnEmptyWave()
        {
            var waves = new List<WavePlan> { new WavePlan(new List<SpawnAssignment>()) };
            var plan = new LevelPlan(1u, new List<RoomPlan> { new RoomPlan("vault", 0, waves, RoomRole.Boss) });

            Assert.That(LevelValidator.IsSolvable(plan, null, out string reason), Is.False);
            Assert.That(reason, Does.Contain("clear itself"));
        }

        [Test]
        public void RejectsDoubleBookedSpawnPoints()
        {
            var spawns = new List<SpawnAssignment>
            {
                new SpawnAssignment("melee", 2),
                new SpawnAssignment("ranged", 2),
            };
            var plan = new LevelPlan(1u, new List<RoomPlan>
            {
                new RoomPlan("vault", 0, new List<WavePlan> { new WavePlan(spawns) }, RoomRole.Boss),
            });

            Assert.That(LevelValidator.IsSolvable(plan, null, out string reason), Is.False);
            Assert.That(reason, Does.Contain("two enemies"));
        }

        [Test]
        public void RejectsAnUnknownArchetype()
        {
            var settings = new FakeGenerationSettings { MinStandardRooms = 0, MaxStandardRooms = 0 };
            var spawns = new List<SpawnAssignment> { new SpawnAssignment("dragon", 0) };
            var plan = new LevelPlan(1u, new List<RoomPlan>
            {
                new RoomPlan("vault", 0, new List<WavePlan> { new WavePlan(spawns) }, RoomRole.Boss),
            });

            Assert.That(LevelValidator.IsSolvable(plan, settings, out string reason), Is.False);
            Assert.That(reason, Does.Contain("dragon"));
        }

        [Test]
        public void RejectsASpawnPointTheTemplateDoesNotHave()
        {
            var settings = new FakeGenerationSettings { MinStandardRooms = 0, MaxStandardRooms = 0 };
            var spawns = new List<SpawnAssignment> { new SpawnAssignment("melee", 99) };
            var plan = new LevelPlan(1u, new List<RoomPlan>
            {
                new RoomPlan("vault", 0, new List<WavePlan> { new WavePlan(spawns) }, RoomRole.Boss),
            });

            Assert.That(LevelValidator.IsSolvable(plan, settings, out string reason), Is.False);
            Assert.That(reason, Does.Contain("does not have"));
        }

        [Test]
        public void AcceptsAWellFormedLevel()
        {
            var settings = new FakeGenerationSettings();
            LevelPlan plan = new LevelGenerator(settings).Generate(new RunContext(99u));

            Assert.That(LevelValidator.IsSolvable(plan, settings, out string reason), Is.True, reason);
        }
    }
}
