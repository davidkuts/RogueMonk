using System.Collections.Generic;

namespace Game.Level.Tests
{
    internal sealed class FakeRoomTemplate : IRoomTemplate
    {
        public string Id { get; set; } = "room";
        public int SpawnPointCount { get; set; } = 6;
        public float SelectionWeight { get; set; } = 1f;
        public bool CanBeFinalRoom { get; set; } = true;
    }

    internal sealed class FakeArchetype : IEnemyArchetype
    {
        public string Id { get; set; } = "grunt";
        public float SelectionWeight { get; set; } = 1f;
        public float Cost { get; set; } = 1f;
    }

    internal sealed class FakeGenerationSettings : ILevelGenerationSettings
    {
        public int MinRooms { get; set; } = 6;
        public int MaxRooms { get; set; } = 7;
        public int MinWavesPerRoom { get; set; } = 1;
        public int MaxWavesPerRoom { get; set; } = 3;
        public float BaseWaveBudget { get; set; } = 4f;
        public float BudgetGrowthPerRoom { get; set; } = 1.5f;
        public int MaxEnemiesPerWave { get; set; } = 5;
        public bool AllowConsecutiveRepeats { get; set; }

        public IReadOnlyList<IRoomTemplate> Templates { get; set; } = new List<IRoomTemplate>
        {
            new FakeRoomTemplate { Id = "arena", SpawnPointCount = 6 },
            new FakeRoomTemplate { Id = "corridor", SpawnPointCount = 4 },
            new FakeRoomTemplate { Id = "pillars", SpawnPointCount = 8 },
            new FakeRoomTemplate { Id = "vault", SpawnPointCount = 5, CanBeFinalRoom = true },
        };

        public IReadOnlyList<IEnemyArchetype> Archetypes { get; set; } = new List<IEnemyArchetype>
        {
            new FakeArchetype { Id = "melee", Cost = 1f, SelectionWeight = 3f },
            new FakeArchetype { Id = "ranged", Cost = 2f, SelectionWeight = 1f },
        };
    }
}
