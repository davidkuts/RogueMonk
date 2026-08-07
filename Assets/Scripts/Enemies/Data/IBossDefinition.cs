using System.Collections.Generic;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// One move in a boss's repertoire: an attack (or a scripted chain of them) plus the
    /// conditions under which the brain is allowed to choose it.
    ///
    /// A move is the unit of *decision*; the attacks it holds are the unit of *timing*, and those
    /// still run through the same <c>AttackStateMachine</c> every other attack in the game uses.
    /// </summary>
    public interface IBossMove
    {
        string Id { get; }

        /// <summary>
        /// Attacks played in order. One element is a single swing; two is a scripted two-hit that
        /// always completes — the boss never chooses to continue, so there is no drop window.
        /// </summary>
        IReadOnlyList<IAttackDefinition> Links { get; }

        /// <summary>Gap between one link ending and the next starting. The only pause inside a chain.</summary>
        float LinkDelaySeconds { get; }

        /// <summary>Closest distance at which this move is legal.</summary>
        float MinRange { get; }

        /// <summary>Furthest distance at which this move is legal. Outside the band it is never chosen.</summary>
        float MaxRange { get; }

        /// <summary>Relative likelihood among the currently legal moves. Zero disables it entirely.</summary>
        float SelectionWeight { get; }

        /// <summary>Zero-based phase index that unlocks this move. 0 means available from the start.</summary>
        int UnlockedAtPhase { get; }

        /// <summary>Enforced gap before this specific move may be chosen again.</summary>
        float MoveCooldownSeconds { get; }

        /// <summary>Distance the boss travels across each link's active frames. 0 roots it.</summary>
        float LungeDistance { get; }

        /// <summary>Projectiles fired when a link's active window opens. 0 makes the move melee.</summary>
        int ProjectileCount { get; }

        /// <summary>Total spread of the projectile fan, centred on facing.</summary>
        float ProjectileSpreadDegrees { get; }
    }

    /// <summary>
    /// A health-tied phase. Phase 0 is implicit and starts at full health, so this describes only
    /// the phases *after* the first.
    /// </summary>
    public interface IBossPhase
    {
        /// <summary>Health fraction at or below which this phase begins.</summary>
        float HealthFractionThreshold { get; }

        /// <summary>Scales the boss's attack cooldown, so later phases press harder.</summary>
        float CooldownMultiplier { get; }
    }

    /// <summary>
    /// Tuning contract for a boss. Extends <see cref="IEnemyDefinition"/> rather than widening it,
    /// so ordinary archetypes never carry an empty moveset and their assets stay untouched.
    ///
    /// There is deliberately no per-phase wind-up multiplier: <c>AttackStateMachine</c> reads
    /// <c>WindupSeconds</c> straight off the attack, so scaling it per phase would need a decorator
    /// around every attack. Later phases get harder by unlocking moves with their own authored
    /// frame data and by shortening the gap between them.
    /// </summary>
    public interface IBossDefinition : IEnemyDefinition
    {
        /// <summary>Name shown on the boss bar and the room banner.</summary>
        string DisplayName { get; }

        IReadOnlyList<IBossMove> Moves { get; }

        /// <summary>Phases after the first, in descending health order.</summary>
        IReadOnlyList<IBossPhase> Phases { get; }

        /// <summary>
        /// Inert, vulnerable window when a phase threshold is crossed. An Immune-tier enemy can
        /// never be staggered, so this is the punish window the player earns with damage instead
        /// of with poise — and it is what stops the boss reading as unresponsive.
        /// </summary>
        float PhaseTransitionSeconds { get; }

        /// <summary>
        /// Weight multiplier applied to the move just used, so the boss rarely repeats itself
        /// without ever being forbidden from it. 1 disables the effect.
        /// </summary>
        float RepeatWeightMultiplier { get; }

        // The death beat's length and hitstop live on EnemyActor beside the other reaction tuning
        // (hit flash, knockback damping) rather than here. Every enemy can have one; putting it on
        // the boss definition too would be the same number authored in two assets.
    }
}
