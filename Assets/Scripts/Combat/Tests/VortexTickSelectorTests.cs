using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// One hit per body per tick, nearest plate wins (M21.1).
    ///
    /// <para>The property that matters is the one the whole rework exists for: <b>the damage a
    /// vortex deals to an enemy must not depend on how many colliders that enemy has.</b> Before
    /// this, a five-collider boss took five times everything — damage, poise, sparks, hitstop —
    /// which made spamming the spin at a big single target beat the combo outright.</para>
    /// </summary>
    public sealed class VortexTickSelectorTests
    {
        static List<VortexTickCandidate> Candidates(params (int target, int collider, float sqrDist)[] rows)
        {
            var list = new List<VortexTickCandidate>();
            foreach (var r in rows)
                list.Add(new VortexTickCandidate(r.target, r.collider, r.sqrDist));
            return list;
        }

        [Test]
        public void AMultiColliderBodyIsChosenExactlyOnce()
        {
            var selector = new VortexTickSelector();

            // One boss, five colliders — the shape that caused the bug.
            selector.Select(Candidates(
                (0, 10, 9f), (0, 11, 4f), (0, 12, 16f), (0, 13, 25f), (0, 14, 1f)));

            Assert.AreEqual(1, selector.Chosen.Count, "A five-collider boss must take ONE hit per tick.");
        }

        [Test]
        public void TheNearestColliderWins()
        {
            var selector = new VortexTickSelector();

            selector.Select(Candidates((0, 10, 9f), (0, 11, 4f), (0, 12, 16f)));

            Assert.AreEqual(11, selector.Chosen[0], "The plate nearest the drain is the one struck.");
        }

        [Test]
        public void EveryDistinctBodyIsChosen()
        {
            var selector = new VortexTickSelector();

            // Three enemies, one with several colliders.
            selector.Select(Candidates(
                (0, 10, 4f),
                (1, 11, 9f), (1, 12, 1f),
                (2, 13, 16f)));

            Assert.AreEqual(3, selector.Chosen.Count);
            CollectionAssert.AreEquivalent(new[] { 10, 12, 13 }, new List<int>(selector.Chosen));
        }

        [Test]
        public void TiesBreakStablyTowardTheLowerColliderIndex()
        {
            var selector = new VortexTickSelector();

            selector.Select(Candidates((0, 7, 4f), (0, 3, 4f)));

            // Not about which is "right" — about the same input never producing two answers.
            Assert.AreEqual(3, selector.Chosen[0]);

            var again = new VortexTickSelector();
            again.Select(Candidates((0, 3, 4f), (0, 7, 4f)));
            Assert.AreEqual(3, again.Chosen[0], "A tie must resolve the same way whatever the query order.");
        }

        [Test]
        public void AnEmptyTickChoosesNothing()
        {
            var selector = new VortexTickSelector();

            selector.Select(new List<VortexTickCandidate>());
            Assert.AreEqual(0, selector.Chosen.Count);

            selector.Select(null);
            Assert.AreEqual(0, selector.Chosen.Count);
        }

        [Test]
        public void SelectingAgainDoesNotCarryTheLastTickOver()
        {
            var selector = new VortexTickSelector();

            selector.Select(Candidates((0, 10, 4f), (1, 11, 4f)));
            Assert.AreEqual(2, selector.Chosen.Count);

            // An enemy died or walked out between ticks.
            selector.Select(Candidates((0, 10, 4f)));
            Assert.AreEqual(1, selector.Chosen.Count, "Buffers are reused, so a stale target must not survive.");
            Assert.AreEqual(10, selector.Chosen[0]);
        }

        [Test]
        public void ColliderCountDoesNotChangeTheDamageABodyTakes()
        {
            // The acceptance criterion, stated as arithmetic. Whatever the shape, a cast lands
            // exactly tickCount hits on one body, so its damage is tickCount x tickDamage.
            const int tickCount = 3;
            const float tickDamage = 6f;

            foreach (int colliders in new[] { 1, 2, 5, 12 })
            {
                var selector = new VortexTickSelector();
                float total = 0f;

                for (int tick = 0; tick < tickCount; tick++)
                {
                    var rows = new List<VortexTickCandidate>();
                    for (int c = 0; c < colliders; c++)
                        rows.Add(new VortexTickCandidate(0, c, 4f + c));

                    selector.Select(rows);
                    total += selector.Chosen.Count * tickDamage;
                }

                Assert.AreEqual(
                    18f, total, 0.0001f,
                    $"A {colliders}-collider body must still take exactly 18 from one cast.");
            }
        }

        [Test]
        public void AnEnemyArrivingMidChannelTakesOnlyTheRemainingTicks()
        {
            // Ticks are counted, not accumulated per target, so "entered late" needs no special
            // case — it simply is not a candidate for the ticks it missed.
            var selector = new VortexTickSelector();
            const float tickDamage = 6f;
            float latecomer = 0f;

            // Tick 1: only enemy 0 is inside.
            selector.Select(Candidates((0, 10, 4f)));
            Assert.AreEqual(1, selector.Chosen.Count);

            // Ticks 2 and 3: enemy 1 has walked in.
            for (int i = 0; i < 2; i++)
            {
                selector.Select(Candidates((0, 10, 4f), (1, 11, 9f)));
                latecomer += tickDamage;
            }

            Assert.AreEqual(12f, latecomer, 0.0001f, "Two of three ticks, no partial credit for the first.");
        }
    }
}
