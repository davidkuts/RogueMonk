using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Denny's whole lane in one object: hits that happen again, later, weaker.
    ///
    /// <para>BOONS.md §2 gives Echo "things happen twice: delayed repeats, re-pulses,
    /// afterimages — weak per-instance, compounding over a fight". Second Take, Encore Pulse,
    /// Reprise and Standing Wave are all the same sentence with different numbers, so they are
    /// all this one scheduler rather than four mechanics.</para>
    ///
    /// <para><b>A repeat never re-enters the hit resolver.</b> It is the consequence of a hit that
    /// already resolved, exactly like a burn tick — and running it back through the pipeline would
    /// let the modifier that schedules repeats schedule a repeat from its own repeat, forever.
    /// DESIGN.md settled this for damage-over-time in M12; the same reasoning, and the same
    /// answer, applies here.</para>
    ///
    /// <para>Engine-free so the timing and the cadence counting are testable. The scheduler holds
    /// the target as an <see cref="IDamageable"/> it does not own; anything that dies before its
    /// repeat is due is dropped rather than resurrected.</para>
    /// </summary>
    public sealed class EchoRepeatScheduler
    {
        public readonly struct PendingRepeat
        {
            public readonly IDamageable Target;
            public readonly float Damage;
            public readonly DamageType DamageType;
            public readonly float DueIn;

            public PendingRepeat(IDamageable target, float damage, DamageType damageType, float dueIn)
            {
                Target = target;
                Damage = damage;
                DamageType = damageType;
                DueIn = dueIn;
            }

            public PendingRepeat WithDueIn(float dueIn) =>
                new PendingRepeat(Target, Damage, DamageType, dueIn);
        }

        readonly List<PendingRepeat> pending = new List<PendingRepeat>();
        readonly List<PendingRepeat> due = new List<PendingRepeat>();

        /// <summary>Repeats owed but not yet delivered.</summary>
        public int PendingCount => pending.Count;

        /// <summary>
        /// Owes <paramref name="target"/> a repeat of <paramref name="damage"/> in
        /// <paramref name="delaySeconds"/>. A non-positive damage or a null target schedules
        /// nothing — a repeat of nothing is not a beat, it is a bug that would print a 0.
        /// </summary>
        public void Schedule(IDamageable target, float damage, DamageType damageType, float delaySeconds)
        {
            if (target == null || damage <= 0f)
                return;

            pending.Add(new PendingRepeat(target, damage, damageType, Mathf.Max(0f, delaySeconds)));
        }

        /// <summary>
        /// Advances every owed repeat and returns those that have come due this tick. The returned
        /// list is reused between calls, so callers must consume it before ticking again.
        /// </summary>
        public IReadOnlyList<PendingRepeat> Tick(float deltaTime)
        {
            due.Clear();
            if (pending.Count == 0)
                return due;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingRepeat repeat = pending[i];

                // A body that died while the repeat was owed simply does not get hit again.
                if (repeat.Target == null || !repeat.Target.IsAlive)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                float remaining = repeat.DueIn - Mathf.Max(0f, deltaTime);
                if (remaining > 0f)
                {
                    pending[i] = repeat.WithDueIn(remaining);
                    continue;
                }

                due.Add(repeat);
                pending.RemoveAt(i);
            }

            return due;
        }

        /// <summary>Forgets everything owed. For a new room, a new run, or the player dying.</summary>
        public void Clear() => pending.Clear();
    }
}
