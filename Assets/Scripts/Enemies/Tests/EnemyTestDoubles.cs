using System.Collections.Generic;
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

    internal class FakeEnemyDefinition : IEnemyDefinition
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

        // 0 by default so existing brain tests, which are about chase/attack decisions rather than
        // spawning, are not silently gated behind a grace window they never asked for.
        public float SpawnGraceSeconds { get; set; }
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

    /// <summary>A move for the multi-move trash brain. The boss-only half is not implemented.</summary>
    internal sealed class FakeEnemyMove : IEnemyMove
    {
        public string Id { get; set; } = "move";
        public IReadOnlyList<IAttackDefinition> Links { get; set; } = new IAttackDefinition[] { new FakeEnemyAttack() };
        public float LinkDelaySeconds { get; set; } = 0.2f;
        public float MinRange { get; set; }
        public float MaxRange { get; set; } = 2.4f;
        public float SelectionWeight { get; set; } = 1f;
        public float MoveCooldownSeconds { get; set; } = 2f;
        public float LungeDistance { get; set; }

        /// <summary>Builds a chain of n distinct links, so tests can tell them apart by identity.</summary>
        public static IAttackDefinition[] Chain(int count)
        {
            var links = new IAttackDefinition[count];
            for (int i = 0; i < count; i++)
                links[i] = new FakeEnemyAttack { Id = $"link{i}" };
            return links;
        }
    }

    internal sealed class FakeBossMove : IBossMove
    {
        public string Id { get; set; } = "move";
        public IReadOnlyList<IAttackDefinition> Links { get; set; } = new IAttackDefinition[] { new FakeEnemyAttack() };
        public float LinkDelaySeconds { get; set; } = 0.3f;
        public float MinRange { get; set; }
        public float MaxRange { get; set; } = 3.2f;
        public float SelectionWeight { get; set; } = 1f;
        public int UnlockedAtPhase { get; set; }
        public float MoveCooldownSeconds { get; set; } = 2f;
        public float LungeDistance { get; set; }
        public int ProjectileCount { get; set; }
        public float ProjectileSpreadDegrees { get; set; } = 24f;
        public float ProjectileLeadFraction { get; set; }
        public int HazardCount { get; set; }
        public bool UseFixedHazardPattern { get; set; }
        public float HazardScatterRadius { get; set; } = 3.5f;
        public float HazardArcDegrees { get; set; } = 360f;
        public bool IsRetaliation { get; set; }

        /// <summary>Builds a chain of n distinct links, so tests can tell them apart by identity.</summary>
        public static IAttackDefinition[] Chain(int count)
        {
            var links = new IAttackDefinition[count];
            for (int i = 0; i < count; i++)
                links[i] = new FakeEnemyAttack { Id = $"link{i}" };
            return links;
        }
    }

    internal sealed class FakeBossPhase : IBossPhase
    {
        public float HealthFractionThreshold { get; set; } = 0.55f;
        public float CooldownMultiplier { get; set; } = 0.7f;
    }

    internal sealed class FakeBossDefinition : FakeEnemyDefinition, IBossDefinition
    {
        public FakeBossDefinition()
        {
            Id = "boss";
            Tier = StaggerTier.Immune;
            MaxHealth = 600f;
            MoveSpeed = 2.6f;
            AggroRange = 30f;
            AttackCooldownSeconds = 0.9f;
            PoiseMax = 0f;
            ArmorMax = 0f;
            StaggerDurationSeconds = 0f;
        }

        public string DisplayName { get; set; } = "The Warden";
        public IReadOnlyList<IBossMove> Moves { get; set; } = new IBossMove[] { new FakeBossMove() };
        public IReadOnlyList<IBossPhase> Phases { get; set; } = new IBossPhase[0];
        public float PhaseTransitionSeconds { get; set; } = 1.4f;
        public float RepeatWeightMultiplier { get; set; } = 0.35f;

        // Off by default so the existing selection tests are not perturbed by a mechanic they
        // are not about; the retaliation tests opt in.
        public int RetaliationHitThreshold { get; set; }

        // 0 keeps the threshold fixed at the minimum, so tests that do not care about the range
        // are not perturbed by it — and no RNG draw is consumed, keeping their streams unchanged.
        public int RetaliationHitThresholdMax { get; set; }
        public float RetaliationWindowSeconds { get; set; } = 2.5f;
    }
}
