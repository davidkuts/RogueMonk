using Game.Combat;
using NUnit.Framework;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// The wind-up interrupt rule (human call 2026-08-11): a landed combo or riposte hit
    /// cancels a charging attack. Only the WHO/WHEN half lives here — the tier half (Immune
    /// never, intact armour never) is ApplyStagger's and already covered by PoiseSystemTests.
    /// </summary>
    public class WindupInterruptTests
    {
        [Test]
        public void ComboAndRiposteHitsInterruptAWindup()
        {
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new FakeTaggedAttack(AbilityId.ATK), windingUp: true, zoneBlocksStagger: false), Is.True);
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new FakeTaggedAttack(AbilityId.SPLIT), windingUp: true, zoneBlocksStagger: false), Is.True);
        }

        [Test]
        public void NothingInterruptsOutsideAWindup()
        {
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new FakeTaggedAttack(AbilityId.ATK), windingUp: false, zoneBlocksStagger: false), Is.False,
                "an enemy not charging anything is governed by poise, not by this rule");
        }

        [Test]
        public void IntactAmberAtTheStruckZoneShrugsItOff()
        {
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new FakeTaggedAttack(AbilityId.ATK), windingUp: true, zoneBlocksStagger: true), Is.False);
        }

        [Test]
        public void OnlyTheComboAndRiposteAsk()
        {
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new FakeTaggedAttack(AbilityId.VORTEX), windingUp: true, zoneBlocksStagger: false), Is.False,
                "the Undertow has its own arrival stagger");
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new FakeTaggedAttack(AbilityId.None), windingUp: true, zoneBlocksStagger: false), Is.False);
            Assert.That(WindupInterrupt.ShouldInterrupt(
                new UntaggedEnemyAttack(), windingUp: true, zoneBlocksStagger: false), Is.False,
                "enemy attacks, friendly fire and the echo never interrupt each other");
        }

        sealed class FakeTaggedAttack : IAttackDefinition, IAbilityTagged
        {
            public FakeTaggedAttack(AbilityId ability) { Ability = ability; }
            public AbilityId Ability { get; }
            public string Id => "fake";
            public float WindupSeconds => 0.1f;
            public float ActiveSeconds => 0.06f;
            public float RecoverySeconds => 0.18f;
            public bool CancellableOnRecovery => true;
            public float ComboWindowSeconds => 0.4f;
            public HitboxShape Hitbox => HitboxShape.DefaultSphere;
            public float Damage => 10f;
            public DamageType DamageType => DamageType.Physical;
            public float PoiseDamage => 10f;
            public float Knockback => 2f;
            public float HitstopSeconds => 0.06f;
            public float AutoAimConeDegrees => 45f;
            public float AutoAimRangeMeters => 3f;
            public float AimSnapSpeedDegPerSec => 540f;
            public float MoveSpeedMultiplier => 0f;
        }

        sealed class UntaggedEnemyAttack : IAttackDefinition
        {
            public string Id => "enemy";
            public float WindupSeconds => 0.45f;
            public float ActiveSeconds => 0.1f;
            public float RecoverySeconds => 0.4f;
            public bool CancellableOnRecovery => false;
            public float ComboWindowSeconds => 0f;
            public HitboxShape Hitbox => HitboxShape.DefaultSphere;
            public float Damage => 10f;
            public DamageType DamageType => DamageType.Physical;
            public float PoiseDamage => 0f;
            public float Knockback => 2f;
            public float HitstopSeconds => 0.06f;
            public float AutoAimConeDegrees => 0f;
            public float AutoAimRangeMeters => 0f;
            public float AimSnapSpeedDegPerSec => 0f;
            public float MoveSpeedMultiplier => 1f;
        }
    }
}
