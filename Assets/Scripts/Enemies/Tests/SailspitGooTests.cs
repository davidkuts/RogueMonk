using Game.Core.Rng;
using NUnit.Framework;
using UnityEngine;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// Where a lobbed glob lands (M21C). The ring exists so the shot denies ground rather than
    /// chasing the player, and the distribution has to be even or a room teaches the pattern.
    /// </summary>
    public sealed class AnnulusTargetingTests
    {
        [Test]
        public void EveryPointLandsInsideTheRing()
        {
            var stream = new XorShiftRandom(4242u);

            for (int i = 0; i < 2000; i++)
            {
                Vector3 offset = AnnulusTargeting.PickOffset(stream, 1.6f, 4.5f);

                Assert.IsTrue(
                    AnnulusTargeting.IsInAnnulus(offset, 1.6f, 4.5f),
                    $"draw {i} landed at {offset.magnitude:0.###}m, outside 1.6-4.5m");
            }
        }

        [Test]
        public void ItNeverLandsOnThePlayer()
        {
            // The inner radius is the whole reason this is area denial rather than a homing hit.
            var stream = new XorShiftRandom(99u);

            for (int i = 0; i < 1000; i++)
            {
                Vector3 offset = AnnulusTargeting.PickOffset(stream, 1.6f, 4.5f);
                Assert.GreaterOrEqual(offset.magnitude, 1.6f - 0.001f);
            }
        }

        [Test]
        public void ThePointsStayFlat()
        {
            var stream = new XorShiftRandom(7u);

            for (int i = 0; i < 200; i++)
                Assert.AreEqual(0f, AnnulusTargeting.PickOffset(stream, 1f, 3f).y, 0.0001f);
        }

        [Test]
        public void TheDistributionIsEvenAcrossTheRingRatherThanBunchedInTheMiddle()
        {
            // Interpolating the radius linearly would over-fill the inner band, because a ring's
            // area grows with its radius. Split the ring into two bands of EQUAL AREA and they
            // should receive roughly equal numbers of points.
            const float min = 1.6f, max = 4.5f;
            float midEqualArea = Mathf.Sqrt((min * min + max * max) * 0.5f);

            var stream = new XorShiftRandom(31337u);
            int inner = 0, outer = 0;

            for (int i = 0; i < 20000; i++)
            {
                float r = AnnulusTargeting.PickOffset(stream, min, max).magnitude;
                if (r < midEqualArea) inner++; else outer++;
            }

            float ratio = inner / (float)outer;
            Assert.That(ratio, Is.EqualTo(1f).Within(0.06f),
                $"equal-area bands should fill equally; got {inner} inner vs {outer} outer");
        }

        [Test]
        public void TheSameSeedThrowsTheSameGlobs()
        {
            var a = new XorShiftRandom(2024u);
            var b = new XorShiftRandom(2024u);

            for (int i = 0; i < 100; i++)
            {
                Vector3 left = AnnulusTargeting.PickOffset(a, 1.6f, 4.5f);
                Vector3 right = AnnulusTargeting.PickOffset(b, 1.6f, 4.5f);
                Assert.AreEqual(left, right, $"draw {i} diverged");
            }
        }

        [Test]
        public void ADifferentSeedThrowsDifferentGlobs()
        {
            var a = new XorShiftRandom(1u);
            var b = new XorShiftRandom(2u);

            int identical = 0;
            for (int i = 0; i < 100; i++)
            {
                if (AnnulusTargeting.PickOffset(a, 1.6f, 4.5f) == AnnulusTargeting.PickOffset(b, 1.6f, 4.5f))
                    identical++;
            }

            Assert.Less(identical, 5);
        }

        [Test]
        public void ADegenerateRingIsNotAnError()
        {
            var stream = new XorShiftRandom(5u);

            // min == max is a circle, and swapped bounds are an authoring slip rather than a crash.
            Assert.IsTrue(AnnulusTargeting.IsInAnnulus(AnnulusTargeting.PickOffset(stream, 3f, 3f), 3f, 3f));
            Assert.IsTrue(AnnulusTargeting.IsInAnnulus(AnnulusTargeting.PickOffset(stream, 4f, 2f), 2f, 4f));
            Assert.AreEqual(Vector3.zero, AnnulusTargeting.PickOffset(null, 1f, 2f));
        }
    }

    /// <summary>
    /// The goo's damage clock. The shape the acceptance criterion names is "standing in it for
    /// 3.5 seconds costs 3" — forgiving at the edges, and never front-loaded.
    /// </summary>
    public sealed class DwellDamageClockTests
    {
        const float Interval = 1f;

        static int Run(DwellDamageClock clock, float seconds, bool inside, float step = 0.05f)
        {
            int ticks = 0;
            for (float t = 0f; t < seconds - 1e-5f; t += step)
                ticks += clock.Tick(step, inside, Interval);

            return ticks;
        }

        [Test]
        public void EnteringCostsNothing()
        {
            var clock = new DwellDamageClock();

            Assert.AreEqual(0, clock.Tick(0.016f, true, Interval), "The instant of entry must be free.");
            Assert.AreEqual(0, Run(clock, 0.9f, true), "And so must the first second.");
        }

        [Test]
        public void ThreePointFiveSecondsCostsThree()
        {
            // The acceptance criterion, verbatim.
            var clock = new DwellDamageClock();

            Assert.AreEqual(3, Run(clock, 3.5f, true));
        }

        [Test]
        public void TicksLandOnWholeSeconds()
        {
            var clock = new DwellDamageClock();

            Assert.AreEqual(1, Run(clock, 1.02f, true));
            Assert.AreEqual(1, Run(clock, 1.0f, true));   // now at 2.02s
            Assert.AreEqual(0, Run(clock, 0.5f, true));   // 2.52s, nothing new
            Assert.AreEqual(1, Run(clock, 0.5f, true));   // 3.02s
        }

        [Test]
        public void LeavingResetsTheClock()
        {
            var clock = new DwellDamageClock();

            Run(clock, 0.9f, true);          // almost a tick
            clock.Tick(0.1f, false, Interval); // stepped out
            Assert.AreEqual(0f, clock.DwellSeconds, 0.0001f);

            Assert.AreEqual(0, Run(clock, 0.9f, true), "Re-entering starts the second over.");
            Assert.AreEqual(1, Run(clock, 0.2f, true));
        }

        [Test]
        public void BrushingRepeatedlyNeverAccrues()
        {
            // Six passes through the edge, none of them long enough to be standing in it.
            var clock = new DwellDamageClock();
            int ticks = 0;

            for (int i = 0; i < 6; i++)
            {
                ticks += Run(clock, 0.8f, true);
                ticks += clock.Tick(0.1f, false, Interval);
            }

            Assert.AreEqual(0, ticks, "Continuous has to mean continuous.");
        }

        [Test]
        public void ALongFrameStillPaysEverythingItPassedOver()
        {
            var clock = new DwellDamageClock();

            Assert.AreEqual(3, clock.Tick(3.2f, true, Interval), "A stalled frame must not swallow ticks.");
        }

        [Test]
        public void AZeroIntervalPaysNothingRatherThanDividingByZero()
        {
            var clock = new DwellDamageClock();

            Assert.AreEqual(0, clock.Tick(5f, true, 0f));
        }
    }
}
