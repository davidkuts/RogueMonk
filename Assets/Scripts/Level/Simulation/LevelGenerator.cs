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
        readonly ILevelGenerationSettings settings;

        readonly List<float> weightScratch = new List<float>();
        readonly List<int> spawnPointScratch = new List<int>();

        public LevelGenerator(ILevelGenerationSettings settings)
        {
            this.settings = settings;
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

            bool anyUsableTemplate = false;
            bool anyFinalRoom = false;
            for (int i = 0; i < settings.Templates.Count; i++)
            {
                IRoomTemplate template = settings.Templates[i];
                if (template == null || template.SelectionWeight <= 0f)
                    continue;

                if (template.SpawnPointCount > 0)
                    anyUsableTemplate = true;
                if (template.CanBeFinalRoom)
                    anyFinalRoom = true;
            }

            if (!anyUsableTemplate)
            {
                reason = "no template has both positive weight and at least one spawn point";
                return false;
            }

            if (!anyFinalRoom)
            {
                reason = "no template is allowed to be the final room";
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

            reason = null;
            return true;
        }

        public LevelPlan Generate(RunContext run)
        {
            IRandomSource random = run.Random;
            int roomCount = random.NextInt(
                Mathf.Max(1, settings.MinRooms),
                Mathf.Max(1, settings.MaxRooms) + 1);

            var rooms = new List<RoomPlan>(roomCount);
            IRoomTemplate previous = null;

            for (int index = 0; index < roomCount; index++)
            {
                bool isFinal = index == roomCount - 1;
                IRoomTemplate template = PickTemplate(random, previous, isFinal);
                previous = template;

                rooms.Add(new RoomPlan(template.Id, index, BuildWaves(random, template, index)));
            }

            return new LevelPlan(run.Seed, rooms);
        }

        IRoomTemplate PickTemplate(IRandomSource random, IRoomTemplate previous, bool mustAllowFinal)
        {
            IRoomTemplate picked = TryPickTemplate(random, previous, mustAllowFinal);

            // Relax the no-repeats rule rather than fail: with a small template set it can be
            // impossible to satisfy, and a repeated room beats no level at all.
            if (picked == null)
                picked = TryPickTemplate(random, null, mustAllowFinal);

            return picked;
        }

        IRoomTemplate TryPickTemplate(IRandomSource random, IRoomTemplate exclude, bool mustAllowFinal)
        {
            weightScratch.Clear();
            IReadOnlyList<IRoomTemplate> templates = settings.Templates;

            for (int i = 0; i < templates.Count; i++)
            {
                IRoomTemplate template = templates[i];
                bool usable = template != null
                              && template.SelectionWeight > 0f
                              && template.SpawnPointCount > 0
                              && (!mustAllowFinal || template.CanBeFinalRoom)
                              && (settings.AllowConsecutiveRepeats || exclude == null || template.Id != exclude.Id);

                weightScratch.Add(usable ? template.SelectionWeight : 0f);
            }

            int index = random.PickWeighted(weightScratch);
            return index >= 0 ? templates[index] : null;
        }

        List<WavePlan> BuildWaves(IRandomSource random, IRoomTemplate template, int roomIndex)
        {
            int waveCount = random.NextInt(
                Mathf.Max(1, settings.MinWavesPerRoom),
                Mathf.Max(1, settings.MaxWavesPerRoom) + 1);

            float budget = settings.BaseWaveBudget + settings.BudgetGrowthPerRoom * roomIndex;
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
