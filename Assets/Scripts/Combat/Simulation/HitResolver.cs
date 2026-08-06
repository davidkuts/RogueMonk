using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Engine-free hit resolution. Builds a <see cref="HitContext"/> from an attack, runs it
    /// through the ordered modifier pipeline, then hands the result to the target. Every hit
    /// in the game goes through here so boons only ever need to add modifiers.
    /// </summary>
    public sealed class HitResolver
    {
        readonly List<IHitModifier> modifiers = new List<IHitModifier>();

        public IReadOnlyList<IHitModifier> Modifiers => modifiers;

        /// <summary>Raised for every hit that survives the pipeline — the hook for hitstop, shake, SFX.</summary>
        public event Action<HitContext> HitApplied;

        /// <summary>Raised when a modifier vetoed the hit. Useful for "immune" feedback later.</summary>
        public event Action<HitContext> HitCancelled;

        /// <summary>Inserts a modifier, keeping the list sorted by ascending Order (stable for ties).</summary>
        public void AddModifier(IHitModifier modifier)
        {
            if (modifier == null)
                throw new ArgumentNullException(nameof(modifier));

            int insertAt = modifiers.Count;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Order > modifier.Order)
                {
                    insertAt = i;
                    break;
                }
            }

            modifiers.Insert(insertAt, modifier);
        }

        public bool RemoveModifier(IHitModifier modifier) => modifiers.Remove(modifier);

        public void ClearModifiers() => modifiers.Clear();

        /// <summary>
        /// Resolves one hit. Returns true when the target actually took it. The caller keeps
        /// the mutated context (via <paramref name="context"/>) so it can read the final
        /// hitstop and knockback the pipeline decided on.
        /// </summary>
        public bool Resolve(ref HitContext context)
        {
            if (context.Target == null || !context.Target.IsAlive)
                return false;

            for (int i = 0; i < modifiers.Count; i++)
            {
                modifiers[i].Modify(ref context);
                if (context.Cancelled)
                {
                    HitCancelled?.Invoke(context);
                    return false;
                }
            }

            // Damage can be driven to zero by a modifier without the hit being cancelled —
            // that is still a hit, and still deserves feedback.
            context.Damage = Mathf.Max(0f, context.Damage);
            context.PoiseDamage = Mathf.Max(0f, context.PoiseDamage);

            context.Target.ApplyHit(in context);
            HitApplied?.Invoke(context);
            return true;
        }
    }
}
