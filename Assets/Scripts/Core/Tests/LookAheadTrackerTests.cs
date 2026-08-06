using Game.Core.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class LookAheadTrackerTests
    {
        const float Step = 1f / 60f;

        static LookAheadTracker Make(out FakeMovementSettings settings)
        {
            settings = new FakeMovementSettings();
            return new LookAheadTracker(settings);
        }

        static void Run(LookAheadTracker tracker, Vector3 facing, float normalizedSpeed, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
                tracker.Tick(facing, normalizedSpeed, Step);
        }

        [Test]
        public void StartsAtZeroOffset()
        {
            LookAheadTracker tracker = Make(out _);
            Assert.That(tracker.Offset, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ConvergesToLookAheadDistance_AtFullSpeed()
        {
            LookAheadTracker tracker = Make(out FakeMovementSettings settings);
            Run(tracker, Vector3.forward, 1f, 2f);
            Assert.That(tracker.Offset.magnitude, Is.EqualTo(settings.LookAheadDistance).Within(0.02f));
            Assert.That(tracker.Offset.z, Is.GreaterThan(0f));
        }

        [Test]
        public void OffsetScalesWithSpeed()
        {
            LookAheadTracker tracker = Make(out FakeMovementSettings settings);
            Run(tracker, Vector3.forward, 0.5f, 2f);
            Assert.That(tracker.Offset.magnitude, Is.EqualTo(settings.LookAheadDistance * 0.5f).Within(0.02f));
        }

        [Test]
        public void StoppingPullsOffsetBackToZero()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, Vector3.forward, 1f, 2f);
            Run(tracker, Vector3.forward, 0f, 2f);
            Assert.That(tracker.Offset.magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void OffsetIsDamped_NotInstant()
        {
            LookAheadTracker tracker = Make(out FakeMovementSettings settings);
            tracker.Tick(Vector3.forward, 1f, Step);
            Assert.That(tracker.Offset.magnitude, Is.LessThan(settings.LookAheadDistance * 0.5f));
        }

        [Test]
        public void OffsetStaysPlanar()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, new Vector3(1f, 5f, 1f), 1f, 1f);
            Assert.That(tracker.Offset.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void DegenerateFacing_TargetsZero()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, Vector3.forward, 1f, 2f);
            Run(tracker, Vector3.zero, 1f, 2f);
            Assert.That(tracker.Offset.magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void Reset_ClearsOffset()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, Vector3.forward, 1f, 1f);
            tracker.Reset();
            Assert.That(tracker.Offset, Is.EqualTo(Vector3.zero));
        }
    }
}
