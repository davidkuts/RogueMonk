using Game.Core.Locomotion;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// Sailspit's stall zones, and the rule that keeps them from becoming a stun.
    /// </summary>
    public class SpeedFieldAccumulatorTests
    {
        [Test]
        public void CleanGroundIsFullSpeed()
        {
            var fields = new SpeedFieldAccumulator();

            // A default-constructed struct has every field at zero, which would read as "frozen
            // solid". The whole player would be unable to move before a single zone existed.
            Assert.AreEqual(1f, fields.Current, 1e-6f);

            fields.EndFrame();
            Assert.AreEqual(1f, fields.Current, 1e-6f);
        }

        [Test]
        public void OneFieldAppliesOnTheFollowingFrame()
        {
            var fields = new SpeedFieldAccumulator();

            fields.Report(0.45f);
            Assert.AreEqual(1f, fields.Current, 1e-6f, "reports are gathered, not applied mid-frame");

            fields.EndFrame();
            Assert.AreEqual(0.45f, fields.Current, 1e-6f);
        }

        [Test]
        public void OverlappingFieldsDoNotStack()
        {
            var fields = new SpeedFieldAccumulator();

            fields.Report(0.45f);
            fields.Report(0.45f);
            fields.Report(0.45f);
            fields.EndFrame();

            // ENEMIES_BIOME1.md § 2.3: overlapping stall zones may overlap but must not stack their
            // slow. The product would be 0.091 — a hard lock, and precisely the accidental stun the
            // design forbids. Two Sailspits are meant to shape the arena, not to remove the player's
            // turn.
            Assert.AreEqual(0.45f, fields.Current, 1e-6f);
        }

        [Test]
        public void TheSlowestFieldWinsRegardlessOfOrder()
        {
            var ascending = new SpeedFieldAccumulator();
            ascending.Report(0.3f);
            ascending.Report(0.8f);
            ascending.Report(0.9f);
            ascending.EndFrame();

            var descending = new SpeedFieldAccumulator();
            descending.Report(0.9f);
            descending.Report(0.8f);
            descending.Report(0.3f);
            descending.EndFrame();

            // Order must not matter: zones report from their own Update and Unity does not order
            // Updates, so a last-one-wins rule would make the slow depend on component order.
            Assert.AreEqual(0.3f, ascending.Current, 1e-6f);
            Assert.AreEqual(0.3f, descending.Current, 1e-6f);
        }

        [Test]
        public void LeavingEveryFieldRestoresFullSpeed()
        {
            var fields = new SpeedFieldAccumulator();

            fields.Report(0.45f);
            fields.EndFrame();
            Assert.AreEqual(0.45f, fields.Current, 1e-6f);

            // Nothing reported this frame: the player has walked out. Without the reopen, the last
            // value would stick and a two-second puddle would slow them for the rest of the run.
            fields.EndFrame();
            Assert.AreEqual(1f, fields.Current, 1e-6f);
        }

        [Test]
        public void MultipliersAreClampedToSomethingSane()
        {
            var fields = new SpeedFieldAccumulator();

            fields.Report(-2f);
            fields.EndFrame();
            Assert.AreEqual(0f, fields.Current, 1e-6f, "a negative field must not reverse the player");

            fields.Report(4f);
            fields.EndFrame();
            Assert.AreEqual(1f, fields.Current, 1e-6f, "a field can slow, never speed up");
        }
    }
}
