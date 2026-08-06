using Game.Combat;

namespace Game.Enemies.Tests
{
    internal sealed class FakeEnemyAttack : IAttackDefinition
    {
        public string Id { get; set; } = "lunge";
        public float WindupSeconds { get; set; } = 0.45f;
        public float ActiveSeconds { get; set; } = 0.10f;
        public float RecoverySeconds { get; set; } = 0.40f;
        public bool CancellableOnRecovery { get; set; }
        public float ComboWindowSeconds { get; set; }
        public HitboxShape Hitbox { get; set; } = HitboxShape.DefaultSphere;
        public float Damage { get; set; } = 12f;
        public DamageType DamageType { get; set; } = DamageType.Physical;
        public float PoiseDamage { get; set; }
        public float Knockback { get; set; } = 3f;
        public float HitstopSeconds { get; set; } = 0.06f;
        public float AutoAimConeDegrees { get; set; } = 30f;
        public float AutoAimRangeMeters { get; set; } = 3f;
        public float AimSnapSpeedDegPerSec { get; set; } = 360f;
        public float MoveSpeedMultiplier { get; set; }
    }

    internal sealed class FakeEnemyDefinition : IEnemyDefinition
    {
        public string Id { get; set; } = "melee";
        public float MaxHealth { get; set; } = 60f;
        public StaggerTier Tier { get; set; } = StaggerTier.Staggerable;
        public float PoiseMax { get; set; } = 30f;
        public float PoiseRegenDelay { get; set; } = 1.5f;
        public float PoiseRegenRate { get; set; } = 15f;
        public float ArmorMax { get; set; } = 40f;
        public float StaggerDurationSeconds { get; set; } = 1.2f;
        public float MoveSpeed { get; set; } = 3.2f;
        public float AggroRange { get; set; } = 12f;
        public float AttackRange { get; set; } = 2.4f;
        public float AttackCooldownSeconds { get; set; } = 1.1f;
        public float LungeDistance { get; set; } = 2.2f;
        public IAttackDefinition Attack { get; set; } = new FakeEnemyAttack();

        public RangedProfile Ranged { get; set; } = new RangedProfile
        {
            PreferredMinRange = 5f,
            PreferredMaxRange = 9f,
            ProjectileSpeed = 9f,
            ProjectileLifetime = 4f,
            ProjectileRadius = 0.35f,
            KiteSpeedFraction = 0.7f,
        };
    }
}
