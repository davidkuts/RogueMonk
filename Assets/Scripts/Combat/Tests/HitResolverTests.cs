using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    public class HitResolverTests
    {
        static HitContext MakeContext(FakeAttack attack, IDamageable target) =>
            HitContext.FromAttack(attack, target, Vector3.forward, Vector3.zero);

        [Test]
        public void MvpPipelineIsEmpty_AndPassesTheAttackThroughUnchanged()
        {
            var resolver = new HitResolver();
            var attack = new FakeAttack { Damage = 12f, PoiseDamage = 7f, Knockback = 3f, HitstopSeconds = 0.06f };
            var target = new FakeDamageable();
            HitContext context = MakeContext(attack, target);

            Assert.That(resolver.Modifiers, Is.Empty);
            Assert.That(resolver.Resolve(ref context), Is.True);

            Assert.That(target.Hits, Has.Count.EqualTo(1));
            Assert.That(target.LastHit.Damage, Is.EqualTo(12f));
            Assert.That(target.LastHit.PoiseDamage, Is.EqualTo(7f));
            Assert.That(target.LastHit.Knockback, Is.EqualTo(3f));
            Assert.That(target.LastHit.HitstopSeconds, Is.EqualTo(0.06f));
            Assert.That(target.LastHit.DamageType, Is.EqualTo(DamageType.Physical));
        }

        [Test]
        public void ModifiersRunInAscendingOrder()
        {
            var resolver = new HitResolver();
            var order = new List<int>();
            resolver.AddModifier(new FakeModifier(10, (ref HitContext c) => order.Add(10)));
            resolver.AddModifier(new FakeModifier(-5, (ref HitContext c) => order.Add(-5)));
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => order.Add(0)));

            var context = MakeContext(new FakeAttack(), new FakeDamageable());
            resolver.Resolve(ref context);

            Assert.That(order, Is.EqualTo(new[] { -5, 0, 10 }));
        }

        [Test]
        public void ModifiersWithEqualOrderKeepInsertionOrder()
        {
            var resolver = new HitResolver();
            var order = new List<string>();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => order.Add("first")));
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => order.Add("second")));

            var context = MakeContext(new FakeAttack(), new FakeDamageable());
            resolver.Resolve(ref context);

            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void AModifierCanChangeDamage_AndTheTargetSeesTheModifiedValue()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.Damage *= 2f));
            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack { Damage = 10f }, target);

            resolver.Resolve(ref context);

            Assert.That(target.LastHit.Damage, Is.EqualTo(20f));
        }

        [Test]
        public void AModifierCanChangeDamageType_TheBoonSeam()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.DamageType = DamageType.Fire));
            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack(), target);

            resolver.Resolve(ref context);

            Assert.That(target.LastHit.DamageType, Is.EqualTo(DamageType.Fire));
        }

        [Test]
        public void ModifiersSeeEachOthersChanges()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.Damage += 5f));
            resolver.AddModifier(new FakeModifier(1, (ref HitContext c) => c.Damage *= 2f));
            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack { Damage = 10f }, target);

            resolver.Resolve(ref context);

            Assert.That(target.LastHit.Damage, Is.EqualTo(30f), "(10 + 5) * 2");
        }

        [Test]
        public void CancellingStopsThePipelineAndTheHit()
        {
            var resolver = new HitResolver();
            var later = new FakeModifier(10, (ref HitContext c) => c.Damage = 999f);
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.Cancelled = true));
            resolver.AddModifier(later);
            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack(), target);

            Assert.That(resolver.Resolve(ref context), Is.False);
            Assert.That(target.Hits, Is.Empty);
            Assert.That(later.CallCount, Is.EqualTo(0), "later modifiers must not run after a veto");
        }

        [Test]
        public void CancelledHitsRaiseHitCancelled_NotHitApplied()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.Cancelled = true));
            int applied = 0, cancelled = 0;
            resolver.HitApplied += _ => applied++;
            resolver.HitCancelled += _ => cancelled++;

            var context = MakeContext(new FakeAttack(), new FakeDamageable());
            resolver.Resolve(ref context);

            Assert.That(applied, Is.EqualTo(0));
            Assert.That(cancelled, Is.EqualTo(1));
        }

        [Test]
        public void HitAppliedCarriesTheResolvedContext()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.HitstopSeconds = 0.2f));
            HitContext seen = default;
            resolver.HitApplied += c => seen = c;

            var context = MakeContext(new FakeAttack { HitstopSeconds = 0.06f }, new FakeDamageable());
            resolver.Resolve(ref context);

            Assert.That(seen.HitstopSeconds, Is.EqualTo(0.2f), "feedback must use the post-pipeline value");
        }

        [Test]
        public void ZeroDamageIsStillAHit()
        {
            // A modifier can null the damage without vetoing — the target still reacts.
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.Damage = 0f));
            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack(), target);

            Assert.That(resolver.Resolve(ref context), Is.True);
            Assert.That(target.Hits, Has.Count.EqualTo(1));
        }

        [Test]
        public void NegativeDamageIsClampedToZero_NoAccidentalHealing()
        {
            var resolver = new HitResolver();
            resolver.AddModifier(new FakeModifier(0, (ref HitContext c) => c.Damage = -50f));
            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack(), target);

            resolver.Resolve(ref context);

            Assert.That(target.LastHit.Damage, Is.EqualTo(0f));
        }

        [Test]
        public void DeadOrMissingTargetsAreSkipped()
        {
            var resolver = new HitResolver();
            var dead = new FakeDamageable { IsAlive = false };

            HitContext deadContext = MakeContext(new FakeAttack(), dead);
            Assert.That(resolver.Resolve(ref deadContext), Is.False);
            Assert.That(dead.Hits, Is.Empty);

            HitContext nullContext = MakeContext(new FakeAttack(), null);
            Assert.That(resolver.Resolve(ref nullContext), Is.False);
        }

        [Test]
        public void RemoveModifier_TakesItOutOfThePipeline()
        {
            var resolver = new HitResolver();
            var modifier = new FakeModifier(0, (ref HitContext c) => c.Damage = 99f);
            resolver.AddModifier(modifier);

            Assert.That(resolver.RemoveModifier(modifier), Is.True);
            Assert.That(resolver.Modifiers, Is.Empty);

            var target = new FakeDamageable();
            var context = MakeContext(new FakeAttack { Damage = 10f }, target);
            resolver.Resolve(ref context);

            Assert.That(target.LastHit.Damage, Is.EqualTo(10f));
        }

        [Test]
        public void FromAttack_NormalizesDirectionAndFlattensIt()
        {
            var attack = new FakeAttack();
            HitContext context = HitContext.FromAttack(attack, new FakeDamageable(), new Vector3(3f, 9f, 4f), Vector3.zero);

            Assert.That(context.Direction.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(context.Direction.magnitude, Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
