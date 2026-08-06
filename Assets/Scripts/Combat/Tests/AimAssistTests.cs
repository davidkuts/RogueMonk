using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    public class AimAssistTests
    {
        const float Cone = 45f;
        const float Range = 3f;

        static bool Select(Vector3[] candidates, out int index, float cone = Cone, float range = Range) =>
            AimAssist.TrySelectTarget(Vector3.zero, Vector3.forward, candidates, cone, range, out index);

        [Test]
        public void NoCandidates_SelectsNothing()
        {
            int index;
            Assert.That(Select(new Vector3[0], out index), Is.False);
            Assert.That(AimAssist.TrySelectTarget(Vector3.zero, Vector3.forward, null, Cone, Range, out index), Is.False);
        }

        [Test]
        public void PicksATargetStraightAhead()
        {
            int index;
            Assert.That(Select(new[] { new Vector3(0f, 0f, 2f) }, out index), Is.True);
            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void IgnoresTargetsBehind()
        {
            int index;
            Assert.That(Select(new[] { new Vector3(0f, 0f, -2f) }, out index), Is.False);
        }

        [Test]
        public void IgnoresTargetsOutOfRange()
        {
            int index;
            Assert.That(Select(new[] { new Vector3(0f, 0f, 5f) }, out index), Is.False);
        }

        [Test]
        public void ConeIsAFullWidth_HalfEachSide()
        {
            // 45 deg cone => 22.5 deg either side of facing.
            var justInside = new[] { Quaternion.Euler(0f, 22f, 0f) * Vector3.forward * 2f };
            var justOutside = new[] { Quaternion.Euler(0f, 23f, 0f) * Vector3.forward * 2f };

            int index;
            Assert.That(Select(justInside, out index), Is.True);
            Assert.That(Select(justOutside, out index), Is.False);
        }

        [Test]
        public void PicksTheNearestQualifyingTarget()
        {
            var candidates = new[]
            {
                new Vector3(0f, 0f, 2.5f),
                new Vector3(0f, 0f, 1.0f),
                new Vector3(0f, 0f, 2.0f),
            };

            int index;
            Assert.That(Select(candidates, out index), Is.True);
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void NearestOutOfConeLosesToFurtherInCone()
        {
            var candidates = new[]
            {
                new Vector3(1.0f, 0f, 0f),   // 90 deg off — outside the cone despite being closest
                new Vector3(0f, 0f, 2.5f),   // straight ahead
            };

            int index;
            Assert.That(Select(candidates, out index), Is.True);
            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void HeightIsIgnored_SelectionIsPlanar()
        {
            var candidates = new[] { new Vector3(0f, 8f, 2f) };
            int index;
            Assert.That(Select(candidates, out index), Is.True);
        }

        [Test]
        public void DegenerateFacingSelectsNothing()
        {
            int index;
            bool found = AimAssist.TrySelectTarget(
                Vector3.zero, Vector3.zero, new[] { Vector3.forward }, Cone, Range, out index);
            Assert.That(found, Is.False);
        }

        [Test]
        public void RotateFacing_TurnsTowardTheTargetAtACappedRate()
        {
            Vector3 result = AimAssist.RotateFacing(Vector3.forward, Vector3.right, 90f, 0.5f);
            Assert.That(Vector3.Angle(Vector3.forward, result), Is.EqualTo(45f).Within(0.5f));
        }

        [Test]
        public void RotateFacing_NeverSnaps()
        {
            // The whole point of aim-snap speed: the player must be able to read the turn.
            Vector3 result = AimAssist.RotateFacing(Vector3.forward, Vector3.right, 90f, 1f / 60f);
            Assert.That(Vector3.Angle(result, Vector3.right), Is.GreaterThan(80f));
        }

        [Test]
        public void RotateFacing_ArrivesWhenAllowedEnoughTime()
        {
            Vector3 result = AimAssist.RotateFacing(Vector3.forward, Vector3.right, 1000f, 1f);
            Assert.That(Vector3.Angle(result, Vector3.right), Is.LessThan(0.5f));
        }

        [Test]
        public void RotateFacing_StaysNormalizedAndPlanar()
        {
            Vector3 result = AimAssist.RotateFacing(Vector3.forward, new Vector3(1f, 5f, 1f), 500f, 0.1f);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(result.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void RotateFacing_HandlesDegenerateInput()
        {
            Assert.That(AimAssist.RotateFacing(Vector3.forward, Vector3.zero, 90f, 0.1f), Is.EqualTo(Vector3.forward));
            Assert.That(AimAssist.RotateFacing(Vector3.zero, Vector3.right, 90f, 0.1f), Is.EqualTo(Vector3.forward));
        }
    }
}
