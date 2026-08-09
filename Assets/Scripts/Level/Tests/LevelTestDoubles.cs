using System.Collections.Generic;
using UnityEngine;

namespace Game.Level.Tests
{
    internal sealed class FakeRoomTemplate : IRoomTemplate
    {
        public string Id { get; set; } = "room";
        public int SpawnPointCount { get; set; } = 6;
        public float SelectionWeight { get; set; } = 1f;
        public RoomRole SupportedRoles { get; set; } = RoomRole.Any;
    }

    internal sealed class FakeArchetype : IEnemyArchetype
    {
        public string Id { get; set; } = "grunt";
        public float SelectionWeight { get; set; } = 1f;
        public float Cost { get; set; } = 1f;
        public bool AllowsSharedSpawnPoints { get; set; }
    }

    internal sealed class FakeRoomScriptVariant : IRoomScriptVariant
    {
        public List<IReadOnlyList<ScriptedSpawn>> Waves { get; set; } = new List<IReadOnlyList<ScriptedSpawn>>();

        IReadOnlyList<IReadOnlyList<ScriptedSpawn>> IRoomScriptVariant.Waves => Waves;
    }

    internal sealed class FakeRoomScript : IRoomScript
    {
        public List<IRoomScriptVariant> Variants { get; set; } = new List<IRoomScriptVariant>();

        IReadOnlyList<IRoomScriptVariant> IRoomScript.Variants => Variants;
    }

    internal sealed class FakeGenerationSettings : ILevelGenerationSettings
    {
        // Defaults chosen so existing single-level tests are unaffected: one level, no escalation.
        public int LevelsPerRun { get; set; } = 1;
        public float BudgetGrowthPerLevel { get; set; }
        public float RoomGrowthPerLevel { get; set; }
        public int MinStandardRooms { get; set; } = 5;
        public int MaxStandardRooms { get; set; } = 5;
        public int MinRooms => MinStandardRooms + 1;

        // Must allow for escalation, or a later level fails its own room-count bound.
        public int MaxRooms =>
            MaxStandardRooms + 1 + Mathf.FloorToInt(Mathf.Max(0f, RoomGrowthPerLevel) * (LevelsPerRun - 1));
        public string BossArchetypeId { get; set; } = "warden";
        public int BossRoomWaves { get; set; } = 1;
        public float BossBudgetBonus { get; set; } = 3f;
        public int MinWavesPerRoom { get; set; } = 1;
        public int MaxWavesPerRoom { get; set; } = 3;
        public float BaseWaveBudget { get; set; } = 4f;
        public float BudgetGrowthPerRoom { get; set; } = 1.5f;
        public int MaxEnemiesPerWave { get; set; } = 5;
        public bool AllowConsecutiveRepeats { get; set; }

        public IReadOnlyList<IRoomTemplate> Templates { get; set; } = new List<IRoomTemplate>
        {
            new FakeRoomTemplate { Id = "arena", SpawnPointCount = 6, SupportedRoles = RoomRole.Standard },
            new FakeRoomTemplate { Id = "corridor", SpawnPointCount = 4, SupportedRoles = RoomRole.Standard },
            new FakeRoomTemplate { Id = "pillars", SpawnPointCount = 8, SupportedRoles = RoomRole.Standard },
            new FakeRoomTemplate { Id = "vault", SpawnPointCount = 5, SupportedRoles = RoomRole.Boss },
        };

        public IReadOnlyList<IEnemyArchetype> Archetypes { get; set; } = new List<IEnemyArchetype>
        {
            new FakeArchetype { Id = "melee", Cost = 1f, SelectionWeight = 3f },
            new FakeArchetype { Id = "ranged", Cost = 2f, SelectionWeight = 1f },

            // Weight 0: PickWeighted skips non-positive weights, so the boss can only ever appear
            // where the generator places it by name — never rolled into an ordinary room.
            new FakeArchetype { Id = "warden", Cost = 0f, SelectionWeight = 0f },
        };

        public IReadOnlyList<IRoomScript> RoomScripts { get; set; }
    }
}
