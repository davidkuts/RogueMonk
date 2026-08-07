using System.Collections.Generic;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class XorShiftRandomTests
    {
        [Test]
        public void SameSeedProducesTheSameStream()
        {
            var a = new XorShiftRandom(4242u);
            var b = new XorShiftRandom(4242u);

            for (int i = 0; i < 500; i++)
                Assert.That(b.NextFloat(), Is.EqualTo(a.NextFloat()), $"draw {i}");
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var a = new XorShiftRandom(1u);
            var b = new XorShiftRandom(2u);

            int identical = 0;
            for (int i = 0; i < 200; i++)
            {
                if (a.NextFloat() == b.NextFloat())
                    identical++;
            }

            Assert.That(identical, Is.LessThan(5));
        }

        [Test]
        public void NearbySeedsDoNotProduceSimilarStreams()
        {
            // Without state mixing, seeds 1 and 2 would start almost identically.
            var a = new XorShiftRandom(1u);
            var b = new XorShiftRandom(2u);

            Assert.That(b.NextFloat(), Is.Not.EqualTo(a.NextFloat()).Within(1e-6f));
        }

        [Test]
        public void SeedZeroIsRemapped_AnAllZeroStateWouldLockTheGenerator()
        {
            var random = new XorShiftRandom(0u);
            var values = new HashSet<float>();
            for (int i = 0; i < 50; i++)
                values.Add(random.NextFloat());

            Assert.That(values.Count, Is.GreaterThan(40), "the stream must not be stuck");
        }

        [Test]
        public void NextFloatStaysInRange()
        {
            var random = new XorShiftRandom(7u);
            for (int i = 0; i < 5000; i++)
            {
                float value = random.NextFloat();
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void NextIntRespectsBounds()
        {
            var random = new XorShiftRandom(11u);
            for (int i = 0; i < 5000; i++)
            {
                int value = random.NextInt(3, 8);
                Assert.That(value, Is.InRange(3, 7));
            }
        }

        [Test]
        public void NextIntCoversEveryValueInRange()
        {
            var random = new XorShiftRandom(13u);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
                seen.Add(random.NextInt(0, 6));

            Assert.That(seen.Count, Is.EqualTo(6), "an off-by-one would silently drop an endpoint");
        }

        [Test]
        public void DegenerateIntRangeReturnsTheMinimum()
        {
            var random = new XorShiftRandom(5u);
            Assert.That(random.NextInt(4, 4), Is.EqualTo(4));
            Assert.That(random.NextInt(9, 2), Is.EqualTo(9));
        }

        [Test]
        public void FloatRangeRespectsBounds()
        {
            var random = new XorShiftRandom(17u);
            for (int i = 0; i < 2000; i++)
            {
                float value = random.NextFloat(-2f, 5f);
                Assert.That(value, Is.GreaterThanOrEqualTo(-2f).And.LessThan(5f));
            }
        }

        [Test]
        public void BoolsAreRoughlyBalanced()
        {
            var random = new XorShiftRandom(23u);
            int trues = 0;
            for (int i = 0; i < 10000; i++)
            {
                if (random.NextBool())
                    trues++;
            }

            Assert.That(trues, Is.InRange(4500, 5500));
        }

        [Test]
        public void SeedIsExposedForLogging()
        {
            Assert.That(new XorShiftRandom(999u).Seed, Is.EqualTo(999u));
        }
    }

    public class RandomExtensionsTests
    {
        [Test]
        public void ShuffleKeepsEveryElement()
        {
            var random = new XorShiftRandom(31u);
            var items = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            random.Shuffle(items);

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, items);
        }

        [Test]
        public void ShuffleIsDeterministicPerSeed()
        {
            var a = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            var b = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

            new XorShiftRandom(41u).Shuffle(a);
            new XorShiftRandom(41u).Shuffle(b);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void ShuffleActuallyReorders()
        {
            var random = new XorShiftRandom(43u);
            var items = new List<int>();
            for (int i = 0; i < 30; i++)
                items.Add(i);

            var original = new List<int>(items);
            random.Shuffle(items);

            CollectionAssert.AreNotEqual(original, items);
        }

        [Test]
        public void PickWeightedNeverReturnsAZeroWeightIndex()
        {
            var random = new XorShiftRandom(47u);
            var weights = new List<float> { 0f, 5f, 0f, 3f };

            for (int i = 0; i < 2000; i++)
            {
                int index = random.PickWeighted(weights);
                Assert.That(index == 1 || index == 3, Is.True, $"picked zero-weight index {index}");
            }
        }

        [Test]
        public void PickWeightedFollowsTheWeights()
        {
            var random = new XorShiftRandom(53u);
            var weights = new List<float> { 9f, 1f };
            int zeros = 0;

            for (int i = 0; i < 10000; i++)
            {
                if (random.PickWeighted(weights) == 0)
                    zeros++;
            }

            Assert.That(zeros, Is.InRange(8500, 9500));
        }

        [Test]
        public void PickWeightedReturnsMinusOneWhenNothingIsSelectable()
        {
            var random = new XorShiftRandom(59u);
            Assert.That(random.PickWeighted(new List<float> { 0f, 0f }), Is.EqualTo(-1));
            Assert.That(random.PickWeighted(new List<float>()), Is.EqualTo(-1));
            Assert.That(random.PickWeighted(null), Is.EqualTo(-1));
        }
    }

    /// <summary>
    /// Covers the derived-stream contract. A subsystem whose draw count depends on how the run
    /// is played (boss move selection) must not consume from the run stream directly, or the
    /// seed stops reproducing the run.
    /// </summary>
    public class RunContextStreamTests
    {
        [Test]
        public void DerivedStreamsAreDeterministicForASeedAndCallOrder()
        {
            var a = new RunContext(8080u);
            var b = new RunContext(8080u);

            for (int i = 0; i < 5; i++)
            {
                IRandomSource left = a.DeriveStream();
                IRandomSource right = b.DeriveStream();

                Assert.That(right.Seed, Is.EqualTo(left.Seed), $"derivation {i}");
                for (int draw = 0; draw < 50; draw++)
                    Assert.That(right.NextFloat(), Is.EqualTo(left.NextFloat()), $"derivation {i}, draw {draw}");
            }
        }

        [Test]
        public void SuccessiveDerivedStreamsDiffer()
        {
            var run = new RunContext(1234u);

            IRandomSource first = run.DeriveStream();
            IRandomSource second = run.DeriveStream();

            Assert.That(second.Seed, Is.Not.EqualTo(first.Seed));

            int identical = 0;
            for (int i = 0; i < 200; i++)
            {
                if (first.NextFloat() == second.NextFloat())
                    identical++;
            }

            Assert.That(identical, Is.LessThan(5));
        }

        [Test]
        public void DrawsOnADerivedStreamDoNotAdvanceTheRunStream()
        {
            // The whole point of deriving: however hard the subsystem hammers its own stream,
            // the run stream must land on exactly the values it would have anyway.
            var withTraffic = new RunContext(555u);
            var quiet = new RunContext(555u);

            IRandomSource derived = withTraffic.DeriveStream();
            for (int i = 0; i < 1000; i++)
                derived.NextFloat();

            quiet.DeriveStream(); // same one derivation, then nothing

            for (int i = 0; i < 100; i++)
                Assert.That(withTraffic.Random.NextFloat(), Is.EqualTo(quiet.Random.NextFloat()), $"draw {i}");
        }

        [Test]
        public void DerivingCostsExactlyOneDrawFromTheRunStream()
        {
            var derived = new RunContext(77u);
            var manual = new RunContext(77u);

            derived.DeriveStream();
            manual.Random.NextFloat(); // one draw, by any route

            Assert.That(derived.Random.NextFloat(), Is.EqualTo(manual.Random.NextFloat()));
        }
    }
}
