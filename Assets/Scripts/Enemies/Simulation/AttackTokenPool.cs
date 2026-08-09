using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Which queue an attacker competes in. Melee and ranged are capped separately.</summary>
    public enum AttackTokenKind
    {
        Melee = 0,
        Ranged = 1,
    }

    /// <summary>
    /// Decides how many enemies may be attacking at once (the Hades pattern).
    ///
    /// <para>Without it, "how hard is this room" is whatever the wave composition happened to
    /// total, and eight enemies in range means eight simultaneous wind-ups — unreadable, and
    /// unfair in a game whose whole promise is that every attack can be read. With it, a pack can
    /// be as large as the fiction wants while the number of things actually swinging stays a
    /// tuned constant.</para>
    ///
    /// <para>Melee and ranged are counted separately because they do not compete for the same
    /// space: two archers and two raptors is a legible fight, four raptors is a pile-on. A
    /// per-group cap sits on top so a single archetype cannot monopolise the global budget —
    /// ENEMIES_BIOME1.md § 2.1 caps Swiftjaw at 2 concurrent attackers regardless of pack size,
    /// and that number arrives here from the Swiftjaw's own asset rather than from raptor code.</para>
    ///
    /// <para>Engine-free by CLAUDE.md rule 1; holders are stored as opaque objects and never
    /// dereferenced, so a MonoBehaviour can pass <c>this</c> without dragging UnityEngine in.</para>
    /// </summary>
    public sealed class AttackTokenPool
    {
        readonly Dictionary<object, Entry> held = new Dictionary<object, Entry>();

        int meleeCap;
        int rangedCap;

        readonly struct Entry
        {
            public Entry(AttackTokenKind kind, string groupId)
            {
                Kind = kind;
                GroupId = groupId;
            }

            public AttackTokenKind Kind { get; }

            public string GroupId { get; }
        }

        public AttackTokenPool(int meleeCap = 2, int rangedCap = 2)
        {
            this.meleeCap = Mathf.Max(0, meleeCap);
            this.rangedCap = Mathf.Max(0, rangedCap);
        }

        public int MeleeCap => meleeCap;

        public int RangedCap => rangedCap;

        public int ActiveMelee { get; private set; }

        public int ActiveRanged { get; private set; }

        public int ActiveTotal => held.Count;

        /// <summary>Retunes the caps live, for the debug overlay and for tests.</summary>
        public void Configure(int newMeleeCap, int newRangedCap)
        {
            meleeCap = Mathf.Max(0, newMeleeCap);
            rangedCap = Mathf.Max(0, newRangedCap);
        }

        public bool Holds(object holder) => holder != null && held.ContainsKey(holder);

        /// <summary>How many holders from <paramref name="groupId"/> are currently attacking.</summary>
        public int ActiveInGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return 0;

            int count = 0;
            foreach (KeyValuePair<object, Entry> pair in held)
            {
                if (pair.Value.GroupId == groupId)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Would <see cref="TryAcquire"/> succeed right now? Asks without taking.
        ///
        /// <para>This is what lets a brain treat "no token" as an ordinary reason to hold position,
        /// the same way it treats a cooldown, instead of committing to a move and then having the
        /// adapter refuse it — which would spend the move's cooldown on an attack that never
        /// happened. The check and the acquire both run inside one enemy's <c>Update</c>, so
        /// nothing can slip between them.</para>
        /// </summary>
        public bool CanAcquire(object holder, AttackTokenKind kind, string groupId = null, int groupCap = 0)
        {
            if (holder == null)
                return false;

            if (held.ContainsKey(holder))
                return true;

            int active = kind == AttackTokenKind.Melee ? ActiveMelee : ActiveRanged;
            int cap = kind == AttackTokenKind.Melee ? meleeCap : rangedCap;

            if (active >= cap)
                return false;

            return groupCap <= 0 || ActiveInGroup(groupId) < groupCap;
        }

        /// <summary>
        /// Asks permission to begin an attack.
        /// </summary>
        /// <param name="groupCap">
        /// Per-archetype ceiling, from that enemy's own definition. 0 or less means "no group
        /// limit, the global cap is the only gate".
        /// </param>
        /// <returns>True when the holder may attack — including when it already holds a token.</returns>
        public bool TryAcquire(object holder, AttackTokenKind kind, string groupId = null, int groupCap = 0)
        {
            if (holder == null)
                return false;

            // Idempotent: a controller that asks every frame while attacking must not be counted
            // twice, and must never be told "no" halfway through its own wind-up.
            if (held.ContainsKey(holder))
                return true;

            int active = kind == AttackTokenKind.Melee ? ActiveMelee : ActiveRanged;
            int cap = kind == AttackTokenKind.Melee ? meleeCap : rangedCap;

            if (active >= cap)
                return false;

            if (groupCap > 0 && ActiveInGroup(groupId) >= groupCap)
                return false;

            held.Add(holder, new Entry(kind, groupId));

            if (kind == AttackTokenKind.Melee)
                ActiveMelee++;
            else
                ActiveRanged++;

            return true;
        }

        /// <summary>
        /// Hands a token back. Safe to call on a holder that has none, which is what lets the
        /// controller release from every exit path — attack ended, staggered, died, disabled —
        /// without tracking which of them already did it.
        ///
        /// <para>A leaked token is the worst failure this class has: it is invisible, it never
        /// recovers, and it ends with a room of enemies that will not attack. Releasing
        /// unconditionally and often is the cheap defence.</para>
        /// </summary>
        public void Release(object holder)
        {
            if (holder == null || !held.TryGetValue(holder, out Entry entry))
                return;

            held.Remove(holder);

            if (entry.Kind == AttackTokenKind.Melee)
                ActiveMelee = Mathf.Max(0, ActiveMelee - 1);
            else
                ActiveRanged = Mathf.Max(0, ActiveRanged - 1);
        }

        /// <summary>Drops every token. Used when a room is torn down.</summary>
        public void ReleaseAll()
        {
            held.Clear();
            ActiveMelee = 0;
            ActiveRanged = 0;
        }
    }
}
