using NUnit.Framework;

namespace Game.Combat.Tests
{
    public class EmpoweredStrikeModifierTests
    {
        static EmpoweredStrikeModifier Armed(float window = 2.5f, float damage = 2.5f)
        {
            var modifier = new EmpoweredStrikeModifier(damage, 0.09f, 2f);
            modifier.Arm(window);
            return modifier;
        }

        static HitContext Hit(float damage = 10f) => new HitContext
        {
            Target = new FakeDamageable(),
            Damage = damage,
            Knockback = 2f,
            HitstopSeconds = 0.06f,
        };

        [Test]
        public void AnUnarmedModifierChangesNothing()
        {
            var modifier = new EmpoweredStrikeModifier(2.5f, 0.09f, 2f);
            HitContext context = Hit();

            modifier.Modify(ref context);

            Assert.That(context.Damage, Is.EqualTo(10f));
            Assert.That(context.HitstopSeconds, Is.EqualTo(0.06f).Within(1e-5f));
        }

        [Test]
        public void AnArmedModifierEmpowersDamageKnockbackAndHitstop()
        {
            EmpoweredStrikeModifier modifier = Armed(damage: 2.5f);
            HitContext context = Hit(10f);

            modifier.Modify(ref context);

            Assert.That(context.Damage, Is.EqualTo(25f).Within(1e-4f));
            Assert.That(context.Knockback, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(context.HitstopSeconds, Is.EqualTo(0.15f).Within(1e-4f));
        }

        [Test]
        public void TheChargeIsSpentByTheFirstHitOnly()
        {
            // A sweep catching three enemies should empower one of them, not all three.
            EmpoweredStrikeModifier modifier = Armed();

            HitContext first = Hit(10f);
            modifier.Modify(ref first);

            HitContext second = Hit(10f);
            modifier.Modify(ref second);

            Assert.That(first.Damage, Is.GreaterThan(10f));
            Assert.That(second.Damage, Is.EqualTo(10f), "the charge was already spent");
            Assert.That(modifier.IsArmed, Is.False);
        }

        [Test]
        public void TheChargeExpiresIfUnused()
        {
            EmpoweredStrikeModifier modifier = Armed(window: 1f);

            modifier.Tick(1.1f);

            Assert.That(modifier.IsArmed, Is.False);

            HitContext context = Hit(10f);
            modifier.Modify(ref context);
            Assert.That(context.Damage, Is.EqualTo(10f), "a dodge cannot be banked for a later room");
        }

        [Test]
        public void ResolvedReportsWhetherTheChargeLandedOrLapsed()
        {
            EmpoweredStrikeModifier spent = Armed();
            bool? spentResult = null;
            spent.Resolved += landed => spentResult = landed;
            HitContext context = Hit();
            spent.Modify(ref context);
            Assert.That(spentResult, Is.True);

            EmpoweredStrikeModifier lapsed = Armed(window: 0.5f);
            bool? lapsedResult = null;
            lapsed.Resolved += landed => lapsedResult = landed;
            lapsed.Tick(0.6f);
            Assert.That(lapsedResult, Is.False);
        }

        [Test]
        public void ResolvedFiresExactlyOnceOnExpiry()
        {
            EmpoweredStrikeModifier modifier = Armed(window: 0.5f);
            int calls = 0;
            modifier.Resolved += _ => calls++;

            modifier.Tick(0.6f);
            modifier.Tick(0.6f);
            modifier.Tick(0.6f);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ReArmingRestoresTheCharge()
        {
            EmpoweredStrikeModifier modifier = Armed();
            HitContext first = Hit();
            modifier.Modify(ref first);
            Assert.That(modifier.IsArmed, Is.False);

            modifier.Arm(2.5f);

            HitContext second = Hit(10f);
            modifier.Modify(ref second);
            Assert.That(second.Damage, Is.GreaterThan(10f));
        }

        [Test]
        public void ItRunsLateSoEarlierModifiersAreMultipliedRatherThanMultiplying()
        {
            // A boon that halves damage should halve the empowered number too, not the reverse.
            var modifier = new EmpoweredStrikeModifier(2f, 0f, 1f);
            Assert.That(modifier.Order, Is.GreaterThan(0));

            var resolver = new HitResolver();
            resolver.AddModifier(modifier);
            resolver.AddModifier(new HalveDamage());
            modifier.Arm(1f);

            HitContext context = Hit(10f);
            resolver.Resolve(ref context);

            Assert.That(context.Damage, Is.EqualTo(10f).Within(1e-4f), "10 halved to 5, then doubled to 10");
        }

        sealed class HalveDamage : IHitModifier
        {
            public int Order => 0;
            public void Modify(ref HitContext context) => context.Damage *= 0.5f;
        }

        sealed class FakeDamageable : IDamageable
        {
            public bool IsAlive => true;
            public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();
            public void ApplyHit(in HitContext context) { }
        }
    }
}
