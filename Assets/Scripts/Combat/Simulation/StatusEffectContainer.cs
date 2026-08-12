using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>Status effects. Stagger is the only one the MVP applies.</summary>
    public enum StatusEffect
    {
        Stagger = 0,

        /// <summary>
        /// Raised while a burn DoT is running. A PRESENCE FLAG ONLY since M22B — the damage lives
        /// in <see cref="DotContainer"/>, and this exists so "bonus damage vs burning" boons and
        /// the body tint keep answering the same question.
        /// </summary>
        Burning = 1,

        Chilled = 2,
        Rooted = 3,

        /// <summary>Fray's other lane. Presence flag for a decay DoT, same shape as Burning.</summary>
        Decaying = 4,
    }

    /// <summary>
    /// Engine-free timed status container carried by every <see cref="IDamageable"/>.
    /// Re-applying a status keeps the longer of the two durations rather than stacking.
    /// </summary>
    public sealed class StatusEffectContainer
    {
        readonly Dictionary<StatusEffect, float> remaining = new Dictionary<StatusEffect, float>();
        readonly List<StatusEffect> expired = new List<StatusEffect>();

        public bool Has(StatusEffect effect) => remaining.ContainsKey(effect);

        public float Remaining(StatusEffect effect)
        {
            float value;
            return remaining.TryGetValue(effect, out value) ? value : 0f;
        }

        public int Count => remaining.Count;

        /// <summary>Applies or refreshes a status. A shorter re-application never cuts an existing one short.</summary>
        public void Apply(StatusEffect effect, float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return;

            float existing;
            remaining[effect] = remaining.TryGetValue(effect, out existing)
                ? Mathf.Max(existing, durationSeconds)
                : durationSeconds;
        }

        public void Clear(StatusEffect effect) => remaining.Remove(effect);

        public void ClearAll() => remaining.Clear();

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || remaining.Count == 0)
                return;

            expired.Clear();
            var keys = new List<StatusEffect>(remaining.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                StatusEffect effect = keys[i];
                float left = remaining[effect] - deltaTime;
                if (left <= 0f)
                    expired.Add(effect);
                else
                    remaining[effect] = left;
            }

            for (int i = 0; i < expired.Count; i++)
                remaining.Remove(expired[i]);
        }
    }
}
