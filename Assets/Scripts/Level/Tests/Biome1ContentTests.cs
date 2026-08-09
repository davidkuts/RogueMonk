using System.Collections.Generic;
using System.Linq;
using Game.Core.Rng;
using NUnit.Framework;
using UnityEditor;

namespace Game.Level.Tests
{
    /// <summary>
    /// Runs the generator against the REAL shipped content set — the asset the game loads,
    /// not a fake. M14.2's lesson: a test that constructs its own inputs can pass while the
    /// real path is broken. These pin the Biome 1 §5 composition rules to the actual data:
    /// the Tyrant alone in the boss room, Scrapfeathers never a wave's only type, and never
    /// more than one elite in a room.
    /// </summary>
    public class Biome1ContentTests
    {
        const string SettingsPath = "Assets/Settings/Data/Level/LevelGenerationSettings.asset";

        static readonly string[] EliteIds = { "AmbershellArchetype", "TwiceStruckArchetype" };

        static LevelGenerationSettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LevelGenerationSettings>(SettingsPath);
            Assert.That(settings, Is.Not.Null, $"missing settings asset at {SettingsPath}");
            return settings;
        }

        [Test]
        public void TheShippedContentSetCanGenerate()
        {
            var generator = new LevelGenerator(LoadSettings());
            Assert.That(generator.CanGenerate(out string reason), Is.True, reason);
        }

        [Test]
        public void TheBossIsTheTyrant()
        {
            Assert.That(LoadSettings().BossArchetypeId, Is.EqualTo("TyrantArchetype"),
                "the Stone Warden was replaced by the Tyrant for Biome 1");
        }

        [Test]
        public void TwoHundredSeedsOfTheRealContentAreSolvableAndFollowTheCompositionRules()
        {
            LevelGenerationSettings settings = LoadSettings();
            var generator = new LevelGenerator(settings);

            for (uint seed = 1; seed <= 200; seed++)
            {
                var run = new RunContext(seed);

                for (int level = 0; level < settings.LevelsPerRun; level++)
                {
                    LevelPlan plan = generator.Generate(run, level);

                    Assert.That(LevelValidator.IsSolvable(plan, settings, out string reason), Is.True,
                        $"seed {seed} level {level}: {reason}");

                    foreach (RoomPlan room in plan.Rooms)
                    {
                        int elitesInRoom = 0;

                        foreach (WavePlan wave in room.Waves)
                        {
                            Assert.That(
                                wave.Spawns.All(s => s.ArchetypeId == "ScrapfeatherArchetype"), Is.False,
                                $"seed {seed} level {level} room {room.Index}: Scrapfeathers must never be a wave's only type");

                            elitesInRoom += wave.Spawns.Count(s => EliteIds.Contains(s.ArchetypeId));
                        }

                        Assert.That(elitesInRoom, Is.LessThanOrEqualTo(1),
                            $"seed {seed} level {level} room {room.Index}: never two elites in a Biome 1 room");
                    }

                    // The boss room is the Tyrant, solo, on the designated point.
                    RoomPlan boss = plan.FinalRoom;
                    Assert.That(boss.Waves.Count, Is.EqualTo(1), $"seed {seed} level {level}: boss room is one wave");
                    Assert.That(boss.Waves[0].Spawns.Count, Is.EqualTo(1), $"seed {seed} level {level}: the boss fights alone");
                    Assert.That(boss.Waves[0].Spawns[0].ArchetypeId, Is.EqualTo("TyrantArchetype"));
                    Assert.That(boss.Waves[0].Spawns[0].SpawnPointIndex, Is.EqualTo(LevelGenerator.BossSpawnPointIndex));
                }
            }
        }

        [Test]
        public void EveryScriptedRoomOfTheFirstLevelIsScriptedNotBudgetRolled()
        {
            // The first five standard rooms of a level must come from the §5 scripts. The cheap
            // proxy pinning it: a budget wave can never exceed maxEnemiesPerWave (3), while the
            // scripted swarm rooms routinely do. Assert directly that every standard room's
            // composition matches one of its slot's authored variants.
            LevelGenerationSettings settings = LoadSettings();
            var generator = new LevelGenerator(settings);
            IReadOnlyList<IRoomScript> scripts = settings.RoomScripts;

            for (uint seed = 1; seed <= 50; seed++)
            {
                LevelPlan plan = generator.Generate(new RunContext(seed), 0);

                for (int r = 0; r < plan.RoomCount; r++)
                {
                    RoomPlan room = plan.Rooms[r];
                    if (room.IsBossRoom || r >= scripts.Count)
                        continue;

                    bool matchesAnyVariant = scripts[r].Variants.Any(variant => VariantMatches(variant, room));
                    Assert.That(matchesAnyVariant, Is.True,
                        $"seed {seed} room {r} does not match any authored variant for its slot");
                }
            }
        }

        static bool VariantMatches(IRoomScriptVariant variant, RoomPlan room)
        {
            if (variant.Waves.Count != room.Waves.Count)
                return false;

            for (int w = 0; w < variant.Waves.Count; w++)
            {
                Dictionary<string, int> authored = variant.Waves[w]
                    .GroupBy(e => e.ArchetypeId)
                    .ToDictionary(g => g.Key, g => g.Sum(e => e.Count));

                Dictionary<string, int> planned = room.Waves[w].Spawns
                    .GroupBy(s => s.ArchetypeId)
                    .ToDictionary(g => g.Key, g => g.Count());

                if (authored.Count != planned.Count)
                    return false;

                foreach (KeyValuePair<string, int> pair in authored)
                {
                    if (!planned.TryGetValue(pair.Key, out int count) || count != pair.Value)
                        return false;
                }
            }

            return true;
        }
    }
}
