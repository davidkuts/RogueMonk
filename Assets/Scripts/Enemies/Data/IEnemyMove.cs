using System.Collections.Generic;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// One entry in an enemy's repertoire: an attack (or a scripted chain of them) plus the
    /// conditions under which a brain may choose it.
    ///
    /// <para>This is the half of <see cref="IBossMove"/> that was never boss-specific. A raptor
    /// choosing between a pounce and a snap combo is doing exactly what the Stone Warden does
    /// when it chooses between a cleave and a volley — a range band, a cooldown and a weight. Only
    /// phases, projectile fans, hazard scatters and retaliation are genuinely a boss's, and those
    /// stay on <see cref="IBossMove"/>.</para>
    ///
    /// <para>Hoisting the shared members rather than copying them means the boss and the biome
    /// roster cannot drift apart on what "a move" is, and it cost the boss nothing:
    /// <c>IBossMove</c> declares the same members it always did.</para>
    /// </summary>
    public interface IEnemyMove
    {
        string Id { get; }

        /// <summary>
        /// Attacks played in order. One element is a single swing; two or more is a scripted chain
        /// that always completes — the enemy never chooses to continue, so there is no drop window
        /// and no combo timer to author.
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

        /// <summary>Enforced gap before this specific move may be chosen again.</summary>
        float MoveCooldownSeconds { get; }

        /// <summary>Distance the attacker travels across each link's active frames. 0 roots it.</summary>
        float LungeDistance { get; }
    }
}
