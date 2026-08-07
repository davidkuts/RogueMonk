using Game.Core.Timing;
using NUnit.Framework;

namespace Game.Core.Tests
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
}
