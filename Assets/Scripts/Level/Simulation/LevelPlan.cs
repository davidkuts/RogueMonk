using System.Collections.Generic;

namespace Game.Level
{
    /// <summary>One enemy to place, resolved to a concrete spawn point.</summary>
    public readonly struct SpawnAssignment
    {
        public readonly string ArchetypeId;

        /// <summary>Index into the room template's tagged spawn points.</summary>
        public readonly int SpawnPointIndex;

        public SpawnAssignment(string archetypeId, int spawnPointIndex)
        {
            ArchetypeId = archetypeId;
            SpawnPointIndex = spawnPointIndex;
        }

        public override string ToString() => $"{ArchetypeId}@{SpawnPointIndex}";
    }

    /// <summary>One wave inside a room. The next wave starts once this one is cleared.</summary>
    public sealed class WavePlan
    {
        public WavePlan(IReadOnlyList<SpawnAssignment> spawns)
        {
            Spawns = spawns ?? new List<SpawnAssignment>();
        }

        public IReadOnlyList<SpawnAssignment> Spawns { get; }

        public int Count => Spawns.Count;
    }

    /// <summary>One room in the level: which template to build, and what fights happen in it.</summary>
    public sealed class RoomPlan
    {
        public RoomPlan(string templateId, int index, IReadOnlyList<WavePlan> waves)
        {
            TemplateId = templateId;
            Index = index;
            Waves = waves ?? new List<WavePlan>();
        }

        public string TemplateId { get; }

        /// <summary>Position in the level, zero-based.</summary>
        public int Index { get; }

        public IReadOnlyList<WavePlan> Waves { get; }

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Waves.Count; i++)
                    total += Waves[i].Count;

                return total;
            }
        }
    }

    /// <summary>
    /// A whole level, fully decided up front from the run seed. Generating everything in one
    /// deterministic pass is what lets a soak test replay thousands of seeds and assert every
    /// one is playable, rather than discovering a broken level mid-run.
    /// </summary>
    public sealed class LevelPlan
    {
        public LevelPlan(uint seed, IReadOnlyList<RoomPlan> rooms)
        {
            Seed = seed;
            Rooms = rooms ?? new List<RoomPlan>();
        }

        public uint Seed { get; }

        public IReadOnlyList<RoomPlan> Rooms { get; }

        public int RoomCount => Rooms.Count;

        public RoomPlan FinalRoom => Rooms.Count > 0 ? Rooms[Rooms.Count - 1] : null;

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Rooms.Count; i++)
                    total += Rooms[i].TotalEnemies;

                return total;
            }
        }
    }
}
