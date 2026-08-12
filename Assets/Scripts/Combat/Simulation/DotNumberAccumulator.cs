using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Collects DoT damage per type and lets it out as one number per interval.
    ///
    /// <para>The confetti valve. A DoT pays out whenever a whole point accrues, which for an enemy
    /// carrying nine burn stacks is several times a second — and a body throwing a stream of small
    /// figures reads as noise, not as information. Worse, it drowns the numbers that say the player
    /// actually landed something. One figure per second per type says the same thing and can
    /// be read.</para>
    ///
    /// <para>Per TYPE, not per target-and-type-combined: burn and decay are separate mechanics with
    /// separate colours, and merging their figures would tell the player a number they cannot act
    /// on. The host owns one of these, so "per enemy" falls out of where it lives.</para>
    /// </summary>
    public sealed class DotNumberAccumulator
    {
        public readonly struct Flush
        {
            public readonly IDotDefinition Definition;
            public readonly int Amount;

            public Flush(IDotDefinition definition, int amount)
            {
                Definition = definition;
                Amount = amount;
            }
        }

        readonly Dictionary<IDotDefinition, int> pending = new Dictionary<IDotDefinition, int>();
        readonly Dictionary<IDotDefinition, float> elapsed = new Dictionary<IDotDefinition, float>();
        readonly List<IDotDefinition> types = new List<IDotDefinition>();
        readonly List<Flush> flushed = new List<Flush>();

        /// <summary>Damage banked for the next flush, for tests and the debug overlay.</summary>
        public int Pending(IDotDefinition definition)
        {
            int value;
            return definition != null && pending.TryGetValue(definition, out value) ? value : 0;
        }

        public void Add(IDotDefinition definition, int amount)
        {
            if (definition == null || amount <= 0)
                return;

            if (!types.Contains(definition))
            {
                types.Add(definition);
                elapsed[definition] = 0f;
            }

            int banked;
            pending.TryGetValue(definition, out banked);
            pending[definition] = banked + amount;
        }

        /// <summary>
        /// Advances every type's clock and returns whichever came due with something to show. The
        /// returned list is reused, so read it before the next call.
        ///
        /// <para>A window that closes with nothing banked emits nothing and simply starts the next
        /// one — a DoT whose damage is still all fractional must show no figure at all rather than
        /// a zero.</para>
        /// </summary>
        public IReadOnlyList<Flush> Tick(float deltaTime, float intervalSeconds)
        {
            flushed.Clear();

            if (deltaTime <= 0f || intervalSeconds <= 0f)
                return flushed;

            for (int i = 0; i < types.Count; i++)
            {
                IDotDefinition definition = types[i];

                float clock = elapsed[definition] + deltaTime;
                if (clock < intervalSeconds)
                {
                    elapsed[definition] = clock;
                    continue;
                }

                // Reset rather than subtract: a frame longer than the interval should still yield
                // one number, not a burst of them catching up.
                elapsed[definition] = 0f;

                int banked;
                if (!pending.TryGetValue(definition, out banked) || banked <= 0)
                    continue;

                pending[definition] = 0;
                flushed.Add(new Flush(definition, banked));
            }

            return flushed;
        }

        public void Clear()
        {
            pending.Clear();
            elapsed.Clear();
            types.Clear();
            flushed.Clear();
        }
    }
}
