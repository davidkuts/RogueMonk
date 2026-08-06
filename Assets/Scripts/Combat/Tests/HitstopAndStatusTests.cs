using NUnit.Framework;

namespace Game.Combat.Tests
{
    public class HitstopControllerTests
    {
        [Test]
        public void StartsInactive()
        {
            var hitstop = new HitstopController();
            Assert.That(hitstop.IsActive, Is.False);
            Assert.That(hitstop.Remaining, Is.EqualTo(0f));
        }

        [Test]
        public void RequestActivatesForTheGivenDuration()
        {
            var hitstop = new HitstopController();
            hitstop.Request(0.06f);

            Assert.That(hitstop.IsActive, Is.True);
            Assert.That(hitstop.Remaining, Is.EqualTo(0.06f).Within(1e-5f));
        }

        [Test]
        public void ExpiresAfterItsDuration()
        {
            var hitstop = new HitstopController();
            hitstop.Request(0.06f);
            hitstop.Tick(0.05f);
            Assert.That(hitstop.IsActive, Is.True);

            hitstop.Tick(0.02f);
            Assert.That(hitstop.IsActive, Is.False);
            Assert.That(hitstop.Remaining, Is.EqualTo(0f));
        }

        [Test]
        public void OverlappingRequestsTakeTheLongest_TheyDoNotStack()
        {
            // Stacking would let a flurry of hits freeze the game.
            var hitstop = new HitstopController();
            hitstop.Request(0.10f);
            hitstop.Tick(0.02f); // 0.08 left
            hitstop.Request(0.06f);

            Assert.That(hitstop.Remaining, Is.EqualTo(0.08f).Within(1e-5f), "a shorter request must not cut it short or extend it");

            hitstop.Request(0.20f);
            Assert.That(hitstop.Remaining, Is.EqualTo(0.20f).Within(1e-5f), "a longer request wins");
        }

        [Test]
        public void ZeroOrNegativeRequestsAreIgnored()
        {
            var hitstop = new HitstopController();
            hitstop.Request(0f);
            hitstop.Request(-1f);
            Assert.That(hitstop.IsActive, Is.False);
        }

        [Test]
        public void RequestRaisesItsEvent()
        {
            var hitstop = new HitstopController();
            float seen = 0f;
            hitstop.Requested += d => seen = d;

            hitstop.Request(0.1f);

            Assert.That(seen, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void Clear_EndsHitstopImmediately()
        {
            var hitstop = new HitstopController();
            hitstop.Request(1f);
            hitstop.Clear();
            Assert.That(hitstop.IsActive, Is.False);
        }
    }

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
