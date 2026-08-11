using NUnit.Framework;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// The riposte guard the elites wear (human call 2026-08-09). Only the gate rule lives here:
    /// what a refusal then does to poise, knockback and hit feedback stays in EnemyActor, which
    /// deliberately keeps all of those landing while the guard is up.
    /// </summary>
    public class DamageGateTests
    {
        static readonly FakeEnemyAttack Key = new FakeEnemyAttack { Id = "riposte" };
        static readonly FakeEnemyAttack Ordinary = new FakeEnemyAttack { Id = "punch1" };

        [Test]
        public void AnUngatedEnemyIsNeverGuarded()
        {
            DamageGate.Verdict verdict = DamageGate.Resolve(null, alreadyBroken: false, Ordinary);

            Assert.That(verdict.Guarded, Is.False);
            Assert.That(verdict.Breaking, Is.False);
            Assert.That(DamageGate.IsUp(null, alreadyBroken: false), Is.False,
                "no key means no gate — every ordinary enemy takes damage normally");
        }

        [Test]
        public void AnOrdinaryHitIsRefusedWhileTheGuardStands()
        {
            DamageGate.Verdict verdict = DamageGate.Resolve(Key, alreadyBroken: false, Ordinary);

            Assert.That(verdict.Guarded, Is.True);
            Assert.That(verdict.Breaking, Is.False);
        }

        [Test]
        public void TheKeyAttackBreaksTheGuardAndIsNotItselfRefused()
        {
            DamageGate.Verdict verdict = DamageGate.Resolve(Key, alreadyBroken: false, Key);

            Assert.That(verdict.Breaking, Is.True);
            Assert.That(verdict.Guarded, Is.False,
                "the breaking hit deals its own damage — it is a guard-break, not a free swing");
        }

        [Test]
        public void TheGuardNeverReArms()
        {
            // A lesson taught once per body, not a shield that regrows mid-fight.
            Assert.That(DamageGate.IsUp(Key, alreadyBroken: true), Is.False);

            DamageGate.Verdict ordinary = DamageGate.Resolve(Key, alreadyBroken: true, Ordinary);
            Assert.That(ordinary.Guarded, Is.False, "ordinary attacks damage it normally after the break");

            DamageGate.Verdict again = DamageGate.Resolve(Key, alreadyBroken: true, Key);
            Assert.That(again.Breaking, Is.False, "there is nothing left to break");
            Assert.That(again.Guarded, Is.False);
        }

        [Test]
        public void MatchingIsByIdSoTheGateStaysGenericData()
        {
            // The gate names an attack rather than the Riposte specifically, so a different asset
            // carrying the same id opens it — that is what makes onlyDamagedBy data and not a
            // riposte special case.
            var sameIdDifferentInstance = new FakeEnemyAttack { Id = "riposte", Damage = 999f };

            Assert.That(DamageGate.Resolve(Key, alreadyBroken: false, sameIdDifferentInstance).Breaking, Is.True);
        }

        [Test]
        public void AHitWithNoAttackBehindItIsRefused()
        {
            // Burn ticks and anything else arriving without an attack must not slip past a guard
            // that promises the health bar does not move before the counter lands.
            DamageGate.Verdict verdict = DamageGate.Resolve(Key, alreadyBroken: false, null);

            Assert.That(verdict.Guarded, Is.True);
            Assert.That(verdict.Breaking, Is.False);
        }
    }
}
