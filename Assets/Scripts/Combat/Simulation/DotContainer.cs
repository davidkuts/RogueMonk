using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Every damage-over-time instance riding one target, and the fractional damage they have
    /// accrued between whole points.
    ///
    /// <para><b>There is no refresh path in this type at all.</b> <see cref="Apply"/> only ever
    /// adds, so "reapplication never resets an existing instance" is a property of the shape rather
    /// than a rule a future caller can forget. One Undertow cast carrying a burn boon resolves
    /// three separate hits, so it lands three separate instances, each on its own clock and each
    /// chipping in parallel — N stacks chip N times as fast.</para>
    ///
    /// <para><b>Accrue, then floor.</b> Damage is banked as a fraction per type and only whole
    /// points ever leave: a health bar that moves by 0.37 is a health bar that appears not to move,
    /// and a number reading "0" is worse than no number. The fractional remainder survives the
    /// instance that earned it, so a burn reapplied a moment later carries on from where it stopped
    /// instead of losing part of a point every time it lapses.</para>
    ///
    /// <para><b>Earned damage is recomputed, never accumulated.</b> Summing <c>dps × deltaTime</c>
    /// across 240 frames lands a hair under the authored total as often as a hair over, and one
    /// flooring later that is a Rare burn quietly dealing 5 instead of 6 — an off-by-one in the
    /// exact number the rarity table is expressed in. Live instances report their progress as a
    /// fraction of their own duration, and an instance that ends contributes its total <em>exactly
    /// once, exactly whole</em>. So the lifetime figure is always the authored one, whatever the
    /// frame rate did.</para>
    ///
    /// <para>Only the timing of an individual point is subject to the frame clock: a point due at
    /// the two-second mark may land a frame either side of it. That is invisible, and the total is
    /// not negotiable.</para>
    /// </summary>
    public sealed class DotContainer
    {
        struct Instance
        {
            public IDotDefinition Definition;
            public float Duration;
            public float TotalDamage;
            public float Remaining;

            /// <summary>Damage earned so far, computed from progress rather than summed.</summary>
            public float Earned;
        }

        readonly List<Instance> instances = new List<Instance>();

        /// <summary>Damage from instances that have finished. Added once, whole, per instance.</summary>
        readonly Dictionary<IDotDefinition, float> retired = new Dictionary<IDotDefinition, float>();

        /// <summary>Whole damage already handed to the host, per type. The rest is the carried fraction.</summary>
        readonly Dictionary<IDotDefinition, int> released = new Dictionary<IDotDefinition, int>();

        /// <summary>Whole damage that came due on the last <see cref="Tick"/>, per type.</summary>
        readonly Dictionary<IDotDefinition, int> due = new Dictionary<IDotDefinition, int>();

        /// <summary>Scratch for the live half of each type's total. Reused, never reallocated.</summary>
        readonly Dictionary<IDotDefinition, float> liveEarned = new Dictionary<IDotDefinition, float>();

        /// <summary>Every type this target has ever carried, so the host can iterate without allocating.</summary>
        readonly List<IDotDefinition> types = new List<IDotDefinition>();

        /// <summary>Every type seen on this target, live or spent.</summary>
        public IReadOnlyList<IDotDefinition> Types => types;

        /// <summary>Live instances across every type.</summary>
        public int TotalStacks => instances.Count;

        /// <summary>
        /// Adds one instance. Always a NEW instance — never a refresh, never a reset.
        /// Returns false when the type is capped, the damage is nothing, or the definition is
        /// missing, so a caller or a test can see the refusal rather than infer it.
        /// </summary>
        public bool Apply(IDotDefinition definition, float totalDamage)
        {
            if (definition == null || totalDamage <= 0f || definition.DurationSeconds <= 0f)
                return false;

            if (definition.MaxStacks > 0 && StackCount(definition) >= definition.MaxStacks)
                return false;

            if (!types.Contains(definition))
                types.Add(definition);

            instances.Add(new Instance
            {
                Definition = definition,
                Duration = definition.DurationSeconds,
                TotalDamage = totalDamage,
                Remaining = definition.DurationSeconds,
                Earned = 0f,
            });

            return true;
        }

        /// <summary>
        /// Advances every instance and floors each type's earned total into the whole damage the
        /// host should apply this frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            for (int i = 0; i < types.Count; i++)
                due[types[i]] = 0;

            if (deltaTime <= 0f || instances.Count == 0)
                return;

            for (int i = instances.Count - 1; i >= 0; i--)
            {
                Instance instance = instances[i];
                instance.Remaining -= deltaTime;

                if (instance.Remaining <= 0f)
                {
                    // A finished instance contributes its authored total exactly once, whole. This
                    // is what makes "an Epic burn deals 8" true rather than approximately true.
                    float banked;
                    retired.TryGetValue(instance.Definition, out banked);
                    retired[instance.Definition] = banked + instance.TotalDamage;

                    instances.RemoveAt(i);
                    continue;
                }

                instance.Earned = instance.TotalDamage * Mathf.Clamp01(1f - instance.Remaining / instance.Duration);
                instances[i] = instance;
            }

            liveEarned.Clear();
            for (int i = 0; i < instances.Count; i++)
            {
                float banked;
                liveEarned.TryGetValue(instances[i].Definition, out banked);
                liveEarned[instances[i].Definition] = banked + instances[i].Earned;
            }

            for (int i = 0; i < types.Count; i++)
            {
                IDotDefinition definition = types[i];

                float retiredDamage, live;
                retired.TryGetValue(definition, out retiredDamage);
                liveEarned.TryGetValue(definition, out live);

                int whole = Mathf.FloorToInt(retiredDamage + live);

                int paid;
                released.TryGetValue(definition, out paid);

                int newly = whole - paid;
                if (newly <= 0)
                    continue;

                released[definition] = whole;
                due[definition] = newly;
            }
        }

        /// <summary>Whole damage of <paramref name="definition"/> that came due on the last tick.</summary>
        public int DueWhole(IDotDefinition definition)
        {
            int value;
            return definition != null && due.TryGetValue(definition, out value) ? value : 0;
        }

        public int StackCount(IDotDefinition definition)
        {
            if (definition == null)
                return 0;

            int count = 0;
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].Definition == definition)
                    count++;
            }

            return count;
        }

        /// <summary>True while at least one instance of this type is still running.</summary>
        public bool Has(IDotDefinition definition) => StackCount(definition) > 0;

        /// <summary>
        /// Seconds left on the longest-lived instance of this type, or zero when none are running.
        /// The host uses this to hold the status flag in step with the stack without the flag ever
        /// becoming a second source of truth for when a DoT ends.
        /// </summary>
        public float LongestRemaining(IDotDefinition definition)
        {
            float longest = 0f;
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].Definition == definition && instances[i].Remaining > longest)
                    longest = instances[i].Remaining;
            }

            return longest;
        }

        /// <summary>Drops every instance and every banked fraction. For a body being reused.</summary>
        public void Clear()
        {
            instances.Clear();
            retired.Clear();
            released.Clear();
            due.Clear();
            liveEarned.Clear();
            types.Clear();
        }
    }
}
