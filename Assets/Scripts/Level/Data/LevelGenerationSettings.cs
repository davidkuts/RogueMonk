using System.Collections.Generic;
using UnityEngine;

namespace Game.Level
{
    /// <summary>All level-generation tuning, in data as CLAUDE.md requires.</summary>
    [CreateAssetMenu(menuName = "Monk/Level Generation Settings", fileName = "LevelGenerationSettings")]
    public sealed class LevelGenerationSettings : ScriptableObject, ILevelGenerationSettings
    {
        [Header("Run")]
        [SerializeField, Tooltip("Levels in a full run, each ending in a boss with a boon choice between them. 1 reproduces the pre-run single-level game.")]
        int levelsPerRun = 3;
        [SerializeField, Tooltip("Extra spend budget per level index. The escalation knob — without it, later levels are EASIER because the player arrives carrying boons.")]
        float budgetGrowthPerLevel = 2.5f;
        [SerializeField, Tooltip("Extra standard rooms per level index. Fractional values round down, so 0.5 adds a room every second level.")]
        float roomGrowthPerLevel = 0.5f;

        [Header("Shape")]
        [SerializeField, Tooltip("Ordinary fight rooms before the boss. The boss room is appended on top, so 5 here means the boss is the 6th room.")]
        int minStandardRooms = 5;
        [SerializeField] int maxStandardRooms = 5;
        [SerializeField] int minWavesPerRoom = 1;
        [SerializeField] int maxWavesPerRoom = 3;

        [Header("Boss room")]
        [SerializeField, Tooltip("The archetype that IS the boss fight. Set it and the boss room holds exactly this one enemy; the two settings below are then ignored. An object reference rather than a typed id, so renaming the asset cannot silently break the level.")]
        EnemyArchetypeDefinition bossArchetype;
        [SerializeField, Tooltip("Waves in the boss room. IGNORED once a boss archetype is set.")]
        int bossRoomWaves = 1;
        [SerializeField, Tooltip("Extra budget the boss room gets. IGNORED once a boss archetype is set.")]
        float bossBudgetBonus = 3f;

        [Header("Difficulty")]
        [SerializeField, Tooltip("Spend budget for the first room's waves.")]
        float baseWaveBudget = 3f;
        [SerializeField, Tooltip("Extra budget per room index, so later rooms escalate.")]
        float budgetGrowthPerRoom = 1.2f;
        [SerializeField, Tooltip("Hard cap on simultaneous enemies, whatever the budget allows.")]
        int maxEnemiesPerWave = 5;
        [SerializeField, Tooltip("Allow the same room template twice in a row.")]
        bool allowConsecutiveRepeats;

        [Header("Content")]
        [SerializeField] List<RoomTemplateDefinition> templates = new List<RoomTemplateDefinition>();
        [SerializeField] List<EnemyArchetypeDefinition> archetypes = new List<EnemyArchetypeDefinition>();

        readonly List<IRoomTemplate> templateView = new List<IRoomTemplate>();
        readonly List<IEnemyArchetype> archetypeView = new List<IEnemyArchetype>();

        public int LevelsPerRun => Mathf.Max(1, levelsPerRun);
        public float BudgetGrowthPerLevel => budgetGrowthPerLevel;
        public float RoomGrowthPerLevel => roomGrowthPerLevel;
        public int MinStandardRooms => minStandardRooms;
        public int MaxStandardRooms => maxStandardRooms;

        // Totals include the appended boss room. The maximum also has to allow for every room that
        // escalation can add by the final level, because the validator sees one level's plan
        // without knowing which level it is.
        public int MinRooms => minStandardRooms + 1;

        public int MaxRooms =>
            maxStandardRooms + 1 + Mathf.FloorToInt(Mathf.Max(0f, roomGrowthPerLevel) * (LevelsPerRun - 1));

        public string BossArchetypeId => bossArchetype != null ? bossArchetype.Id : null;
        public int BossRoomWaves => bossRoomWaves;
        public float BossBudgetBonus => bossBudgetBonus;
        public int MinWavesPerRoom => minWavesPerRoom;
        public int MaxWavesPerRoom => maxWavesPerRoom;
        public float BaseWaveBudget => baseWaveBudget;
        public float BudgetGrowthPerRoom => budgetGrowthPerRoom;
        public int MaxEnemiesPerWave => maxEnemiesPerWave;
        public bool AllowConsecutiveRepeats => allowConsecutiveRepeats;

        public IReadOnlyList<IRoomTemplate> Templates
        {
            get
            {
                templateView.Clear();
                for (int i = 0; i < templates.Count; i++)
                {
                    if (templates[i] != null)
                        templateView.Add(templates[i]);
                }

                return templateView;
            }
        }

        public IReadOnlyList<IEnemyArchetype> Archetypes
        {
            get
            {
                archetypeView.Clear();
                for (int i = 0; i < archetypes.Count; i++)
                {
                    if (archetypes[i] != null)
                        archetypeView.Add(archetypes[i]);
                }

                return archetypeView;
            }
        }

        public RoomTemplateDefinition FindTemplate(string id)
        {
            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i] != null && templates[i].Id == id)
                    return templates[i];
            }

            return null;
        }

        public EnemyArchetypeDefinition FindArchetype(string id)
        {
            for (int i = 0; i < archetypes.Count; i++)
            {
                if (archetypes[i] != null && archetypes[i].Id == id)
                    return archetypes[i];
            }

            return null;
        }
    }
}
