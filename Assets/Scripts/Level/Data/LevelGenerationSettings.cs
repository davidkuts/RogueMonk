using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Level
{
    /// <summary>One authored spawn line: an archetype and how many of it.</summary>
    [Serializable]
    public sealed class ScriptedWaveEntry
    {
        public EnemyArchetypeDefinition archetype;
        [Min(1)] public int count = 1;
    }

    /// <summary>One authored wave. The next wave starts once this one is dead.</summary>
    [Serializable]
    public sealed class ScriptedWave
    {
        public List<ScriptedWaveEntry> entries = new List<ScriptedWaveEntry>();
    }

    /// <summary>One fight a scripted room can host. The generator draws one variant per run.</summary>
    [Serializable]
    public sealed class ScriptedRoomVariant
    {
        [Tooltip("Author note only — which §5 lesson this composition teaches.")]
        public string note;

        [Tooltip("Tick for the slot's elite duel. A level places EXACTLY ONE elite variant across all its slots: the seed decides which one, never whether there is one.")]
        public bool isElite;

        public List<ScriptedWave> waves = new List<ScriptedWave>();
    }

    /// <summary>Authored composition for one standard-room slot, positional by list index.</summary>
    [Serializable]
    public sealed class ScriptedRoom
    {
        [Tooltip("Author note only — e.g. 'vocabulary room' or 'elite room'.")]
        public string note;
        public List<ScriptedRoomVariant> variants = new List<ScriptedRoomVariant>();
    }

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

        [Header("Scripted rooms (ENEMIES_BIOME1.md §5)")]
        [SerializeField, Tooltip("Authored wave composition per standard-room slot, in order. Rooms beyond this list (or slots with no variants) fall back to the budget-weighted generator.")]
        List<ScriptedRoom> roomScripts = new List<ScriptedRoom>();

        [Header("Door rewards (REWARDS.md §8)")]
        [SerializeField, Tooltip("The tier-parity reward generator's tuning. Leave empty and every fork degrades to one Minutes-cache door.")]
        RewardGenerationConfig rewardConfig;

        readonly List<IRoomTemplate> templateView = new List<IRoomTemplate>();
        readonly List<IEnemyArchetype> archetypeView = new List<IEnemyArchetype>();
        readonly List<IRoomScript> roomScriptView = new List<IRoomScript>();

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

        public IReadOnlyList<IRoomScript> RoomScripts
        {
            get
            {
                roomScriptView.Clear();
                for (int i = 0; i < roomScripts.Count; i++)
                    roomScriptView.Add(new RoomScriptView(roomScripts[i]));

                return roomScriptView;
            }
        }

        /// <summary>
        /// Adapts the serialized script data to the engine-free view the generator consumes,
        /// resolving archetype object references to ids. Entries with no archetype and waves
        /// with no entries are skipped rather than passed on as holes.
        /// </summary>
        sealed class RoomScriptView : IRoomScript
        {
            readonly List<IRoomScriptVariant> variants = new List<IRoomScriptVariant>();

            public RoomScriptView(ScriptedRoom room)
            {
                if (room == null || room.variants == null)
                    return;

                for (int v = 0; v < room.variants.Count; v++)
                {
                    var variant = new RoomScriptVariantView(room.variants[v]);
                    if (variant.Waves.Count > 0)
                        variants.Add(variant);
                }
            }

            public IReadOnlyList<IRoomScriptVariant> Variants => variants;
        }

        sealed class RoomScriptVariantView : IRoomScriptVariant
        {
            readonly List<IReadOnlyList<ScriptedSpawn>> waves = new List<IReadOnlyList<ScriptedSpawn>>();

            public bool IsElite { get; }

            public RoomScriptVariantView(ScriptedRoomVariant variant)
            {
                if (variant == null || variant.waves == null)
                    return;

                IsElite = variant.isElite;

                for (int w = 0; w < variant.waves.Count; w++)
                {
                    ScriptedWave wave = variant.waves[w];
                    if (wave == null || wave.entries == null)
                        continue;

                    var spawns = new List<ScriptedSpawn>();
                    for (int e = 0; e < wave.entries.Count; e++)
                    {
                        ScriptedWaveEntry entry = wave.entries[e];
                        if (entry != null && entry.archetype != null && entry.count > 0)
                            spawns.Add(new ScriptedSpawn(entry.archetype.Id, entry.count));
                    }

                    if (spawns.Count > 0)
                        waves.Add(spawns);
                }
            }

            public IReadOnlyList<IReadOnlyList<ScriptedSpawn>> Waves => waves;
        }

        public IRewardConfig Rewards => rewardConfig;

        /// <summary>The concrete config, for presentation (tints, icons) and pickup effects.</summary>
        public RewardGenerationConfig RewardConfigAsset => rewardConfig;

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
