using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// Denny's lane: hits that happen again, later, weaker. The cadence counting and the
    /// dead-target handling are the parts that would fail silently in play.
    /// </summary>
    public class EchoRepeatTests
    {
        sealed class FakeTarget : IDamageable, IEchoRepeatTarget
        {
            public bool IsAlive { get; set; } = true;
            public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();
            public float TakenFromEchoes { get; private set; }
            public int EchoCount { get; private set; }

            public void ApplyHit(in HitContext context) { }
            public void ApplyStagger(float seconds) { }

            public void ApplyEchoRepeat(float damage, DamageType damageType)
            {
                TakenFromEchoes += damage;
                EchoCount++;
            }
        }

        static int Deliver(EchoRepeatScheduler scheduler, float deltaTime)
        {
            IReadOnlyList<EchoRepeatScheduler.PendingRepeat> due = scheduler.Tick(deltaTime);
            for (int i = 0; i < due.Count; i++)
            {
                if (due[i].Target is IEchoRepeatTarget echoable)
                    echoable.ApplyEchoRepeat(due[i].Damage, due[i].DamageType);
            }

            return due.Count;
        }

        [Test]
        public void ARepeatArrivesOnlyAfterItsDelay()
        {
            var scheduler = new EchoRepeatScheduler();
            var target = new FakeTarget();

            scheduler.Schedule(target, 10f, DamageType.Physical, 0.8f);

            Assert.That(Deliver(scheduler, 0.5f), Is.Zero, "not due yet");
            Assert.That(target.EchoCount, Is.Zero);

            Assert.That(Deliver(scheduler, 0.4f), Is.EqualTo(1), "due now");
            Assert.That(target.TakenFromEchoes, Is.EqualTo(10f).Within(0.001f));
            Assert.That(scheduler.PendingCount, Is.Zero, "and is not owed twice");
        }

        [Test]
        public void ADeadTargetIsNotEchoed()
        {
            var scheduler = new EchoRepeatScheduler();
            var target = new FakeTarget();
            scheduler.Schedule(target, 10f, DamageType.Physical, 0.5f);

            target.IsAlive = false;

            Assert.That(Deliver(scheduler, 1f), Is.Zero, "a body that died before its repeat came due stays dead");
            Assert.That(scheduler.PendingCount, Is.Zero, "and the debt is dropped rather than held forever");
        }

        [Test]
        public void NothingIsScheduledForNothing()
        {
            var scheduler = new EchoRepeatScheduler();

            scheduler.Schedule(null, 10f, DamageType.Physical, 0.5f);
            scheduler.Schedule(new FakeTarget(), 0f, DamageType.Physical, 0.5f);

            Assert.That(scheduler.PendingCount, Is.Zero,
                "a repeat of zero damage would print a 0 over an enemy that was never echoed");
        }

        [Test]
        public void SeveralRepeatsCoexistAndComeDueIndependently()
        {
            var scheduler = new EchoRepeatScheduler();
            var target = new FakeTarget();

            scheduler.Schedule(target, 5f, DamageType.Physical, 0.2f);
            scheduler.Schedule(target, 7f, DamageType.Physical, 0.9f);

            Deliver(scheduler, 0.3f);
            Assert.That(target.EchoCount, Is.EqualTo(1));
            Assert.That(target.TakenFromEchoes, Is.EqualTo(5f).Within(0.001f));

            Deliver(scheduler, 0.7f);
            Assert.That(target.EchoCount, Is.EqualTo(2));
            Assert.That(target.TakenFromEchoes, Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void TheModifierRepeatsOnItsCadenceAndScalesWhatLanded()
        {
            // Second Take: every 3rd ATK hit repeats at 40% of what actually landed.
            var scheduler = new EchoRepeatScheduler();
            var target = new FakeTarget();
            var modifier = new EchoRepeatModifier(
                AbilityId.ATK, anyAbility: false, fraction: 0.4f, delaySeconds: 0.8f,
                everyNHits: 3, scheduler);

            for (int i = 0; i < 3; i++)
            {
                var context = new HitContext
                {
                    Attack = new FakeAttack { Ability = AbilityId.ATK },
                    Target = target,
                    Damage = 25f,
                    DamageType = DamageType.Physical,
                };
                modifier.Modify(ref context);
            }

            Assert.That(scheduler.PendingCount, Is.EqualTo(1), "one repeat per three hits, not three");

            Deliver(scheduler, 1f);
            Assert.That(target.TakenFromEchoes, Is.EqualTo(10f).Within(0.001f), "40% of the 25 that landed");
        }

        [Test]
        public void TheModifierIgnoresOtherAbilitySlots()
        {
            var scheduler = new EchoRepeatScheduler();
            var modifier = new EchoRepeatModifier(
                AbilityId.SPLIT, anyAbility: false, fraction: 0.6f, delaySeconds: 3f,
                everyNHits: 1, scheduler);

            var context = new HitContext
            {
                Attack = new FakeAttack { Ability = AbilityId.ATK },
                Target = new FakeTarget(),
                Damage = 25f,
            };
            modifier.Modify(ref context);

            Assert.That(scheduler.PendingCount, Is.Zero, "a boon on the Riposte must not echo a punch");
        }

        [Test]
        public void StandingWaveCountsEveryAbility()
        {
            // The PASSIVE lane: "every 4th instance of ANY damage".
            var scheduler = new EchoRepeatScheduler();
            var target = new FakeTarget();
            var modifier = new EchoRepeatModifier(
                AbilityId.None, anyAbility: true, fraction: 0.3f, delaySeconds: 0.5f,
                everyNHits: 4, scheduler);

            AbilityId[] mixed = { AbilityId.ATK, AbilityId.VORTEX, AbilityId.SPLIT, AbilityId.ATK };
            foreach (AbilityId ability in mixed)
            {
                var context = new HitContext
                {
                    Attack = new FakeAttack { Ability = ability },
                    Target = target,
                    Damage = 10f,
                };
                modifier.Modify(ref context);
            }

            Assert.That(scheduler.PendingCount, Is.EqualTo(1),
                "four hits across four different slots still add up to one repeat");
        }
    }
}
