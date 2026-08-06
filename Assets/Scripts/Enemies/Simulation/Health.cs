using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Engine-free hit points. Deliberately has no Heal method — DESIGN.md locks "no healing
    /// at all" during a run, and the absence is the enforcement.
    /// </summary>
    public sealed class Health
    {
        public Health(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public float Max { get; }

        public float Current { get; private set; }

        public bool IsAlive => Current > 0f;

        public float Fraction => Max <= 0f ? 0f : Mathf.Clamp01(Current / Max);

        /// <summary>Raised for every damaging hit that lands, with the amount actually taken.</summary>
        public event Action<float> Damaged;

        /// <summary>Raised once, on the hit that takes Current to zero.</summary>
        public event Action Died;

        /// <summary>Returns the damage actually applied, which is clamped by what was left.</summary>
        public float TakeDamage(float amount)
        {
            if (amount <= 0f || !IsAlive)
                return 0f;

            float applied = Mathf.Min(amount, Current);
            Current -= applied;
            Damaged?.Invoke(applied);

            if (Current <= 0f)
            {
                Current = 0f;
                Died?.Invoke();
            }

            return applied;
        }

        /// <summary>Full reset, for pooling and for test setup. Not a gameplay heal.</summary>
        public void ResetToFull() => Current = Max;
    }
}
