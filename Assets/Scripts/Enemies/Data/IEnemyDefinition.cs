using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// How an enemy answers being hit (DESIGN.md § Stagger tiers).
    /// </summary>
    public enum StaggerTier
    {
        /// <summary>Most trash. Poise bar breaks into an interrupt plus brief vulnerability.</summary>
        Staggerable = 0,

        /// <summary>Armour must be stripped before poise damage counts at all.</summary>
        Armored = 1,

        /// <summary>Elites and bosses. Full hit feedback, never interrupted, no knockback.</summary>
        Immune = 2,
    }

    /// <summary>
    /// Tuning contract for one enemy archetype. Implemented by a ScriptableObject at runtime
    /// and by fakes in tests, so the enemy simulation never depends on an asset type.
    /// </summary>
    public interface IEnemyDefinition
    {
        string Id { get; }

        float MaxHealth { get; }

        StaggerTier Tier { get; }

        // --- Poise / armour ---

        float PoiseMax { get; }

        /// <summary>Quiet time after the last poise damage before poise starts coming back.</summary>
        float PoiseRegenDelay { get; }

        /// <summary>Poise per second once regeneration starts.</summary>
        float PoiseRegenRate { get; }

        /// <summary>Armored tier only. Absorbs poise damage until stripped, then stays stripped.</summary>
        float ArmorMax { get; }

        /// <summary>How long a poise break keeps the enemy helpless.</summary>
        float StaggerDurationSeconds { get; }

        // --- Movement / AI ---

        float MoveSpeed { get; }

        /// <summary>Distance at which the enemy starts chasing.</summary>
        float AggroRange { get; }

        /// <summary>Distance at which it commits to an attack.</summary>
        float AttackRange { get; }

        /// <summary>Enforced gap between attacks, measured from the end of the previous one.</summary>
        float AttackCooldownSeconds { get; }

        /// <summary>How far the lunge carries the enemy across its active frames.</summary>
        float LungeDistance { get; }

        /// <summary>The attack this enemy throws. One for the MVP melee humanoid.</summary>
        IAttackDefinition Attack { get; }
    }
}
