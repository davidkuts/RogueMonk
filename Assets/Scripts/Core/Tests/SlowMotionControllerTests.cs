using Game.Core.Timing;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class SlowMotionControllerTests
    {
        [Test]
        public void StartsInactiveAtFullSpeed()
        {
            var slow = new SlowMotionController();

            Assert.That(slow.IsActive, Is.False);
            Assert.That(slow.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void ARequestSlowsTimeForItsDuration()
        {
            var slow = new SlowMotionController();
            slow.Request(0.6f, 0.35f);

            Assert.That(slow.IsActive, Is.True);
            Assert.That(slow.Scale, Is.EqualTo(0.35f).Within(1e-4f));

            slow.Tick(0.3f);
            Assert.That(slow.IsActive, Is.True, "half way through");

            slow.Tick(0.35f);
            Assert.That(slow.IsActive, Is.False);
            Assert.That(slow.Scale, Is.EqualTo(1f), "and full speed returns");
        }

        [Test]
        public void OverlappingRequestsTakeTheStrongerSlowAndNeverCompound()
        {
            // Two perfect dodges in quick succession should read as one clean moment of focus, not
            // multiply into a crawl the player cannot act out of.
            var slow = new SlowMotionController();

            slow.Request(0.6f, 0.5f);
            slow.Request(0.6f, 0.25f);
            Assert.That(slow.Scale, Is.EqualTo(0.25f).Within(1e-4f), "the stronger slow wins");

            slow.Request(0.6f, 0.8f);
            Assert.That(slow.Scale, Is.EqualTo(0.25f).Within(1e-4f), "a weaker request must not undo it");
        }

        [Test]
        public void OverlappingRequestsTakeTheLongerRemainingTime()
        {
            var slow = new SlowMotionController();

            slow.Request(0.2f, 0.5f);
            slow.Request(1f, 0.5f);
            slow.Tick(0.5f);

            Assert.That(slow.IsActive, Is.True, "the longer request should still be running");
        }

        [Test]
        public void AShorterRequestDoesNotCutAnActiveOneShort()
        {
            var slow = new SlowMotionController();

            slow.Request(1f, 0.5f);
            slow.Request(0.1f, 0.5f);
            slow.Tick(0.5f);

            Assert.That(slow.IsActive, Is.True);
        }

        [Test]
        public void ScaleIsClampedToSomethingPlayable()
        {
            var slow = new SlowMotionController();

            slow.Request(1f, 0f);
            Assert.That(slow.Scale, Is.GreaterThan(0f), "a zero scale would be a freeze, not a slow");

            slow.Clear();
            slow.Request(1f, 5f);
            Assert.That(slow.Scale, Is.LessThanOrEqualTo(1f), "it slows time; it never speeds it up");
        }

        [Test]
        public void ZeroAndNegativeRequestsAreIgnored()
        {
            var slow = new SlowMotionController();

            slow.Request(0f, 0.3f);
            slow.Request(-1f, 0.3f);

            Assert.That(slow.IsActive, Is.False);
            Assert.That(slow.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void ClearRestoresFullSpeedImmediately()
        {
            var slow = new SlowMotionController();
            slow.Request(5f, 0.2f);

            slow.Clear();

            Assert.That(slow.IsActive, Is.False);
            Assert.That(slow.Scale, Is.EqualTo(1f));
        }

        [Test]
        public void ProgressRunsFromZeroToOne()
        {
            var slow = new SlowMotionController();
            slow.Request(1f, 0.5f);

            Assert.That(slow.Progress, Is.EqualTo(0f).Within(1e-4f));
            slow.Tick(0.5f);
            Assert.That(slow.Progress, Is.EqualTo(0.5f).Within(1e-3f));
            slow.Tick(0.5f);
            Assert.That(slow.Progress, Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
