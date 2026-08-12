using System.Collections.Generic;
using Game.Combat;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The confetti valve. An enemy under heavy stacks must show at most one number per DoT type
    /// per second, and a second that earned only fractions must show nothing at all.
    /// </summary>
    public sealed class DotNumberAccumulatorTests
    {
        // 0.25 is exact in binary, so four of them make exactly one second and the flush lands on
        // the frame it should. A 1/60 step accumulates to 0.99999997 and would flush a frame late,
        // which is a test measuring float error rather than the valve.
        const float Frame = 0.25f;
        const float Interval = 1f;

        static int RunSeconds(DotNumberAccumulator numbers, float seconds, IDotDefinition definition, int perFrameDamage, out int flushCount)
        {
            int total = 0;
            flushCount = 0;

            for (float t = 0f; t < seconds - 0.0001f; t += Frame)
            {
                if (perFrameDamage > 0)
                    numbers.Add(definition, perFrameDamage);

                IReadOnlyList<DotNumberAccumulator.Flush> due = numbers.Tick(Frame, Interval);
                for (int i = 0; i < due.Count; i++)
                {
                    if (due[i].Definition != definition)
                        continue;

                    flushCount++;
                    total += due[i].Amount;
                }
            }

            return total;
        }

        /// <summary>The acceptance criterion: heavy stacks, at most one number per type per second.</summary>
        [Test]
        public void HeavyStacksStillShowOneNumberASecond()
        {
            var burn = new FakeDot();
            var numbers = new DotNumberAccumulator();

            int flushes;
            int total = RunSeconds(numbers, 5f, burn, perFrameDamage: 1, flushCount: out flushes);

            Assert.That(flushes, Is.EqualTo(5), "one number a second, not one per point of damage");
            Assert.That(total + numbers.Pending(burn), Is.EqualTo(20), "and every point is accounted for");
        }

        [Test]
        public void DamageIsSummedIntoOneFigureRatherThanSplit()
        {
            var burn = new FakeDot();
            var numbers = new DotNumberAccumulator();

            numbers.Add(burn, 3);
            numbers.Add(burn, 4);
            numbers.Add(burn, 5);

            IReadOnlyList<DotNumberAccumulator.Flush> due = numbers.Tick(Interval, Interval);

            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(due[0].Amount, Is.EqualTo(12));
        }

        /// <summary>
        /// "If a DoT pool's accumulated whole damage at flush time is 0, show nothing that second."
        /// A zero would tell the player the burn had stopped working.
        /// </summary>
        [Test]
        public void AnEmptySecondShowsNothing()
        {
            var burn = new FakeDot();
            var numbers = new DotNumberAccumulator();

            numbers.Add(burn, 2);
            Assert.That(numbers.Tick(Interval, Interval).Count, Is.EqualTo(1));

            // The next window earns nothing at all.
            Assert.That(numbers.Tick(Interval, Interval).Count, Is.EqualTo(0));
        }

        [Test]
        public void TwoTypesFlushSeparately()
        {
            var burn = new FakeDot { Id = "burn" };
            var decay = new FakeDot { Id = "decay", StatusFlag = StatusEffect.Decaying };
            var numbers = new DotNumberAccumulator();

            numbers.Add(burn, 4);
            numbers.Add(decay, 7);

            IReadOnlyList<DotNumberAccumulator.Flush> due = numbers.Tick(Interval, Interval);

            Assert.That(due.Count, Is.EqualTo(2), "burn and decay are separate figures, never merged");

            int burnAmount = 0, decayAmount = 0;
            for (int i = 0; i < due.Count; i++)
            {
                if (due[i].Definition == burn) burnAmount = due[i].Amount;
                if (due[i].Definition == decay) decayAmount = due[i].Amount;
            }

            Assert.That(burnAmount, Is.EqualTo(4));
            Assert.That(decayAmount, Is.EqualTo(7));
        }

        /// <summary>A hitch longer than the interval yields one number, not a burst catching up.</summary>
        [Test]
        public void ALongFrameStillFlushesOnce()
        {
            var burn = new FakeDot();
            var numbers = new DotNumberAccumulator();

            numbers.Add(burn, 9);
            IReadOnlyList<DotNumberAccumulator.Flush> due = numbers.Tick(6f, Interval);

            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(due[0].Amount, Is.EqualTo(9));
        }

        [Test]
        public void NothingIsBankedForZeroOrNegativeDamage()
        {
            var burn = new FakeDot();
            var numbers = new DotNumberAccumulator();

            numbers.Add(burn, 0);
            numbers.Add(burn, -5);
            numbers.Add(null, 5);

            Assert.That(numbers.Pending(burn), Is.EqualTo(0));
            Assert.That(numbers.Tick(Interval, Interval).Count, Is.EqualTo(0));
        }

        [Test]
        public void ClearDropsPendingDamage()
        {
            var burn = new FakeDot();
            var numbers = new DotNumberAccumulator();
            numbers.Add(burn, 7);

            numbers.Clear();

            Assert.That(numbers.Pending(burn), Is.EqualTo(0));
            Assert.That(numbers.Tick(Interval, Interval).Count, Is.EqualTo(0));
        }
    }
}
