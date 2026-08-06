using System.Collections.Generic;

namespace Game.Level
{
    /// <summary>A room template as the generator sees it. No prefab, no engine types.</summary>
    public interface IRoomTemplate
    {
        string Id { get; }

        /// <summary>How many tagged spawn points the prefab provides. Caps a wave's size.</summary>
        int SpawnPointCount { get; }

        /// <summary>Relative likelihood of being picked. Zero excludes the template.</summary>
        float SelectionWeight { get; }

        /// <summary>True if this template may be used as the level's final room.</summary>
        bool CanBeFinalRoom { get; }
    }

    /// <summary>An enemy archetype as the generator sees it.</summary>
    public interface IEnemyArchetype
    {
        string Id { get; }

        /// <summary>Relative likelihood of being picked for a spawn slot.</summary>
        float SelectionWeight { get; }

        /// <summary>Budget cost, so a wave of tough enemies is smaller than a wave of weak ones.</summary>
        float Cost { get; }
    }

    /// <summary>Tuning for level generation. All values are data (CLAUDE.md hard rule 2).</summary>
    public interface ILevelGenerationSettings
    {
        int MinRooms { get; }
        int MaxRooms { get; }

        int MinWavesPerRoom { get; }
        int MaxWavesPerRoom { get; }

        /// <summary>Spend budget for the first room's waves.</summary>
        float BaseWaveBudget { get; }

        /// <summary>Extra budget added per room index, so later rooms escalate.</summary>
        float BudgetGrowthPerRoom { get; }

        /// <summary>Hard cap on simultaneous enemies, independent of budget or spawn points.</summary>
        int MaxEnemiesPerWave { get; }

        /// <summary>Allow the same template twice in a row. Off makes levels feel more varied.</summary>
        bool AllowConsecutiveRepeats { get; }

        IReadOnlyList<IRoomTemplate> Templates { get; }
        IReadOnlyList<IEnemyArchetype> Archetypes { get; }
    }
}
