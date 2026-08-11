using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The reward system's hit modifiers: ability-scoped boons touch only their slot's hits,
    /// era bonuses touch only their era, and the splice arithmetic respects the biome-entry
    /// ceiling.
    /// </summary>
    public class RewardModifierTests
    {
        static HitContext Context(FakeAttack attack, FakeDamageable target) =>
            HitContext.FromAttack(attack, target, Vector3.forward, Vector3.zero);

        [Test]
        public void AbilityScopedModifierTouchesOnlyItsSlot()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new AbilityScopedModifier(AbilityId.ATK, 1.4f, 1f, null, 0f));

            var target = new FakeDamageable();

            var atkContext = Context(new FakeAttack { Ability = AbilityId.ATK, Damage = 10f }, target);
            resolver.Resolve(ref atkContext);
            Assert.That(atkContext.Damage, Is.EqualTo(14f).Within(0.001f), "+40% on the combo's hits");

            var vortexContext = Context(new FakeAttack { Ability = AbilityId.VORTEX, Damage = 10f }, target);
            resolver.Resolve(ref vortexContext);
            Assert.That(vortexContext.Damage, Is.EqualTo(10f).Within(0.001f), "other slots untouched");

            var untaggedContext = HitContext.FromAttack(
                new UntaggedAttack(), target, Vector3.forward, Vector3.zero);
            resolver.Resolve(ref untaggedContext);
            Assert.That(untaggedContext.Damage, Is.EqualTo(10f).Within(0.001f),
                "an attack with no ability tag is never matched by a scoped boon");
        }

        [Test]
        public void AbilityScopeNoneAppliesToEverything()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new AbilityScopedModifier(AbilityId.None, 2f, 1f, null, 0f));

            var target = new FakeDamageable();
            var context = Context(new FakeAttack { Ability = AbilityId.VORTEX, Damage = 10f }, target);
            resolver.Resolve(ref context);

            Assert.That(context.Damage, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void AbilityScopedStatusLandsOnlyOnScopedHits()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new AbilityScopedModifier(AbilityId.ATK, 1.2f, 1f, StatusEffect.Chilled, 2f));

            var target = new FakeDamageable();

            var vortexContext = Context(new FakeAttack { Ability = AbilityId.VORTEX }, target);
            resolver.Resolve(ref vortexContext);
            Assert.That(target.Statuses.Has(StatusEffect.Chilled), Is.False);

            var atkContext = Context(new FakeAttack { Ability = AbilityId.ATK }, target);
            resolver.Resolve(ref atkContext);
            Assert.That(target.Statuses.Has(StatusEffect.Chilled), Is.True);
            Assert.That(target.Statuses.Remaining(StatusEffect.Chilled), Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void EraDamageModifierPaysOnlyAgainstItsEra()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new EraDamageModifier(Era.Cretaceous, 1.15f));

            var dinosaur = new FakeDamageable { Era = Era.Cretaceous };
            var knight = new FakeDamageable { Era = Era.Medieval };

            var dinoContext = Context(new FakeAttack { Damage = 100f }, dinosaur);
            resolver.Resolve(ref dinoContext);
            Assert.That(dinoContext.Damage, Is.EqualTo(115f).Within(0.001f));

            var knightContext = Context(new FakeAttack { Damage = 100f }, knight);
            resolver.Resolve(ref knightContext);
            Assert.That(knightContext.Damage, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void EraModifierRunsAfterBoons()
        {
            // Order matters for legibility, not arithmetic (multiplication commutes), but the
            // declared order is part of the contract: era bonus multiplies the built-up hit.
            Assert.That(new EraDamageModifier(Era.Cretaceous, 1.15f).Order,
                Is.GreaterThan(new AbilityScopedModifier(AbilityId.ATK, 1.4f, 1f, null, 0f).Order));
        }

        [Test]
        public void SpliceHealsByDepthButNeverPastTheBiomeEntrySnapshot()
        {
            // Entered the biome at 60/100, now at 30. A 40% splice would reach 70 — clamped to 60.
            Assert.That(SpliceMath.Heal(30f, 100f, 0.4f, 60f), Is.EqualTo(60f).Within(0.001f));

            // A shallow splice below the ceiling heals in full.
            Assert.That(SpliceMath.Heal(30f, 100f, 0.2f, 60f), Is.EqualTo(50f).Within(0.001f));

            // Already at the ceiling: nothing to rewind.
            Assert.That(SpliceMath.Heal(60f, 100f, 0.4f, 60f), Is.EqualTo(60f).Within(0.001f));

            // A splice never damages, even above the snapshot.
            Assert.That(SpliceMath.Heal(80f, 100f, 0.4f, 60f), Is.EqualTo(80f).Within(0.001f));

            // The ceiling can never exceed max health.
            Assert.That(SpliceMath.Heal(90f, 100f, 1f, 500f), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void ShieldProcArmsEveryNthScopedHitAndIgnoresOtherSlots()
        {
            int armed = 0;
            var resolver = new HitResolver();
            resolver.AddModifier(new ShieldProcModifier(AbilityId.ATK, 3, () => armed++));

            var target = new FakeDamageable();

            for (int i = 0; i < 7; i++)
            {
                var context = Context(new FakeAttack { Ability = AbilityId.ATK }, target);
                resolver.Resolve(ref context);
            }

            Assert.That(armed, Is.EqualTo(2), "7 combo hits at every-3rd arm exactly twice");

            var vortexContext = Context(new FakeAttack { Ability = AbilityId.VORTEX }, target);
            resolver.Resolve(ref vortexContext);
            var atkContext = Context(new FakeAttack { Ability = AbilityId.ATK }, target);
            resolver.Resolve(ref atkContext);

            Assert.That(armed, Is.EqualTo(2), "vortex hits do not feed an ATK-scoped counter (2 of 3 so far)");
        }

        [Test]
        public void StatusConditionalBonusPaysOnlyWhileTheStatusIsLive()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new StatusConditionalModifier(AbilityId.None, StatusEffect.Burning, 1f, 1.3f));

            var target = new FakeDamageable();

            var cleanHit = Context(new FakeAttack { PoiseDamage = 10f }, target);
            resolver.Resolve(ref cleanHit);
            Assert.That(cleanHit.PoiseDamage, Is.EqualTo(10f).Within(0.001f), "no status, no bonus");

            target.Statuses.Apply(StatusEffect.Burning, 3f);
            var frayedHit = Context(new FakeAttack { PoiseDamage = 10f }, target);
            resolver.Resolve(ref frayedHit);
            Assert.That(frayedHit.PoiseDamage, Is.EqualTo(13f).Within(0.001f), "+30% armor-break while frayed");
        }

        [Test]
        public void StatusConditionalRunsAfterStatusAppliers()
        {
            // A boon that inflicts the status (Order 50) must run before the bonus reads it,
            // so the very hit that frays a clean target already profits.
            var applier = new AbilityScopedModifier(AbilityId.ATK, 1f, 1f, StatusEffect.Burning, 3f);
            var conditional = new StatusConditionalModifier(AbilityId.None, StatusEffect.Burning, 1.5f, 1f);
            Assert.That(conditional.Order, Is.GreaterThan(applier.Order));

            var resolver = new HitResolver();
            resolver.AddModifier(conditional);
            resolver.AddModifier(applier);

            var target = new FakeDamageable();
            var context = Context(new FakeAttack { Ability = AbilityId.ATK, Damage = 10f }, target);
            resolver.Resolve(ref context);
            Assert.That(context.Damage, Is.EqualTo(15f).Within(0.001f),
                "the fraying hit itself lands on an already-frayed target");
        }

        /// <summary>An attack with no ability tag at all — enemy attacks look like this.</summary>
        sealed class UntaggedAttack : IAttackDefinition
        {
            public string Id => "untagged";
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
    }
}
