using NUnit.Framework;

namespace Game.UI.Tests
{
    public class BossBarModelTests
    {
        static BossBarModel Bound(float drain = 0.25f, float delay = 0.4f, int phases = 2)
        {
            var model = new BossBarModel(drain, delay);
            model.Bind(phases, new[] { 0.55f });
            return model;
        }

        [Test]
        public void FillTracksHealthExactly()
        {
            BossBarModel model = Bound();

            model.Tick(0.016f, 0.73f, 0);

            Assert.That(model.Fill, Is.EqualTo(0.73f).Within(1e-5f));
        }

        [Test]
        public void ABindStartsFull()
        {
            BossBarModel model = Bound();

            Assert.That(model.Fill, Is.EqualTo(1f));
            Assert.That(model.Chip, Is.EqualTo(1f));
            Assert.That(model.PhaseIndex, Is.EqualTo(0));
            Assert.That(model.PhaseCount, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 0.55f }, model.PhaseThresholds);
        }

        [Test]
        public void TheChipHoldsBeforeItDrains()
        {
            // The hold is what makes a hit read as an amount lost rather than a new length.
            BossBarModel model = Bound(drain: 0.25f, delay: 0.4f);

            model.Tick(0.016f, 0.6f, 0);
            Assert.That(model.Chip, Is.EqualTo(1f).Within(1e-4f), "it must not start draining instantly");

            for (int i = 0; i < 20; i++)
                model.Tick(0.016f, 0.6f, 0);   // ~0.32 s, still inside the hold

            Assert.That(model.Chip, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void TheChipDrainsAtItsAuthoredRateOnceTheHoldExpires()
        {
            BossBarModel model = Bound(drain: 0.25f, delay: 0.1f);

            model.Tick(0.1f, 0.5f, 0);  // opens the hold
            model.Tick(0.1f, 0.5f, 0);  // hold expires

            float before = model.Chip;
            model.Tick(1f, 0.5f, 0);    // a full second of draining

            Assert.That(before - model.Chip, Is.EqualTo(0.25f).Within(1e-3f));
        }

        [Test]
        public void TheChipNeverFallsBelowTheFill()
        {
            BossBarModel model = Bound(drain: 10f, delay: 0f);

            for (int i = 0; i < 50; i++)
                model.Tick(0.1f, 0.4f, 0);

            Assert.That(model.Chip, Is.GreaterThanOrEqualTo(model.Fill));
            Assert.That(model.Chip, Is.EqualTo(0.4f).Within(1e-4f), "it should settle onto the fill");
        }

        [Test]
        public void PhaseJustBrokeIsTrueForExactlyOneTick()
        {
            BossBarModel model = Bound();

            model.Tick(0.016f, 0.6f, 0);
            Assert.That(model.PhaseJustBroke, Is.False);

            model.Tick(0.016f, 0.54f, 1);
            Assert.That(model.PhaseJustBroke, Is.True, "the crossing frame");

            model.Tick(0.016f, 0.53f, 1);
            Assert.That(model.PhaseJustBroke, Is.False, "and never again for the same phase");
        }

        [Test]
        public void APhaseNeverBreaksBackwards()
        {
            BossBarModel model = Bound(phases: 3);

            model.Tick(0.016f, 0.5f, 2);
            Assert.That(model.PhaseJustBroke, Is.True);

            model.Tick(0.016f, 0.5f, 1);
            Assert.That(model.PhaseJustBroke, Is.False);
        }

        [Test]
        public void ZeroAndNegativeDeltaTimeDoNotAgeTheChip()
        {
            // A paused frame is not a hit; ageing the chip there would drain the bar behind a
            // pause menu.
            BossBarModel model = Bound(drain: 10f, delay: 0f);

            model.Tick(0f, 0.3f, 0);
            Assert.That(model.Chip, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(model.Fill, Is.EqualTo(0.3f).Within(1e-4f), "the value should still track");

            model.Tick(-1f, 0.3f, 0);
            Assert.That(model.Chip, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void HealthIsClampedToZeroAndOne()
        {
            BossBarModel model = Bound();

            model.Tick(0.016f, 4f, 0);
            Assert.That(model.Fill, Is.EqualTo(1f));

            model.Tick(0.016f, -2f, 0);
            Assert.That(model.Fill, Is.EqualTo(0f));
        }

        [Test]
        public void ClearResetsEverything()
        {
            BossBarModel model = Bound();
            model.Tick(0.5f, 0.2f, 1);

            model.Clear();

            Assert.That(model.Fill, Is.EqualTo(1f));
            Assert.That(model.Chip, Is.EqualTo(1f));
            Assert.That(model.PhaseIndex, Is.EqualTo(0));
            Assert.That(model.PhaseCount, Is.EqualTo(1));
            Assert.That(model.PhaseJustBroke, Is.False);
            Assert.That(model.PhaseThresholds, Is.Empty);
        }

        [Test]
        public void RebindingAfterAFightStartsCleanAgain()
        {
            BossBarModel model = Bound();
            model.Tick(0.5f, 0.05f, 1);

            model.Bind(2, new[] { 0.55f });

            Assert.That(model.Fill, Is.EqualTo(1f));
            Assert.That(model.Chip, Is.EqualTo(1f));
            Assert.That(model.PhaseIndex, Is.EqualTo(0));
            Assert.That(model.PhaseJustBroke, Is.False, "a rebind is not a phase break");
        }

        [Test]
        public void ANullThresholdListIsTolerated()
        {
            var model = new BossBarModel(0.25f, 0.4f);

            Assert.DoesNotThrow(() => model.Bind(1, null));
            Assert.That(model.PhaseThresholds, Is.Empty);
        }

        [Test]
        public void APhaseIndexBeyondTheCountIsClamped()
        {
            BossBarModel model = Bound(phases: 2);

            model.Tick(0.016f, 0.5f, 9);

            Assert.That(model.PhaseIndex, Is.EqualTo(1));
        }
    }
}
