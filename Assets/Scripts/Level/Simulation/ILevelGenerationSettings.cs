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

        /// <summary>Roles this authored room is suitable for.</summary>
        RoomRole SupportedRoles { get; }
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
        /// <summary>
        /// Levels in a full run. Each ends in a boss and, between them, a boon choice. One makes a
        /// run a single level, which is exactly what it was before runs existed.
        /// </summary>
        int LevelsPerRun { get; }

        /// <summary>
        /// Extra spend budget added per level index, on top of the per-room growth. This is the
        /// escalation knob: without it level three would be no harder than level one, and the boons
        /// the player has collected by then would make it easier.
        /// </summary>
        float BudgetGrowthPerLevel { get; }

        /// <summary>Extra standard rooms added per level index. Fractional values round down.</summary>
        float RoomGrowthPerLevel { get; }

        /// <summary>Ordinary fight rooms before the boss.</summary>
        int MinStandardRooms { get; }
        int MaxStandardRooms { get; }

        /// <summary>
        /// Total rooms including the appended boss room, across <em>any</em> level of a run — so
        /// <see cref="MaxRooms"/> has to allow for everything <see cref="RoomGrowthPerLevel"/> can
        /// add by the final level. The validator checks a single level's plan against this, and it
        /// has no idea which level it is looking at.
        /// </summary>
        int MinRooms { get; }
        int MaxRooms { get; }

        /// <summary>
        /// The archetype that <em>is</em> the boss fight. When set, the boss room holds exactly one
        /// wave containing exactly this one enemy, and <see cref="BossRoomWaves"/> and
        /// <see cref="BossBudgetBonus"/> are ignored. Left empty, the boss room falls back to an
        /// ordinary dense wave — which is what keeps a content set with no boss still playable.
        /// </summary>
        string BossArchetypeId { get; }

        /// <summary>Waves in the boss room. Ignored once <see cref="BossArchetypeId"/> is set.</summary>
        int BossRoomWaves { get; }

        /// <summary>
        /// Extra budget the boss room gets on top of normal escalation. Ignored once
        /// <see cref="BossArchetypeId"/> is set.
        /// </summary>
        float BossBudgetBonus { get; }

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
