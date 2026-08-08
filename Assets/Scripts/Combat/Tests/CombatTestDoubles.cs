using System;
using System.Collections.Generic;

namespace Game.Combat.Tests
{
    /// <summary>Mutable stand-in for an AttackDefinition asset so tests stay asset-free.</summary>
    internal sealed class FakeAttack : IAttackDefinition
    {
        public string Id { get; set; } = "fake";
        public float WindupSeconds { get; set; } = 0.10f;
        public float ActiveSeconds { get; set; } = 0.06f;
        public float RecoverySeconds { get; set; } = 0.18f;
        public bool CancellableOnRecovery { get; set; } = true;
        public float ComboWindowSeconds { get; set; } = 0.40f;
        public HitboxShape Hitbox { get; set; } = HitboxShape.DefaultSphere;
        public float Damage { get; set; } = 10f;
        public DamageType DamageType { get; set; } = DamageType.Physical;
        public float PoiseDamage { get; set; } = 10f;
        public float Knockback { get; set; } = 2f;
        public float HitstopSeconds { get; set; } = 0.06f;
        public float AutoAimConeDegrees { get; set; } = 45f;
        public float AutoAimRangeMeters { get; set; } = 3f;
        public float AimSnapSpeedDegPerSec { get; set; } = 540f;
        public float MoveSpeedMultiplier { get; set; }

        public float TotalSeconds => WindupSeconds + ActiveSeconds + RecoverySeconds;
    }

    /// <summary>Records every hit it receives so tests can assert on resolved values.</summary>
    internal sealed class FakeDamageable : IDamageable
    {
        public bool IsAlive { get; set; } = true;
        public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();
        public List<HitContext> Hits { get; } = new List<HitContext>();

        public HitContext LastHit => Hits[Hits.Count - 1];

        public void ApplyHit(in HitContext context) => Hits.Add(context);

        /// <summary>Total seconds of forced stagger received, so a test can assert the arrival beat.</summary>
        public float StaggerApplied { get; private set; }

        public void ApplyStagger(float seconds) => StaggerApplied += seconds;
    }

    /// <summary>Runs an arbitrary mutation, so a test can express any pipeline stage inline.</summary>
    internal sealed class FakeModifier : IHitModifier
    {
        internal delegate void ModifyDelegate(ref HitContext context);

        readonly ModifyDelegate modify;

        public FakeModifier(int order, ModifyDelegate modify)
        {
            Order = order;
            this.modify = modify;
        }

        public int Order { get; }

        public int CallCount { get; private set; }

        public void Modify(ref HitContext context)
        {
            CallCount++;
            modify(ref context);
        }
    }
}
