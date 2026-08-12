using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    public class BoonModifierTests
    {
        sealed class FakeTarget : IDamageable
        {
            public bool IsAlive => true;
            public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();
            public DotContainer Dots { get; } = new DotContainer();
            public void ApplyHit(in HitContext context) { }
            public void ApplyStagger(float seconds) { }
        }

        static HitContext Hit(FakeTarget target) => new HitContext
        {
            Target = target,
            Damage = 10f,
            PoiseDamage = 10f,
            Knockback = 2f,
            HitstopSeconds = 0.06f,
            DamageType = DamageType.Physical,
            Direction = Vector3.forward,
        };

        static BoonModifier Boon(
            DamageType type = DamageType.Physical,
            float damage = 1f, float poise = 1f, float knockback = 1f, float hitstop = 0f,
            StatusEffect? status = null, float statusSeconds = 0f) =>
            new BoonModifier(type, damage, poise, knockback, hitstop, status, statusSeconds);

        [Test]
        public void ANeutralBoonChangesNothing()
        {
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon().Modify(ref context);

            Assert.That(context.Damage, Is.EqualTo(10f));
            Assert.That(context.PoiseDamage, Is.EqualTo(10f));
            Assert.That(context.Knockback, Is.EqualTo(2f));
            Assert.That(context.DamageType, Is.EqualTo(DamageType.Physical));
        }

        [Test]
        public void MultipliersApplyToTheRightFields()
        {
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon(damage: 1.6f, poise: 3f, knockback: 3.2f, hitstop: 0.05f).Modify(ref context);

            Assert.That(context.Damage, Is.EqualTo(16f).Within(1e-4f));
            Assert.That(context.PoiseDamage, Is.EqualTo(30f).Within(1e-4f));
            Assert.That(context.Knockback, Is.EqualTo(6.4f).Within(1e-4f));
            Assert.That(context.HitstopSeconds, Is.EqualTo(0.11f).Within(1e-4f));
        }

        [Test]
        public void AnElementalBoonStampsItsDamageType()
        {
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon(DamageType.Fire).Modify(ref context);

            Assert.That(context.DamageType, Is.EqualTo(DamageType.Fire));
        }

        [Test]
        public void APhysicalBoonDoesNotBlankAnEarlierElement()
        {
            // Stacking Ember then Focus should still burn: a pure-damage boon has no element of
            // its own to claim, so it must leave the one already on the hit alone.
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon(DamageType.Fire).Modify(ref context);
            Boon(DamageType.Physical, damage: 1.6f).Modify(ref context);

            Assert.That(context.DamageType, Is.EqualTo(DamageType.Fire));
            Assert.That(context.Damage, Is.EqualTo(16f).Within(1e-4f));
        }

        [Test]
        public void AStatusBoonAppliesItToTheTarget()
        {
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon(DamageType.Fire, status: StatusEffect.Burning, statusSeconds: 3.5f).Modify(ref context);

            Assert.That(target.Statuses.Has(StatusEffect.Burning), Is.True);
            Assert.That(target.Statuses.Remaining(StatusEffect.Burning), Is.EqualTo(3.5f).Within(1e-4f));
        }

        [Test]
        public void AZeroDurationStatusIsNotApplied()
        {
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon(status: StatusEffect.Burning, statusSeconds: 0f).Modify(ref context);

            Assert.That(target.Statuses.Count, Is.EqualTo(0));
        }

        [Test]
        public void ANullTargetIsTolerated()
        {
            HitContext context = Hit(null);
            context.Target = null;

            Assert.DoesNotThrow(() => Boon(status: StatusEffect.Burning, statusSeconds: 2f).Modify(ref context));
        }

        [Test]
        public void BoonsStackMultiplicativelyThroughTheResolver()
        {
            var target = new FakeTarget();
            var resolver = new HitResolver();
            resolver.AddModifier(Boon(damage: 2f));
            resolver.AddModifier(Boon(damage: 1.5f));

            HitContext context = Hit(target);
            resolver.Resolve(ref context);

            Assert.That(context.Damage, Is.EqualTo(30f).Within(1e-4f), "10 x2 x1.5");
        }

        [Test]
        public void BoonsRunBeforeTheRiposteStyleLateModifiers()
        {
            // Order 50 sits between setup and anything with the final say, so a late modifier
            // multiplies the boosted number rather than being boosted by it.
            Assert.That(Boon().Order, Is.EqualTo(50));
        }

        [Test]
        public void NegativeMagnitudesAreClampedRatherThanHealing()
        {
            var target = new FakeTarget();
            HitContext context = Hit(target);

            Boon(damage: -5f).Modify(ref context);

            Assert.That(context.Damage, Is.EqualTo(0f), "a boon must never turn a hit into a heal");
        }
    }
}
