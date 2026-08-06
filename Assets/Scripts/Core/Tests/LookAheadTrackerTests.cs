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

        static void Run(LookAheadTracker tracker, Vector3 velocityFraction, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
                tracker.Tick(velocityFraction, Step);
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
            Run(tracker, Vector3.forward, 2f);
            Assert.That(tracker.Offset.magnitude, Is.EqualTo(settings.LookAheadDistance).Within(0.02f));
            Assert.That(tracker.Offset.z, Is.GreaterThan(0f));
        }

        [Test]
        public void OffsetScalesWithSpeed()
        {
            LookAheadTracker tracker = Make(out FakeMovementSettings settings);
            Run(tracker, Vector3.forward * 0.5f, 2f);
            Assert.That(tracker.Offset.magnitude, Is.EqualTo(settings.LookAheadDistance * 0.5f).Within(0.02f));
        }

        [Test]
        public void StoppingPullsOffsetBackToZero()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, Vector3.forward, 2f);
            Run(tracker, Vector3.zero, 2f);
            Assert.That(tracker.Offset.magnitude, Is.LessThan(0.02f));
        }

        [Test]
        public void OffsetIsDamped_NotInstant()
        {
            LookAheadTracker tracker = Make(out FakeMovementSettings settings);
            tracker.Tick(Vector3.forward, Step);
            Assert.That(tracker.Offset.magnitude, Is.LessThan(settings.LookAheadDistance * 0.5f));
        }

        [Test]
        public void OffsetStaysPlanar()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, new Vector3(0.5f, 5f, 0.5f), 1f);
            Assert.That(tracker.Offset.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void OverUnitVelocity_IsClampedToLookAheadDistance()
        {
            LookAheadTracker tracker = Make(out FakeMovementSettings settings);
            Run(tracker, new Vector3(3f, 0f, 3f), 2f);
            Assert.That(tracker.Offset.magnitude, Is.EqualTo(settings.LookAheadDistance).Within(0.02f));
        }

        [Test]
        public void DirectionReversal_NeverSwingsSideways()
        {
            // The motion-sickness regression: a facing-driven look-ahead sweeps a lateral arc
            // when the player turns around. A velocity-driven one must retract through zero
            // and never introduce an axis the player is not travelling along.
            LookAheadTracker tracker = Make(out _);
            Run(tracker, Vector3.right, 2f);

            float maxLateral = 0f;
            for (int i = 0; i < 120; i++)
            {
                // Velocity reverses over ~8 frames, as accel/decel would produce.
                float t = Mathf.Clamp01(i / 8f);
                tracker.Tick(Vector3.right * Mathf.Lerp(1f, -1f, t), Step);
                maxLateral = Mathf.Max(maxLateral, Mathf.Abs(tracker.Offset.z));
            }

            Assert.That(maxLateral, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(tracker.Offset.x, Is.LessThan(0f));
        }

        [Test]
        public void LongerSmoothTime_MovesTheOffsetMoreSlowly()
        {
            LookAheadTracker lazy = Make(out FakeMovementSettings lazySettings);
            lazySettings.LookAheadSmoothTime = 0.6f;
            LookAheadTracker eager = Make(out FakeMovementSettings eagerSettings);
            eagerSettings.LookAheadSmoothTime = 0.2f;

            Run(lazy, Vector3.forward, 0.2f);
            Run(eager, Vector3.forward, 0.2f);

            Assert.That(lazy.Offset.magnitude, Is.LessThan(eager.Offset.magnitude));
        }

        [Test]
        public void Reset_ClearsOffset()
        {
            LookAheadTracker tracker = Make(out _);
            Run(tracker, Vector3.forward, 1f);
            tracker.Reset();
            Assert.That(tracker.Offset, Is.EqualTo(Vector3.zero));
        }
    }
}
