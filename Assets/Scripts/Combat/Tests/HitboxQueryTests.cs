using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// Covers the arc's narrow phase. The broad phase is Unity physics and is not tested here;
    /// what matters is that the wedge rejects everything the bounding sphere lets through.
    /// </summary>
    public class HitboxQueryTests
    {
        static HitboxShape Arc(float degrees, float radius = 3f) => new HitboxShape
        {
            Kind = HitboxKind.Arc,
            LocalOffset = Vector3.zero,
            Radius = radius,
            ArcDegrees = degrees,
        };

        static readonly Vector3 Origin = Vector3.zero;
        static readonly Vector3 Forward = Vector3.forward;

        [Test]
        public void APointStraightAheadIsInsideAnyArc()
        {
            Assert.That(HitboxQuery.Contains(Arc(60f), Origin, Forward, new Vector3(0f, 0f, 2f)), Is.True);
        }

        [Test]
        public void APointBehindIsOutsideAForwardArc()
        {
            // This is the whole point of the shape: a sphere would have hit them.
            Assert.That(HitboxQuery.Contains(Arc(110f), Origin, Forward, new Vector3(0f, 0f, -2f)), Is.False);
        }

        [Test]
        public void APointToTheSideIsOutsideANarrowArcAndInsideAWideOne()
        {
            var side = new Vector3(2f, 0f, 0f);   // exactly 90 degrees off facing

            Assert.That(HitboxQuery.Contains(Arc(110f), Origin, Forward, side), Is.False,
                "55 degrees of half-width cannot reach 90 degrees");
            Assert.That(HitboxQuery.Contains(Arc(200f), Origin, Forward, side), Is.True,
                "100 degrees of half-width can");
        }

        [Test]
        public void TheBoundaryIsInclusive()
        {
            // Exactly on the edge of a 90 degree arc: 45 degrees off facing.
            var edge = new Vector3(1f, 0f, 1f);

            Assert.That(HitboxQuery.Contains(Arc(90f), Origin, Forward, edge), Is.True);
        }

        [Test]
        public void HeightIsIgnored()
        {
            // A tall enemy must not escape a ground-level swing by being tall.
            var high = new Vector3(0f, 5f, 2f);

            Assert.That(HitboxQuery.Contains(Arc(90f), Origin, Forward, high), Is.True);
        }

        [Test]
        public void AFullCircleArcContainsEverything()
        {
            HitboxShape full = Arc(360f);

            Assert.That(HitboxQuery.Contains(full, Origin, Forward, new Vector3(0f, 0f, -3f)), Is.True);
            Assert.That(HitboxQuery.Contains(full, Origin, Forward, new Vector3(-3f, 0f, 0f)), Is.True);
        }

        [Test]
        public void AnUnsetArcWidthIsTreatedAsAFullCircle()
        {
            // Existing assets have no ArcDegrees key, so a zero must not mean "hits nothing".
            var shape = new HitboxShape { Kind = HitboxKind.Arc, Radius = 3f, ArcDegrees = 0f };

            Assert.That(shape.EffectiveArcDegrees, Is.EqualTo(360f));
            Assert.That(HitboxQuery.Contains(shape, Origin, Forward, new Vector3(0f, 0f, -2f)), Is.True);
        }

        [Test]
        public void TheArcRotatesWithFacing()
        {
            var target = new Vector3(2f, 0f, 0f);
            HitboxShape arc = Arc(90f);

            Assert.That(HitboxQuery.Contains(arc, Origin, Vector3.forward, target), Is.False);
            Assert.That(HitboxQuery.Contains(arc, Origin, Vector3.right, target), Is.True, "turn to face it and it lands");
        }

        [Test]
        public void SpheresAndBoxesHaveNoNarrowPhase()
        {
            // Their broad phase was already exact; a narrow phase would double-reject.
            var sphere = new HitboxShape { Kind = HitboxKind.Sphere, Radius = 1f };
            var box = new HitboxShape { Kind = HitboxKind.Box, Size = Vector3.one };

            Assert.That(HitboxQuery.Contains(sphere, Origin, Forward, new Vector3(0f, 0f, -50f)), Is.True);
            Assert.That(HitboxQuery.Contains(box, Origin, Forward, new Vector3(0f, 0f, -50f)), Is.True);
        }

        [Test]
        public void SomeoneStandingExactlyOnTheAttackerIsHit()
        {
            // Otherwise the pivot point becomes a hiding place with no bearing to test.
            Assert.That(HitboxQuery.Contains(Arc(60f), Origin, Forward, Origin), Is.True);
        }

        [Test]
        public void AnOffsetArcMeasuresBearingFromItsOwnCentre()
        {
            var shape = new HitboxShape
            {
                Kind = HitboxKind.Arc,
                LocalOffset = new Vector3(0f, 0f, 5f),
                Radius = 2f,
                ArcDegrees = 90f,
            };

            // 6 m ahead is straight in front of a centre that sits at 5 m.
            Assert.That(HitboxQuery.Contains(shape, Origin, Forward, new Vector3(0f, 0f, 6f)), Is.True);

            // 4 m ahead is *behind* that centre.
            Assert.That(HitboxQuery.Contains(shape, Origin, Forward, new Vector3(0f, 0f, 4f)), Is.False);
        }
    }
}
