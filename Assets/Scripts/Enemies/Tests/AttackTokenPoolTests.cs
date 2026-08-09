using NUnit.Framework;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// The attack-token pool is what turns "eight enemies in range" into a readable fight, so the
    /// properties worth pinning are the ones whose failure is invisible in play: a leaked token
    /// never comes back, and a double-count silently lowers the cap for the rest of the room.
    /// </summary>
    public class AttackTokenPoolTests
    {
        sealed class Holder
        {
            public Holder(string name) => Name = name;
            public string Name { get; }
            public override string ToString() => Name;
        }

        static Holder[] Holders(int count)
        {
            var holders = new Holder[count];
            for (int i = 0; i < count; i++)
                holders[i] = new Holder($"h{i}");
            return holders;
        }

        [Test]
        public void GrantsUpToTheGlobalMeleeCapAndNoFurther()
        {
            var pool = new AttackTokenPool(meleeCap: 2, rangedCap: 2);
            Holder[] h = Holders(3);

            Assert.IsTrue(pool.TryAcquire(h[0], AttackTokenKind.Melee));
            Assert.IsTrue(pool.TryAcquire(h[1], AttackTokenKind.Melee));
            Assert.IsFalse(pool.TryAcquire(h[2], AttackTokenKind.Melee));

            Assert.AreEqual(2, pool.ActiveMelee);
        }

        [Test]
        public void MeleeAndRangedAreCountedSeparately()
        {
            var pool = new AttackTokenPool(meleeCap: 1, rangedCap: 1);
            Holder[] h = Holders(2);

            Assert.IsTrue(pool.TryAcquire(h[0], AttackTokenKind.Melee));

            // A full melee queue must not block an archer: two archers plus two rushers is a
            // legible fight, and collapsing them into one budget would make range and melee
            // compete for a slot they never share on screen.
            Assert.IsTrue(pool.TryAcquire(h[1], AttackTokenKind.Ranged));

            Assert.AreEqual(1, pool.ActiveMelee);
            Assert.AreEqual(1, pool.ActiveRanged);
        }

        [Test]
        public void AGroupCapLimitsOneArchetypeInsideAWiderGlobalCap()
        {
            var pool = new AttackTokenPool(meleeCap: 4, rangedCap: 0);
            Holder[] h = Holders(3);

            Assert.IsTrue(pool.TryAcquire(h[0], AttackTokenKind.Melee, "swiftjaw", groupCap: 2));
            Assert.IsTrue(pool.TryAcquire(h[1], AttackTokenKind.Melee, "swiftjaw", groupCap: 2));

            // ENEMIES_BIOME1.md § 2.1: at most 2 Swiftjaws attack at once whatever the pack size,
            // even though the room could carry four attackers in total.
            Assert.IsFalse(pool.TryAcquire(h[2], AttackTokenKind.Melee, "swiftjaw", groupCap: 2));

            // A different archetype still gets in — the group cap is not a global one.
            Assert.IsTrue(pool.TryAcquire(h[2], AttackTokenKind.Melee, "cerashorn", groupCap: 2));
            Assert.AreEqual(3, pool.ActiveMelee);
        }

        [Test]
        public void AcquiringTwiceDoesNotDoubleCount()
        {
            var pool = new AttackTokenPool(meleeCap: 2, rangedCap: 2);
            var holder = new Holder("h");

            Assert.IsTrue(pool.TryAcquire(holder, AttackTokenKind.Melee));
            Assert.IsTrue(pool.TryAcquire(holder, AttackTokenKind.Melee), "re-asking mid-attack must not be refused");

            Assert.AreEqual(1, pool.ActiveMelee, "a controller asking every frame must not consume the whole cap");
        }

        [Test]
        public void ReleasingRestoresTheSlot()
        {
            var pool = new AttackTokenPool(meleeCap: 1, rangedCap: 0);
            Holder[] h = Holders(2);

            Assert.IsTrue(pool.TryAcquire(h[0], AttackTokenKind.Melee));
            Assert.IsFalse(pool.TryAcquire(h[1], AttackTokenKind.Melee));

            pool.Release(h[0]);

            Assert.AreEqual(0, pool.ActiveMelee);
            Assert.IsTrue(pool.TryAcquire(h[1], AttackTokenKind.Melee));
        }

        [Test]
        public void ReleasingAHolderThatNeverHeldOneIsHarmless()
        {
            var pool = new AttackTokenPool(meleeCap: 1, rangedCap: 1);
            Holder[] h = Holders(2);

            pool.TryAcquire(h[0], AttackTokenKind.Melee);

            // The controller releases from every exit path there is — ended, staggered, died,
            // disabled, destroyed — because a leak never recovers. Double and spurious releases
            // must therefore be free.
            pool.Release(h[1]);
            pool.Release(h[0]);
            pool.Release(h[0]);

            Assert.AreEqual(0, pool.ActiveMelee, "a redundant release must not drive the count negative");
            Assert.IsTrue(pool.TryAcquire(h[1], AttackTokenKind.Melee));
        }

        [Test]
        public void CanAcquireAnswersWithoutTaking()
        {
            var pool = new AttackTokenPool(meleeCap: 1, rangedCap: 0);
            Holder[] h = Holders(2);

            Assert.IsTrue(pool.CanAcquire(h[0], AttackTokenKind.Melee));
            Assert.IsTrue(pool.CanAcquire(h[1], AttackTokenKind.Melee));
            Assert.AreEqual(0, pool.ActiveMelee, "asking must not consume the slot");

            pool.TryAcquire(h[0], AttackTokenKind.Melee);
            Assert.IsFalse(pool.CanAcquire(h[1], AttackTokenKind.Melee));
        }

        [Test]
        public void CanAcquireIsTrueForAHolderThatAlreadyHasOne()
        {
            var pool = new AttackTokenPool(meleeCap: 1, rangedCap: 0);
            var holder = new Holder("h");

            pool.TryAcquire(holder, AttackTokenKind.Melee);

            // Otherwise the brain would be told "no" halfway through its own committed wind-up.
            Assert.IsTrue(pool.CanAcquire(holder, AttackTokenKind.Melee));
        }

        [Test]
        public void ReleaseAllEmptiesEveryQueue()
        {
            var pool = new AttackTokenPool(meleeCap: 3, rangedCap: 3);
            Holder[] h = Holders(4);

            pool.TryAcquire(h[0], AttackTokenKind.Melee, "a");
            pool.TryAcquire(h[1], AttackTokenKind.Melee, "a");
            pool.TryAcquire(h[2], AttackTokenKind.Ranged, "b");
            pool.TryAcquire(h[3], AttackTokenKind.Ranged, "b");

            pool.ReleaseAll();

            Assert.AreEqual(0, pool.ActiveMelee);
            Assert.AreEqual(0, pool.ActiveRanged);
            Assert.AreEqual(0, pool.ActiveTotal);
            Assert.AreEqual(0, pool.ActiveInGroup("a"));
        }

        [Test]
        public void AZeroCapRefusesEveryone()
        {
            var pool = new AttackTokenPool(meleeCap: 0, rangedCap: 0);
            Assert.IsFalse(pool.TryAcquire(new Holder("h"), AttackTokenKind.Melee));
        }
    }
}
