using System.Collections.Generic;
using UnityEngine;

namespace Game.Level
{
    /// <summary>All level-generation tuning, in data as CLAUDE.md requires.</summary>
    [CreateAssetMenu(menuName = "Monk/Level Generation Settings", fileName = "LevelGenerationSettings")]
    public sealed class LevelGenerationSettings : ScriptableObject, ILevelGenerationSettings
    {
        [Header("Shape")]
        [SerializeField, Tooltip("DESIGN.md: 6-7 rooms per level.")]
        int minRooms = 6;
        [SerializeField] int maxRooms = 7;
        [SerializeField] int minWavesPerRoom = 1;
        [SerializeField] int maxWavesPerRoom = 3;

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

        public int MinRooms => minRooms;
        public int MaxRooms => maxRooms;
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
