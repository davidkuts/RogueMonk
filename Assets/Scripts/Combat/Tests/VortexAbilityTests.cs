using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The Undertow's availability rules (DESIGN.md § The Vortex § Recharge). These are the parts
    /// that must not drift: uptime rides landed hits rather than perfect dodges, and whiffs pay
    /// nothing.
    /// </summary>
    public sealed class VortexAbilityTests
    {
        [Test]
        public void ItStartsReady()
        {
            var ability = new VortexAbility(10f, 0.4f);

            Assert.IsTrue(ability.IsReady);
            Assert.AreEqual(1f, ability.ReadyFraction, 0.0001f);
        }

        [Test]
        public void ConsumingStartsTheFullCooldown()
        {
            var ability = new VortexAbility(10f, 0.4f);

            Assert.IsTrue(ability.TryConsume());
            Assert.IsFalse(ability.IsReady);
            Assert.AreEqual(10f, ability.CooldownRemaining, 0.0001f);
        }

        [Test]
        public void ItCannotBeConsumedTwiceWithoutRecharging()
        {
            var ability = new VortexAbility(10f, 0.4f);
            ability.TryConsume();

            Assert.IsFalse(ability.TryConsume(), "A recharging vortex must not be castable.");
        }

        [Test]
        public void ItBecomesReadyAgainOnTheCooldown()
        {
            var ability = new VortexAbility(2f, 0.4f);
            int readyEvents = 0;
            ability.BecameReady += () => readyEvents++;

            ability.TryConsume();
            ability.Tick(1.9f);
            Assert.IsFalse(ability.IsReady);
            Assert.AreEqual(0, readyEvents);

            ability.Tick(0.2f);
            Assert.IsTrue(ability.IsReady);
            Assert.AreEqual(1, readyEvents, "Readiness must announce itself exactly once.");
        }

        [Test]
        public void ALandedHitShavesTheCooldown()
        {
            var ability = new VortexAbility(10f, 0.4f);
            ability.TryConsume();

            ability.RegisterLandedHit();
            ability.RegisterLandedHit();
            ability.RegisterLandedHit();

            // A three-hit chain is worth 1.2s, per the tuning table.
            Assert.AreEqual(8.8f, ability.CooldownRemaining, 0.0001f);
        }

        [Test]
        public void AggressiveAndPassivePlayBothConverge()
        {
            // The whole point of the refund: aggression roughly doubles uptime, and doing nothing
            // still gets the ability back on a fair timer.
            var passive = new VortexAbility(10f, 0.4f);
            var aggressive = new VortexAbility(10f, 0.4f);
            passive.TryConsume();
            aggressive.TryConsume();

            // ~2.3 hits/second is the real chain rate measured in M10.2.
            for (int i = 0; i < 50; i++)
            {
                passive.Tick(0.1f);
                aggressive.Tick(0.1f);
                if (i % 4 == 0)
                    aggressive.RegisterLandedHit();
            }

            Assert.IsTrue(aggressive.IsReady, "Sustained aggression should have cycled the vortex.");
            Assert.IsFalse(passive.IsReady, "Standing still should not.");
        }

        [Test]
        public void RefundsDoNotBankAgainstTheNextCast()
        {
            var ability = new VortexAbility(10f, 0.4f);

            // Already ready: hits landed now must not carry credit forward.
            for (int i = 0; i < 20; i++)
                ability.RegisterLandedHit();

            ability.TryConsume();
            Assert.AreEqual(10f, ability.CooldownRemaining, 0.0001f);
        }

        [Test]
        public void AZeroCooldownIsAlwaysReady()
        {
            var ability = new VortexAbility(0f, 0.4f);

            Assert.IsTrue(ability.TryConsume());
            Assert.IsTrue(ability.IsReady);
            Assert.AreEqual(1f, ability.ReadyFraction, 0.0001f);
        }
    }

    /// <summary>
    /// Tick scheduling. The guarantee that matters is the same one
    /// <see cref="AttackStateMachine"/> gives the hitbox: a long frame must never swallow a tick.
    /// </summary>
    public sealed class VortexSpinTests
    {
        [Test]
        public void TheFirstTickLandsOnTheOpeningFrame()
        {
            var spin = new VortexSpin(3, 0.45f);

            Assert.AreEqual(1, spin.Due(0f), "Under pressure the spin has to feel immediate.");
        }

        [Test]
        public void TicksAreSpreadAcrossTheActiveWindow()
        {
            var spin = new VortexSpin(3, 0.45f);

            Assert.AreEqual(1, spin.Due(0f));
            Assert.AreEqual(0, spin.Due(0.10f));
            Assert.AreEqual(1, spin.Due(0.16f));
            Assert.AreEqual(0, spin.Due(0.25f));
            Assert.AreEqual(1, spin.Due(0.31f));
            Assert.AreEqual(3, spin.Fired);
        }

        [Test]
        public void ItNeverFiresMoreTicksThanAuthored()
        {
            var spin = new VortexSpin(3, 0.45f);

            spin.Due(0f);
            spin.Due(10f);

            Assert.AreEqual(3, spin.Fired);
            Assert.AreEqual(0, spin.Due(20f));
        }

        [Test]
        public void ASingleLongFrameStillPaysEveryTick()
        {
            var spin = new VortexSpin(3, 0.45f);

            // One frame swallows the entire active window.
            Assert.AreEqual(3, spin.Due(0.5f));
        }

        [Test]
        public void DrainPaysOutWhateverTheWindowStillOwed()
        {
            var spin = new VortexSpin(3, 0.45f);
            spin.Due(0f);

            Assert.AreEqual(2, spin.Drain());
            Assert.AreEqual(0, spin.Drain(), "Draining twice must not double-pay.");
        }

        [Test]
        public void ResetArmsTheNextSpin()
        {
            var spin = new VortexSpin(3, 0.45f);
            spin.Due(1f);
            spin.Reset();

            Assert.AreEqual(0, spin.Fired);
            Assert.AreEqual(1, spin.Due(0f));
        }
    }
}
