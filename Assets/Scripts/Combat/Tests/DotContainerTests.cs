using Game.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>Asset-free stand-in for a DotDefinition, so these never touch a ScriptableObject.</summary>
    internal sealed class FakeDot : IDotDefinition
    {
        public string Id { get; set; } = "burn";
        public string DisplayName { get; set; } = "BURN";
        public DamageType DamageType { get; set; } = DamageType.Fire;
        public float DurationSeconds { get; set; } = 4f;
        public float BaseTotalDamage { get; set; } = 4f;
        public int MaxStacks { get; set; }
        public Color NumberColor { get; set; } = Color.red;
        public StatusEffect StatusFlag { get; set; } = StatusEffect.Burning;
    }

    /// <summary>
    /// The rules the DoT model exists to enforce (M22B). Feel is the human's call; that instances
    /// stack independently, that rarity buys damage and never time, and that no fractional damage
    /// escapes are correctness questions and belong here.
    /// </summary>
    public sealed class DotContainerTests
    {
        const float Frame = 1f / 60f;

        static int Drain(DotContainer dots, float seconds, IDotDefinition definition)
        {
            int total = 0;
            for (float t = 0f; t < seconds - 0.0001f; t += Frame)
            {
                dots.Tick(Frame);
                total += dots.DueWhole(definition);
            }

            return total;
        }

        // -----------------------------------------------------------------------------------
        // Rule 1 — independent stack instances
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// The acceptance criterion verbatim: one Undertow cast, one enemy, three damage events →
        /// three burn instances, applied 1.33 s apart and expiring 1.33 s apart.
        /// </summary>
        [Test]
        public void OneVortexCastLandsThreeInstancesThatExpireOnTheirOwnClocks()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();

            // Three ticks across the 4 s channel, the vortex's real cadence.
            const float tickGap = 4f / 3f;

            dots.Apply(burn, 4f);
            Assert.That(dots.StackCount(burn), Is.EqualTo(1));

            Drain(dots, tickGap, burn);
            dots.Apply(burn, 4f);
            Assert.That(dots.StackCount(burn), Is.EqualTo(2));

            Drain(dots, tickGap, burn);
            dots.Apply(burn, 4f);
            Assert.That(dots.StackCount(burn), Is.EqualTo(3), "one cast, three independent instances");

            // The first expires 4 s after IT was applied, which is 2 x tickGap from here.
            Drain(dots, 4f - 2f * tickGap + Frame, burn);
            Assert.That(dots.StackCount(burn), Is.EqualTo(2), "the first instance expired on its own clock");

            Drain(dots, tickGap, burn);
            Assert.That(dots.StackCount(burn), Is.EqualTo(1));

            Drain(dots, tickGap, burn);
            Assert.That(dots.StackCount(burn), Is.EqualTo(0));
        }

        /// <summary>
        /// The failure the old model had: reapplying refreshed the single timer, so the second
        /// application bought nothing. Nothing may reach into a running instance.
        /// </summary>
        [Test]
        public void ReapplicationNeverRefreshesAnExistingInstance()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();

            dots.Apply(burn, 4f);
            Drain(dots, 3f, burn);
            Assert.That(dots.LongestRemaining(burn), Is.EqualTo(1f).Within(0.05f));

            dots.Apply(burn, 4f);

            // The new instance is the longest-lived, but the OLD one still has only its own second
            // left — proven by it expiring while the new one keeps running.
            Assert.That(dots.StackCount(burn), Is.EqualTo(2));
            Drain(dots, 1.1f, burn);
            Assert.That(dots.StackCount(burn), Is.EqualTo(1), "the older instance died on schedule");
        }

        [Test]
        public void ThreeStacksChipThreeTimesAsFast()
        {
            var burn = new FakeDot { DurationSeconds = 4f, BaseTotalDamage = 4f };

            var single = new DotContainer();
            single.Apply(burn, 4f);

            var triple = new DotContainer();
            triple.Apply(burn, 4f);
            triple.Apply(burn, 4f);
            triple.Apply(burn, 4f);

            // Drained slightly past the two-second mark on purpose. A point due exactly on a
            // boundary can land a frame either side of it, which is invisible in play and not what
            // this test is about; the claim is the ratio between one stack and three.
            Assert.That(Drain(single, 2.05f, burn), Is.EqualTo(2));
            Assert.That(Drain(triple, 2.05f, burn), Is.EqualTo(6));
        }

        // -----------------------------------------------------------------------------------
        // Rule 2 — fixed duration, rarity scales damage
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Rare and Epic deal more total damage over the SAME four seconds. The multipliers are the
        /// project-wide additive-on-base table (x1 / x1.5 / x2), stated here as arithmetic so a
        /// change to it fails loudly rather than silently retuning every DoT.
        /// </summary>
        [Test]
        public void HigherRarityDealsMoreDamageOverTheSameDuration()
        {
            var burn = new FakeDot { DurationSeconds = 4f, BaseTotalDamage = 4f };

            var normal = new DotContainer();
            var rare = new DotContainer();
            var epic = new DotContainer();

            normal.Apply(burn, 4f * 1.0f);
            rare.Apply(burn, 4f * 1.5f);
            epic.Apply(burn, 4f * 2.0f);

            Assert.That(Drain(normal, 5f, burn), Is.EqualTo(4));
            Assert.That(Drain(rare, 5f, burn), Is.EqualTo(6));
            Assert.That(Drain(epic, 5f, burn), Is.EqualTo(8));

            // And every one of them was finished at four seconds, not later.
            Assert.That(epic.StackCount(burn), Is.EqualTo(0));
        }

        [Test]
        public void AnInstanceDealsExactlyItsTotalAndNoMore()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();
            dots.Apply(burn, 6f);

            Assert.That(Drain(dots, 4.1f, burn), Is.EqualTo(6), "the lifetime total is the authored figure, exactly");
            Assert.That(Drain(dots, 4f, burn), Is.EqualTo(0), "an expired instance keeps paying nothing");
        }

        /// <summary>
        /// A frame far longer than the instance has left must not overpay it. Hitches happen, and a
        /// DoT that deals its whole total to a single long frame would make the tier numbers a lie.
        /// </summary>
        [Test]
        public void ALongFrameNeverOverpaysAnExpiringInstance()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();
            dots.Apply(burn, 4f);

            dots.Tick(3.5f);
            int paid = dots.DueWhole(burn);

            dots.Tick(10f);          // a hitch far past the end
            paid += dots.DueWhole(burn);

            Assert.That(paid, Is.EqualTo(4));
            Assert.That(dots.StackCount(burn), Is.EqualTo(0));
        }

        // -----------------------------------------------------------------------------------
        // Rule 3 — whole numbers only
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Damage accrues as a fraction and only whole points ever leave. A health bar moving by
        /// 0.37 is a health bar that appears not to move at all.
        /// </summary>
        [Test]
        public void FractionalDamageIsBankedUntilItMakesAWholePoint()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();
            dots.Apply(burn, 4f);          // exactly 1 per second

            // Half a second in, 0.5 has accrued and nothing has been paid.
            Assert.That(Drain(dots, 0.5f, burn), Is.EqualTo(0));

            // The whole point lands as the second completes.
            Assert.That(Drain(dots, 0.55f, burn), Is.EqualTo(1));
        }

        /// <summary>The remainder survives the instance that earned it, so a lapse loses nothing.</summary>
        [Test]
        public void ABankedFractionCarriesIntoTheNextApplication()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();

            dots.Apply(burn, 3f);          // 0.75/s over 4s = 3 total
            Assert.That(Drain(dots, 4.5f, burn), Is.EqualTo(3));

            // A fresh instance continues from whatever fraction was left rather than from zero.
            dots.Apply(burn, 3f);
            Assert.That(Drain(dots, 4.5f, burn), Is.EqualTo(3));
        }

        // -----------------------------------------------------------------------------------
        // Rules 5 and 6 — coexistence and the cap
        // -----------------------------------------------------------------------------------

        [Test]
        public void TwoTypesAreSeparatePoolsWithSeparateInstances()
        {
            var burn = new FakeDot { Id = "burn", DurationSeconds = 4f };
            var decay = new FakeDot { Id = "decay", DurationSeconds = 4f, StatusFlag = StatusEffect.Decaying };
            var dots = new DotContainer();

            dots.Apply(burn, 4f);
            dots.Apply(decay, 8f);

            Assert.That(dots.StackCount(burn), Is.EqualTo(1));
            Assert.That(dots.StackCount(decay), Is.EqualTo(1));
            Assert.That(dots.Types.Count, Is.EqualTo(2));

            dots.Tick(1f);
            Assert.That(dots.DueWhole(burn), Is.EqualTo(1));
            Assert.That(dots.DueWhole(decay), Is.EqualTo(2), "decay's pool is its own, and twice as hot");
        }

        [Test]
        public void ZeroMaxStacksIsUncapped()
        {
            var burn = new FakeDot { MaxStacks = 0 };
            var dots = new DotContainer();

            for (int i = 0; i < 20; i++)
                Assert.That(dots.Apply(burn, 4f), Is.True);

            Assert.That(dots.StackCount(burn), Is.EqualTo(20));
        }

        [Test]
        public void AStackCapRefusesTheOneOverIt()
        {
            var burn = new FakeDot { MaxStacks = 3 };
            var dots = new DotContainer();

            Assert.That(dots.Apply(burn, 4f), Is.True);
            Assert.That(dots.Apply(burn, 4f), Is.True);
            Assert.That(dots.Apply(burn, 4f), Is.True);
            Assert.That(dots.Apply(burn, 4f), Is.False, "the fourth is refused, not swapped in");
            Assert.That(dots.StackCount(burn), Is.EqualTo(3));

            // A cap on one type says nothing about another.
            var decay = new FakeDot { Id = "decay", MaxStacks = 3 };
            Assert.That(dots.Apply(decay, 4f), Is.True);
        }

        /// <summary>A cap frees up as instances expire — it bounds concurrency, not applications.</summary>
        [Test]
        public void ACapReopensAsInstancesExpire()
        {
            var burn = new FakeDot { MaxStacks = 1, DurationSeconds = 4f };
            var dots = new DotContainer();

            Assert.That(dots.Apply(burn, 4f), Is.True);
            Assert.That(dots.Apply(burn, 4f), Is.False);

            Drain(dots, 4.1f, burn);
            Assert.That(dots.Apply(burn, 4f), Is.True);
        }

        // -----------------------------------------------------------------------------------
        // Housekeeping
        // -----------------------------------------------------------------------------------

        [Test]
        public void NothingIsAppliedForNoDamageOrNoDefinition()
        {
            var dots = new DotContainer();

            Assert.That(dots.Apply(null, 4f), Is.False);
            Assert.That(dots.Apply(new FakeDot(), 0f), Is.False);
            Assert.That(dots.Apply(new FakeDot(), -3f), Is.False);
            Assert.That(dots.TotalStacks, Is.EqualTo(0));
        }

        [Test]
        public void LongestRemainingTracksTheLastAppliedInstance()
        {
            var burn = new FakeDot { DurationSeconds = 4f };
            var dots = new DotContainer();

            dots.Apply(burn, 4f);
            Drain(dots, 2f, burn);
            dots.Apply(burn, 4f);

            Assert.That(dots.LongestRemaining(burn), Is.EqualTo(4f).Within(0.05f));
            Assert.That(dots.Has(burn), Is.True);

            Drain(dots, 4.1f, burn);
            Assert.That(dots.LongestRemaining(burn), Is.EqualTo(0f));
            Assert.That(dots.Has(burn), Is.False);
        }

        [Test]
        public void ClearDropsEverything()
        {
            var burn = new FakeDot();
            var dots = new DotContainer();
            dots.Apply(burn, 4f);
            dots.Tick(0.5f);

            dots.Clear();

            Assert.That(dots.TotalStacks, Is.EqualTo(0));
            Assert.That(dots.Types.Count, Is.EqualTo(0));
            Assert.That(dots.DueWhole(burn), Is.EqualTo(0));
        }

        [Test]
        public void AZeroLengthFrameChangesNothing()
        {
            var burn = new FakeDot();
            var dots = new DotContainer();
            dots.Apply(burn, 4f);

            dots.Tick(0f);

            Assert.That(dots.StackCount(burn), Is.EqualTo(1));
            Assert.That(dots.DueWhole(burn), Is.EqualTo(0));
        }
    }
}
