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
        public RoomPlan(
            string templateId,
            int index,
            IReadOnlyList<WavePlan> waves,
            RoomRole role = RoomRole.Standard,
            IReadOnlyList<RewardChoice> exitRewards = null)
        {
            TemplateId = templateId;
            Index = index;
            Waves = waves ?? new List<WavePlan>();
            Role = role;
            ExitRewards = exitRewards ?? new List<RewardChoice>();
        }

        /// <summary>
        /// The doors offered once this room is cleared, one entry per door, each carrying the
        /// reward waiting in the room behind it (REWARDS.md §8 tier parity: same tier, distinct
        /// types). Decided at generation time so a seed reproduces the doors as faithfully as
        /// the fights. Exactly one boss-door entry when the boss is next; empty for the final
        /// room, whose clear ends the level with no door.
        /// </summary>
        public IReadOnlyList<RewardChoice> ExitRewards { get; }

        public int ExitDoorCount => ExitRewards.Count;

        public string TemplateId { get; }

        /// <summary>What this room is for. Exactly one room per level is <see cref="RoomRole.Boss"/>.</summary>
        public RoomRole Role { get; }

        public bool IsBossRoom => Role == RoomRole.Boss;

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

        /// <summary>Zero-based index of the boss room, or -1 if there somehow isn't one.</summary>
        public int BossRoomIndex
        {
            get
            {
                for (int i = 0; i < Rooms.Count; i++)
                {
                    if (Rooms[i].IsBossRoom)
                        return i;
                }

                return -1;
            }
        }

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
