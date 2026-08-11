using System.Collections.Generic;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Builds a whole level from the run seed: room count, template order, wave count and
    /// spawn population. Geometry is never generated — DESIGN.md locks hand-authored room
    /// prefabs, and this only chooses which ones to use and what fights they hold.
    ///
    /// Every draw comes from the supplied <see cref="IRandomSource"/> in a fixed order, so the
    /// same seed always produces the same level.
    /// </summary>
    public sealed class LevelGenerator
    {
        /// <summary>
        /// Where the boss stands. Fixed rather than drawn: a boss rolled into a corner would open
        /// the fight already halfway across the arena. Boss-capable templates author point 0 at
        /// the far centre of the room for exactly this.
        /// </summary>
        public const int BossSpawnPointIndex = 0;

        readonly ILevelGenerationSettings settings;
        readonly RewardRoller rewardRoller;

        readonly List<float> weightScratch = new List<float>();
        readonly List<int> spawnPointScratch = new List<int>();
        readonly List<IRoomScriptVariant> variantScratch = new List<IRoomScriptVariant>();

        public LevelGenerator(ILevelGenerationSettings settings)
        {
            this.settings = settings;
            if (settings != null && settings.Rewards != null)
                rewardRoller = new RewardRoller(settings.Rewards);
        }

        /// <summary>Returns false with a reason when the content set cannot produce a level at all.</summary>
        public bool CanGenerate(out string reason)
        {
            if (settings == null)
            {
                reason = "no generation settings";
                return false;
            }

            if (settings.Templates == null || settings.Templates.Count == 0)
            {
                reason = "no room templates";
                return false;
            }

            if (settings.Archetypes == null || settings.Archetypes.Count == 0)
            {
                reason = "no enemy archetypes";
                return false;
            }

            bool anyStandardRoom = false;
            bool anyBossRoom = false;
            for (int i = 0; i < settings.Templates.Count; i++)
            {
                IRoomTemplate template = settings.Templates[i];
                if (template == null || template.SelectionWeight <= 0f || template.SpawnPointCount <= 0)
                    continue;

                if ((template.SupportedRoles & RoomRole.Standard) != 0)
                    anyStandardRoom = true;
                if ((template.SupportedRoles & RoomRole.Boss) != 0)
                    anyBossRoom = true;
            }

            if (!anyStandardRoom)
            {
                reason = "no template can host a standard room with positive weight and at least one spawn point";
                return false;
            }

            if (!anyBossRoom)
            {
                reason = "no template can host the boss room";
                return false;
            }

            bool anyArchetype = false;
            for (int i = 0; i < settings.Archetypes.Count; i++)
            {
                if (settings.Archetypes[i] != null && settings.Archetypes[i].SelectionWeight > 0f)
                {
                    anyArchetype = true;
                    break;
                }
            }

            if (!anyArchetype)
            {
                reason = "no enemy archetype has positive weight";
                return false;
            }

            // A boss id that names nothing would generate a boss room whose only spawn silently
            // fails, leaving a room that can never be cleared.
            string bossId = settings.BossArchetypeId;
            if (!string.IsNullOrEmpty(bossId))
            {
                bool bossPresent = false;
                for (int i = 0; i < settings.Archetypes.Count; i++)
                {
                    if (settings.Archetypes[i] != null && settings.Archetypes[i].Id == bossId)
                    {
                        bossPresent = true;
                        break;
                    }
                }

                if (!bossPresent)
                {
                    reason = $"boss archetype '{bossId}' is not in the archetype list";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public LevelPlan Generate(RunContext run) => Generate(run, 0);

        /// <summary>
        /// Builds one level of a run. <paramref name="levelIndex"/> drives escalation: later levels
        /// get more rooms and a bigger spend budget, which is what stops level three being easier
        /// than level one now that the player arrives carrying boons.
        /// </summary>
        public LevelPlan Generate(RunContext run, int levelIndex)
        {
            IRandomSource random = run.Random;
            levelIndex = Mathf.Max(0, levelIndex);

            int extraRooms = Mathf.FloorToInt(Mathf.Max(0f, settings.RoomGrowthPerLevel) * levelIndex);
            float levelBudgetBonus = Mathf.Max(0f, settings.BudgetGrowthPerLevel) * levelIndex;

            // The boss room is appended, not drawn: the player must always face it last, and
            // always after the same number of ordinary rooms.
            int standardRooms = random.NextInt(
                Mathf.Max(1, settings.MinStandardRooms),
                Mathf.Max(1, settings.MaxStandardRooms) + 1) + extraRooms;

            var rooms = new List<RoomPlan>(standardRooms + 1);
            IRoomTemplate previous = null;

            // Exactly one elite duel per level. Two independent coin-flip slots produced a
            // distribution nobody chose — a quarter of runs met no elite and a quarter met two —
            // so the quota is spent here instead: the seed decides WHICH elite the player fights,
            // never whether they fight one.
            int eliteSlotsRemaining = CountEliteSlots(standardRooms);
            bool elitePlaced = false;

            for (int index = 0; index < standardRooms; index++)
            {
                IRoomTemplate template = PickTemplate(random, previous, RoomRole.Standard);
                previous = template;

                IRoomScript script = FindRoomScript(index);

                EliteRequirement requirement = EliteRequirement.Free;
                if (script != null && HasEliteVariant(script))
                {
                    eliteSlotsRemaining--;
                    requirement =
                        elitePlaced ? EliteRequirement.Forbidden
                        : eliteSlotsRemaining == 0 ? EliteRequirement.Required
                        : EliteRequirement.Free;
                }

                List<WavePlan> waves;
                if (script != null)
                {
                    waves = BuildScriptedWaves(random, template, script, requirement, out bool tookElite);
                    elitePlaced |= tookElite;
                }
                else
                {
                    waves = BuildWaves(random, template, index, levelBudgetBonus);
                }

                bool bossIsNext = index == standardRooms - 1;
                rooms.Add(new RoomPlan(
                    template.Id, index, waves, RoomRole.Standard, DrawExitRewards(random, bossIsNext)));
            }

            // The boss room ends at a door of its own: beating the boss reveals the level
            // exit, and leaving is a deliberate Interact like every other door — never an
            // automatic cut the moment the health bar empties.
            IRoomTemplate bossTemplate = PickTemplate(random, previous, RoomRole.Boss);
            rooms.Add(new RoomPlan(
                bossTemplate.Id,
                standardRooms,
                BuildBossWaves(random, bossTemplate, standardRooms),
                RoomRole.Boss,
                new List<RewardChoice>(1) { RewardChoice.LevelExit }));

            return new LevelPlan(run.Seed, rooms);
        }

        /// <summary>Most doors a cleared room may offer.</summary>
        public const int MaxExitDoors = 4;

        /// <summary>
        /// Decides a cleared room's doors: when the boss is next there is exactly one, carrying
        /// the boss mark — the boss is never optional and never a surprise. Otherwise a seeded
        /// 1–4 doors rolled band-first (the band decides what kinds appear; boon bands are a
        /// single door). With no reward config the fork degrades to one Basic Minutes cache
        /// rather than failing generation.
        /// </summary>
        List<RewardChoice> DrawExitRewards(IRandomSource random, bool bossIsNext)
        {
            if (bossIsNext)
                return new List<RewardChoice>(1) { RewardChoice.BossDoor };

            int doors = random.NextInt(1, MaxExitDoors + 1);

            if (rewardRoller == null)
                return new List<RewardChoice>(1)
                {
                    new RewardChoice(RewardType.MinutesCache, RewardBand.Basic),
                };

            List<RewardChoice> fork = rewardRoller.RollFork(random, doors);
            if (fork.Count == 0)
                fork.Add(new RewardChoice(RewardType.MinutesCache, RewardBand.Basic));

            return fork;
        }

        IRoomTemplate PickTemplate(IRandomSource random, IRoomTemplate previous, RoomRole role)
        {
            IRoomTemplate picked = TryPickTemplate(random, previous, role);

            // Relax the no-repeats rule rather than fail: with a small template set it can be
            // impossible to satisfy, and a repeated room beats no level at all.
            if (picked == null)
                picked = TryPickTemplate(random, null, role);

            return picked;
        }

        IRoomTemplate TryPickTemplate(IRandomSource random, IRoomTemplate exclude, RoomRole role)
        {
            weightScratch.Clear();
            IReadOnlyList<IRoomTemplate> templates = settings.Templates;

            for (int i = 0; i < templates.Count; i++)
            {
                IRoomTemplate template = templates[i];
                bool usable = template != null
                              && template.SelectionWeight > 0f
                              && template.SpawnPointCount > 0
                              && (template.SupportedRoles & role) != 0
                              && (settings.AllowConsecutiveRepeats || exclude == null || template.Id != exclude.Id);

                weightScratch.Add(usable ? template.SelectionWeight : 0f);
            }

            int index = random.PickWeighted(weightScratch);
            return index >= 0 ? templates[index] : null;
        }

        /// <summary>
        /// The boss room's fight: one wave holding the boss and nothing else.
        ///
        /// No escorts, deliberately. The boss <em>is</em> the fight — three grunts swinging
        /// alongside it would destroy the readability of a 700 ms telegraph, and a lone boss is
        /// what lets its health bar mean "how far through this am I".
        ///
        /// With no boss archetype configured the old placeholder path still runs, so a content set
        /// without a boss still produces a playable level.
        /// </summary>
        List<WavePlan> BuildBossWaves(IRandomSource random, IRoomTemplate template, int roomIndex)
        {
            string bossId = settings.BossArchetypeId;
            if (!string.IsNullOrEmpty(bossId))
            {
                return new List<WavePlan>(1)
                {
                    new WavePlan(new List<SpawnAssignment> { new SpawnAssignment(bossId, BossSpawnPointIndex) }),
                };
            }

            int waveCount = Mathf.Max(1, settings.BossRoomWaves);
            float budget = settings.BaseWaveBudget
                           + settings.BudgetGrowthPerRoom * roomIndex
                           + settings.BossBudgetBonus;

            var waves = new List<WavePlan>(waveCount);
            for (int w = 0; w < waveCount; w++)
                waves.Add(BuildWave(random, template, budget));

            return waves;
        }

        /// <summary>True when any of this slot's variants is the elite duel.</summary>
        static bool HasEliteVariant(IRoomScript script)
        {
            IReadOnlyList<IRoomScriptVariant> variants = script.Variants;
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].IsElite)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// How many of this level's slots could host the elite. Counted before the walk so the
        /// last one can be told it is the last chance — which is what turns "maybe an elite"
        /// into "exactly one".
        /// </summary>
        int CountEliteSlots(int standardRooms)
        {
            int count = 0;
            for (int index = 0; index < standardRooms; index++)
            {
                IRoomScript script = FindRoomScript(index);
                if (script != null && HasEliteVariant(script))
                    count++;
            }

            return count;
        }

        /// <summary>The authored script covering a standard-room slot, or null for the budget path.</summary>
        IRoomScript FindRoomScript(int roomIndex)
        {
            IReadOnlyList<IRoomScript> scripts = settings.RoomScripts;
            if (scripts == null || roomIndex >= scripts.Count)
                return null;

            IRoomScript script = scripts[roomIndex];
            return script != null && script.Variants != null && script.Variants.Count > 0 ? script : null;
        }

        /// <summary>
        /// Expands one authored room script: a seeded draw picks the variant, then every wave is
        /// resolved onto the template's spawn points. This is how ENEMIES_BIOME1.md §5's
        /// composition rules (vocabulary → sentence → elite) are honoured literally rather than
        /// hoped for from a budget roll.
        /// </summary>
        List<WavePlan> BuildScriptedWaves(
            IRandomSource random, IRoomTemplate template, IRoomScript script,
            EliteRequirement requirement, out bool tookElite)
        {
            IReadOnlyList<IRoomScriptVariant> variants = script.Variants;

            // Narrow to what the level's elite quota still permits, then draw among those. The
            // draw stays a single seeded pick either way, so a slot always costs one decision.
            variantScratch.Clear();
            for (int i = 0; i < variants.Count; i++)
            {
                IRoomScriptVariant candidate = variants[i];
                bool allowed =
                    requirement == EliteRequirement.Free
                    || (requirement == EliteRequirement.Required && candidate.IsElite)
                    || (requirement == EliteRequirement.Forbidden && !candidate.IsElite);

                if (allowed)
                    variantScratch.Add(candidate);
            }

            // A slot with nothing left to offer falls back to the full list rather than producing
            // an empty room: a content set that cannot satisfy the quota should still be playable,
            // and the soak test is what catches it.
            if (variantScratch.Count == 0)
            {
                for (int i = 0; i < variants.Count; i++)
                    variantScratch.Add(variants[i]);
            }

            IRoomScriptVariant variant =
                variantScratch[variantScratch.Count == 1 ? 0 : random.NextInt(0, variantScratch.Count)];
            tookElite = variant.IsElite;

            var waves = new List<WavePlan>(variant.Waves.Count);
            for (int w = 0; w < variant.Waves.Count; w++)
                waves.Add(BuildScriptedWave(random, template, variant.Waves[w]));

            return waves;
        }

        WavePlan BuildScriptedWave(IRandomSource random, IRoomTemplate template, IReadOnlyList<ScriptedSpawn> entries)
        {
            spawnPointScratch.Clear();
            for (int i = 0; i < template.SpawnPointCount; i++)
                spawnPointScratch.Add(i);
            random.Shuffle(spawnPointScratch);

            var spawns = new List<SpawnAssignment>();

            // Exclusive archetypes claim unique points first. Anything the template cannot seat
            // is dropped rather than doubled up: two Cerashorns fused inside each other is worse
            // than one missing, and the authored waves are sized to the smallest template anyway.
            int next = 0;
            for (int e = 0; e < entries.Count; e++)
            {
                ScriptedSpawn entry = entries[e];
                if (ArchetypeAllowsSharedSpawnPoints(entry.ArchetypeId))
                    continue;

                for (int c = 0; c < entry.Count && next < spawnPointScratch.Count; c++)
                    spawns.Add(new SpawnAssignment(entry.ArchetypeId, spawnPointScratch[next++]));
            }

            // Swarm archetypes cycle through the remaining (then all) points — a swarm's size is
            // authored, not capped by the room. The runner fans out same-point spawns on placement.
            int cursor = next;
            for (int e = 0; e < entries.Count; e++)
            {
                ScriptedSpawn entry = entries[e];
                if (!ArchetypeAllowsSharedSpawnPoints(entry.ArchetypeId))
                    continue;

                for (int c = 0; c < entry.Count; c++)
                {
                    spawns.Add(new SpawnAssignment(entry.ArchetypeId, spawnPointScratch[cursor % spawnPointScratch.Count]));
                    cursor++;
                }
            }

            return new WavePlan(spawns);
        }

        bool ArchetypeAllowsSharedSpawnPoints(string archetypeId)
        {
            IReadOnlyList<IEnemyArchetype> archetypes = settings.Archetypes;
            for (int i = 0; i < archetypes.Count; i++)
            {
                if (archetypes[i] != null && archetypes[i].Id == archetypeId)
                    return archetypes[i].AllowsSharedSpawnPoints;
            }

            return false;
        }

        List<WavePlan> BuildWaves(IRandomSource random, IRoomTemplate template, int roomIndex, float levelBudgetBonus)
        {
            int waveCount = random.NextInt(
                Mathf.Max(1, settings.MinWavesPerRoom),
                Mathf.Max(1, settings.MaxWavesPerRoom) + 1);

            float budget = settings.BaseWaveBudget + settings.BudgetGrowthPerRoom * roomIndex + levelBudgetBonus;
            var waves = new List<WavePlan>(waveCount);

            for (int w = 0; w < waveCount; w++)
                waves.Add(BuildWave(random, template, budget));

            return waves;
        }

        WavePlan BuildWave(IRandomSource random, IRoomTemplate template, float budget)
        {
            // A wave can never exceed the spawn points the room actually has — that is the
            // invariant the solvability test leans on.
            int slots = Mathf.Min(template.SpawnPointCount, Mathf.Max(1, settings.MaxEnemiesPerWave));

            spawnPointScratch.Clear();
            for (int i = 0; i < template.SpawnPointCount; i++)
                spawnPointScratch.Add(i);
            random.Shuffle(spawnPointScratch);

            var spawns = new List<SpawnAssignment>();
            float remaining = budget;

            for (int slot = 0; slot < slots; slot++)
            {
                IEnemyArchetype archetype = PickAffordableArchetype(random, remaining, spawns.Count == 0);
                if (archetype == null)
                    break;

                spawns.Add(new SpawnAssignment(archetype.Id, spawnPointScratch[slot]));
                remaining -= Mathf.Max(0f, archetype.Cost);
            }

            return new WavePlan(spawns);
        }

        /// <summary>
        /// Picks an archetype the remaining budget can pay for. <paramref name="forceAtLeastOne"/>
        /// ignores the budget for the first slot, because an empty wave would be a room that
        /// clears itself — the one outcome that makes a level unplayable.
        /// </summary>
        IEnemyArchetype PickAffordableArchetype(IRandomSource random, float remaining, bool forceAtLeastOne)
        {
            weightScratch.Clear();
            IReadOnlyList<IEnemyArchetype> archetypes = settings.Archetypes;

            for (int i = 0; i < archetypes.Count; i++)
            {
                IEnemyArchetype archetype = archetypes[i];
                bool usable = archetype != null
                              && archetype.SelectionWeight > 0f
                              && (forceAtLeastOne || Mathf.Max(0f, archetype.Cost) <= remaining);

                weightScratch.Add(usable ? archetype.SelectionWeight : 0f);
            }

            int index = random.PickWeighted(weightScratch);
            return index >= 0 ? archetypes[index] : null;
        }
    }
}
