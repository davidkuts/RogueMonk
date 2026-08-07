using NUnit.Framework;

namespace Game.Combat.Tests
{
    public class StatusEffectContainerTests
    {
        [Test]
        public void StartsEmpty()
        {
            var statuses = new StatusEffectContainer();
            Assert.That(statuses.Count, Is.EqualTo(0));
            Assert.That(statuses.Has(StatusEffect.Stagger), Is.False);
        }

        [Test]
        public void ApplyAddsATimedStatus()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 0.5f);

            Assert.That(statuses.Has(StatusEffect.Stagger), Is.True);
            Assert.That(statuses.Remaining(StatusEffect.Stagger), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void StatusExpires()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 0.5f);

            statuses.Tick(0.49f);
            Assert.That(statuses.Has(StatusEffect.Stagger), Is.True);

            statuses.Tick(0.02f);
            Assert.That(statuses.Has(StatusEffect.Stagger), Is.False);
        }

        [Test]
        public void ReapplyingKeepsTheLongerDuration()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 1f);
            statuses.Apply(StatusEffect.Stagger, 0.2f);

            Assert.That(statuses.Remaining(StatusEffect.Stagger), Is.EqualTo(1f).Within(1e-5f),
                "a weak re-application must not cut a long stagger short");

            statuses.Apply(StatusEffect.Stagger, 2f);
            Assert.That(statuses.Remaining(StatusEffect.Stagger), Is.EqualTo(2f).Within(1e-5f));
        }

        [Test]
        public void StatusesAreIndependent()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 0.2f);
            statuses.Apply(StatusEffect.Burning, 1f);

            statuses.Tick(0.3f);

            Assert.That(statuses.Has(StatusEffect.Stagger), Is.False);
            Assert.That(statuses.Has(StatusEffect.Burning), Is.True);
        }

        [Test]
        public void ZeroDurationIsIgnored()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 0f);
            Assert.That(statuses.Has(StatusEffect.Stagger), Is.False);
        }

        [Test]
        public void ClearRemovesOne_ClearAllRemovesEverything()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 1f);
            statuses.Apply(StatusEffect.Burning, 1f);

            statuses.Clear(StatusEffect.Stagger);
            Assert.That(statuses.Has(StatusEffect.Stagger), Is.False);
            Assert.That(statuses.Has(StatusEffect.Burning), Is.True);

            statuses.ClearAll();
            Assert.That(statuses.Count, Is.EqualTo(0));
        }

        [Test]
        public void RemainingIsZeroForAnAbsentStatus()
        {
            var statuses = new StatusEffectContainer();
            Assert.That(statuses.Remaining(StatusEffect.Chilled), Is.EqualTo(0f));
        }

        [Test]
        public void TickingManyStatusesDoesNotThrow()
        {
            var statuses = new StatusEffectContainer();
            statuses.Apply(StatusEffect.Stagger, 0.1f);
            statuses.Apply(StatusEffect.Burning, 0.1f);
            statuses.Apply(StatusEffect.Chilled, 0.1f);

            Assert.DoesNotThrow(() => statuses.Tick(0.2f));
            Assert.That(statuses.Count, Is.EqualTo(0));
        }
    }
}
