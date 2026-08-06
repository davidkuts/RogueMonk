using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Everything one hit carries, built by <see cref="HitResolver"/> and mutated in turn by
    /// each <see cref="IHitModifier"/> before it reaches the target. This is the seam the
    /// elemental boon system plugs into later (DESIGN.md § Future system) — the MVP runs it
    /// with an empty modifier list.
    /// </summary>
    public struct HitContext
    {
        /// <summary>The attack that produced this hit. Read-only reference data.</summary>
        public IAttackDefinition Attack;

        public IDamageable Target;

        public float Damage;
        public DamageType DamageType;
        public float PoiseDamage;

        /// <summary>Knockback impulse magnitude. Applied along <see cref="Direction"/>.</summary>
        public float Knockback;

        /// <summary>Planar, normalized direction from attacker to target.</summary>
        public Vector3 Direction;

        /// <summary>Approximate contact point, for VFX.</summary>
        public Vector3 Point;

        public float HitstopSeconds;

        /// <summary>A modifier may set this to veto the hit entirely (a future "phase through" boon, a parry).</summary>
        public bool Cancelled;

        public static HitContext FromAttack(IAttackDefinition attack, IDamageable target, Vector3 direction, Vector3 point)
        {
            direction.y = 0f;

            return new HitContext
            {
                Attack = attack,
                Target = target,
                Damage = attack.Damage,
                DamageType = attack.DamageType,
                PoiseDamage = attack.PoiseDamage,
                Knockback = attack.Knockback,
                Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward,
                Point = point,
                HitstopSeconds = attack.HitstopSeconds,
                Cancelled = false,
            };
        }
    }
}
