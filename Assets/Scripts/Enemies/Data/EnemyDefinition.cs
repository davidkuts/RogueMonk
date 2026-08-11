using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// One enemy archetype's tuning. Telegraph lengths live on the referenced
    /// <see cref="AttackDefinition"/>; DESIGN.md wants melee wind-ups at 400–500 ms, and
    /// Immune-tier enemies longer still (650–750 ms) to pay for never being interrupted.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject, IEnemyDefinition
    {
        [Header("Identity")]
        [SerializeField, Tooltip("The era this enemy belongs to. The Displaced Tooth Stray (and any future era-scoped effect) keys on this; None opts out of all of it.")]
        Era era = Era.None;

        [Header("Kill economy")]
        [SerializeField, Tooltip("Seconds shed on death, as drifting fragments. Proportional to toughness, not per body: a PAIR of Swiftjaws (3 each) is the baseline, one Cerashorn equals that pair, a swarm bird is worth a third of a raptor — so a six-bird wave never out-pays the triceratops it was easier than. Elites pay several times the baseline, the boss most of all.")]
        int secondsOnKill = 3;
        [SerializeField, Tooltip("Minutes trickled on death, on the same proportionality rule. Small — door caches are the real Minutes income.")]
        int minutesOnKill = 1;
        [SerializeField, Tooltip("Hours (meta currency) shed on death. Zero for everything ordinary — the Hades pattern: bosses guarantee meta currency, trash never does.")]
        int hoursOnKill;
        [SerializeField, Tooltip("Amber (premium meta) shed on death. Bosses only; the Tyrant sheds the most — he is saturated with temporal debris (REWARDS.md §1).")]
        int amberOnKill;

        [Header("Vitals")]
        [SerializeField] float maxHealth = 60f;
        [SerializeField, Tooltip("Staggerable = poise breaks. Armored = strip armour first. Immune = never interrupted.")]
        StaggerTier tier = StaggerTier.Staggerable;

        [Header("Poise / armour")]
        [SerializeField, Tooltip("Poise pool. Three punches (10 each) or one kick (30) breaks 30.")]
        float poiseMax = 30f;
        [SerializeField, Tooltip("Quiet time after the last poise damage before poise regenerates.")]
        float poiseRegenDelay = 1.5f;
        [SerializeField, Tooltip("Poise per second once regeneration starts.")]
        float poiseRegenRate = 15f;
        [SerializeField, Tooltip("Armored tier only. Does not regenerate once stripped.")]
        float armorMax = 40f;
        [SerializeField, Tooltip("How long a poise break keeps the enemy helpless — the punish window.")]
        float staggerDurationSeconds = 1.2f;
        [SerializeField, Tooltip("Stagger applied when a player combo/riposte hit lands DURING this enemy's wind-up, cancelling the attack. Shorter than a poise break — an interrupt, not a punish window. Tier rules still apply: Immune ignores it, intact armour shrugs it off. 0 opts this enemy out.")]
        float windupInterruptStaggerSeconds = 0.45f;

        [Header("Movement / AI")]
        [SerializeField] float moveSpeed = 3.2f;
        [SerializeField, Tooltip("Distance at which the enemy starts chasing.")]
        float aggroRange = 12f;
        [SerializeField, Tooltip("Distance at which it commits to an attack.")]
        float attackRange = 2.4f;
        [SerializeField, Tooltip("Enforced gap after an attack, so the player always gets a punish window.")]
        float attackCooldownSeconds = 1.1f;
        [SerializeField, Tooltip("Quiet time after spawning before it may attack. It still chases — this only stops an enemy that appeared on top of the player from swinging instantly.")]
        float spawnGraceSeconds = 1.2f;
        [SerializeField, Tooltip("How far the lunge carries the enemy across its active frames.")]
        float lungeDistance = 2.2f;

        [Header("Attack")]
        [SerializeField] AttackDefinition attack;

        [Header("Damage gate")]
        [SerializeField, Tooltip("When set, ONLY this attack can deal health damage to this enemy — everything else lands for zero (poise, knockback and feedback still apply). Set to the Riposte on Biome 1 elites: the fight becomes earn-the-counter, spend-the-counter. Leave empty for ordinary enemies.")]
        AttackDefinition onlyDamagedBy;

        [Header("Ranged only")]
        [SerializeField, Tooltip("Read only by ranged archetypes. Melee enemies ignore this block.")]
        RangedProfile ranged = new RangedProfile
        {
            PreferredMinRange = 5f,
            PreferredMaxRange = 9f,
            ProjectileSpeed = 9f,
            ProjectileLifetime = 4f,
            ProjectileRadius = 0.35f,
            KiteSpeedFraction = 0.7f,
        };

        public string Id => name;
        public Era Era => era;
        public int SecondsOnKill => Mathf.Max(0, secondsOnKill);
        public int MinutesOnKill => Mathf.Max(0, minutesOnKill);
        public int HoursOnKill => Mathf.Max(0, hoursOnKill);
        public int AmberOnKill => Mathf.Max(0, amberOnKill);
        public float MaxHealth => maxHealth;
        public StaggerTier Tier => tier;
        public float PoiseMax => poiseMax;
        public float PoiseRegenDelay => poiseRegenDelay;
        public float PoiseRegenRate => poiseRegenRate;
        public float ArmorMax => armorMax;
        public float StaggerDurationSeconds => staggerDurationSeconds;

        /// <summary>Interrupt stagger for a hit landed inside this enemy's wind-up. Not on the interface — only the actor reads it.</summary>
        public float WindupInterruptStaggerSeconds => Mathf.Max(0f, windupInterruptStaggerSeconds);
        public float MoveSpeed => moveSpeed;
        public float AggroRange => aggroRange;
        public float AttackRange => attackRange;
        public float AttackCooldownSeconds => attackCooldownSeconds;
        public float SpawnGraceSeconds => spawnGraceSeconds;
        public float LungeDistance => lungeDistance;
        public IAttackDefinition Attack => attack;
        public RangedProfile Ranged => ranged;

        /// <summary>The one attack allowed to damage this enemy, or null for no gate.</summary>
        public IAttackDefinition OnlyDamagedBy => onlyDamagedBy;
    }
}
