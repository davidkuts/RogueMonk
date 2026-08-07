using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// One entry in a boss's moveset. A plain serializable class rather than its own asset:
    /// a move is only ever meaningful inside the boss that owns it.
    /// </summary>
    [System.Serializable]
    public sealed class BossMove : IBossMove
    {
        [SerializeField, Tooltip("Identifier for logs and telegraph debugging.")]
        string id = "Move";

        [SerializeField, Tooltip("Played in order. One = a single swing. Two = a scripted chain that always completes.")]
        AttackDefinition[] links = new AttackDefinition[0];

        [SerializeField, Tooltip("Gap between one link ending and the next starting.")]
        float linkDelaySeconds = 0.3f;

        [Header("When this move is legal")]
        [SerializeField, Tooltip("Closest distance at which this move may be chosen.")]
        float minRange;
        [SerializeField, Tooltip("Furthest distance at which this move may be chosen.")]
        float maxRange = 3.2f;
        [SerializeField, Tooltip("Relative likelihood among the legal moves. 0 disables it.")]
        float selectionWeight = 1f;
        [SerializeField, Tooltip("Phase index that unlocks this move. 0 = available from the start.")]
        int unlockedAtPhase;
        [SerializeField, Tooltip("Enforced gap before this specific move may be chosen again.")]
        float moveCooldownSeconds = 2f;

        [Header("Delivery")]
        [SerializeField, Tooltip("How far the boss travels across each link's active frames. 0 roots it.")]
        float lungeDistance;
        [SerializeField, Tooltip("Projectiles fired when a link goes active. 0 makes this a melee move.")]
        int projectileCount;
        [SerializeField, Tooltip("Total fan width, centred on facing.")]
        float projectileSpreadDegrees = 24f;

        IReadOnlyList<IAttackDefinition> cachedLinks;

        public string Id => id;
        public float LinkDelaySeconds => linkDelaySeconds;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
        public float SelectionWeight => selectionWeight;
        public int UnlockedAtPhase => unlockedAtPhase;
        public float MoveCooldownSeconds => moveCooldownSeconds;
        public float LungeDistance => lungeDistance;
        public int ProjectileCount => projectileCount;
        public float ProjectileSpreadDegrees => projectileSpreadDegrees;

        public IReadOnlyList<IAttackDefinition> Links
        {
            get
            {
                if (cachedLinks == null)
                {
                    // Drop null entries here rather than making every consumer null-check a link.
                    var built = new List<IAttackDefinition>(links.Length);
                    for (int i = 0; i < links.Length; i++)
                    {
                        if (links[i] != null)
                            built.Add(links[i]);
                    }
                    cachedLinks = built;
                }

                return cachedLinks;
            }
        }

        /// <summary>Drops the cached projection so inspector edits take effect without a reload.</summary>
        public void InvalidateCache() => cachedLinks = null;
    }

    /// <summary>A health-tied phase. Phase 0 is implicit, so this is the second phase onward.</summary>
    [System.Serializable]
    public sealed class BossPhase : IBossPhase
    {
        [SerializeField, Range(0f, 1f), Tooltip("Health fraction at or below which this phase begins.")]
        float healthFractionThreshold = 0.55f;

        [SerializeField, Tooltip("Scales the attack cooldown. Below 1 makes the boss press harder.")]
        float cooldownMultiplier = 0.7f;

        public float HealthFractionThreshold => healthFractionThreshold;
        public float CooldownMultiplier => cooldownMultiplier;
    }

    /// <summary>
    /// A boss's tuning. Subclasses <see cref="EnemyDefinition"/> so it inherits vitals, poise and
    /// movement unchanged — the boss prefab's <c>EnemyActor</c> field already takes this type, and
    /// the two trash archetypes never gain an empty moveset they would carry forever.
    ///
    /// Bosses are Immune tier, so the inherited poise and armour pools should be left at zero:
    /// <c>PoiseSystem</c> returns Immune before touching them, and a non-zero pool would be a
    /// number the inspector shows but nothing can ever move.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Boss Definition", fileName = "BossDefinition")]
    public sealed class BossDefinition : EnemyDefinition, IBossDefinition
    {
        [Header("Boss identity")]
        [SerializeField, Tooltip("Shown on the boss bar and the room banner.")]
        string displayName = "The Warden";

        [Header("Moveset")]
        [SerializeField] BossMove[] moves = new BossMove[0];

        [Header("Phases (after the first; descending health order)")]
        [SerializeField] BossPhase[] phases = new BossPhase[0];

        [SerializeField, Tooltip("Inert, vulnerable window when a phase threshold is crossed. The punish window an Immune enemy earns with damage instead of poise.")]
        float phaseTransitionSeconds = 1.4f;

        [Header("Selection")]
        [SerializeField, Range(0f, 1f), Tooltip("Weight applied to the move just used. Discourages repeats without ever forbidding them. 1 disables the effect.")]
        float repeatWeightMultiplier = 0.35f;

        IReadOnlyList<IBossMove> cachedMoves;
        IReadOnlyList<IBossPhase> cachedPhases;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public float PhaseTransitionSeconds => phaseTransitionSeconds;
        public float RepeatWeightMultiplier => repeatWeightMultiplier;

        public IReadOnlyList<IBossMove> Moves
        {
            get
            {
                if (cachedMoves == null)
                {
                    // A move with no links could be chosen and then do nothing, hanging the brain
                    // on an attack that never starts. Drop those here, once.
                    var built = new List<IBossMove>(moves.Length);
                    for (int i = 0; i < moves.Length; i++)
                    {
                        if (moves[i] != null && moves[i].Links.Count > 0)
                            built.Add(moves[i]);
                    }
                    cachedMoves = built;
                }

                return cachedMoves;
            }
        }

        public IReadOnlyList<IBossPhase> Phases
        {
            get
            {
                if (cachedPhases == null)
                {
                    var built = new List<IBossPhase>(phases.Length);
                    for (int i = 0; i < phases.Length; i++)
                    {
                        if (phases[i] != null)
                            built.Add(phases[i]);
                    }
                    cachedPhases = built;
                }

                return cachedPhases;
            }
        }

        void OnValidate()
        {
            // The projections above are built once; without this an inspector edit would be
            // invisible until the next domain reload.
            cachedMoves = null;
            cachedPhases = null;
            for (int i = 0; i < moves.Length; i++)
                moves[i]?.InvalidateCache();

            if (Tier != StaggerTier.Immune)
                Debug.LogWarning($"{name}: bosses are expected to be Immune tier (DESIGN.md § Stagger tiers).", this);

            for (int i = 1; i < phases.Length; i++)
            {
                if (phases[i] == null || phases[i - 1] == null)
                    continue;

                if (phases[i].HealthFractionThreshold >= phases[i - 1].HealthFractionThreshold)
                {
                    Debug.LogWarning(
                        $"{name}: phase {i + 1}'s threshold must be below phase {i}'s, or it can never be reached.",
                        this);
                }
            }
        }
    }
}
