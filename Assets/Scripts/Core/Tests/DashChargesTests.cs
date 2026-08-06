using Game.Core.Locomotion;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class DashChargesTests
    {
        static DashCharges Make(out FakeDashSettings settings)
        {
            settings = new FakeDashSettings();
            return new DashCharges(settings);
        }

        [Test]
        public void StartsFull()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            Assert.That(charges.Available, Is.EqualTo(settings.MaxCharges));
            Assert.That(charges.HasCharge, Is.True);
        }

        [Test]
        public void SpendingReducesAvailable()
        {
            DashCharges charges = Make(out _);
            Assert.That(charges.TrySpend(), Is.True);
            Assert.That(charges.Available, Is.EqualTo(1));
        }

        [Test]
        public void CannotSpendWhenEmpty()
        {
            DashCharges charges = Make(out _);
            charges.TrySpend();
            charges.TrySpend();
            Assert.That(charges.Available, Is.EqualTo(0));
            Assert.That(charges.TrySpend(), Is.False);
        }

        [Test]
        public void ChargeReturnsAfterRechargeTime()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.Tick(settings.RechargeSeconds - 0.01f);
            Assert.That(charges.Available, Is.EqualTo(1), "returned early");
            charges.Tick(0.02f);
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void RechargeIsSequential_NotParallel()
        {
            // Both charges spent back-to-back must come back one at a time, a full
            // recharge apart — never together.
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.TrySpend();
            Assert.That(charges.Available, Is.EqualTo(0));

            charges.Tick(settings.RechargeSeconds + 0.01f);
            Assert.That(charges.Available, Is.EqualTo(1), "only one charge may return per recharge period");

            charges.Tick(settings.RechargeSeconds);
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void SpendingASecondCharge_DoesNotRestartTheRunningTimer()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.Tick(settings.RechargeSeconds - 0.1f); // first charge is nearly back
            charges.TrySpend();

            charges.Tick(0.11f);
            Assert.That(charges.Available, Is.EqualTo(1), "the first charge keeps its original schedule");
        }

        [Test]
        public void FullPoolDrainsAndRefillsOverTwoPeriods()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.TrySpend();

            charges.Tick(settings.RechargeSeconds * 2f + 0.01f);
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void ALongFrameCompletesMoreThanOneCharge()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.TrySpend();
            charges.Tick(settings.RechargeSeconds * 5f);
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void RefundReturnsAChargeImmediately()
        {
            DashCharges charges = Make(out _);
            charges.TrySpend();
            charges.Refund();
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void RefundKeepsProgressOnTheRemainingCharge()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.Tick(settings.RechargeSeconds - 0.1f);
            charges.TrySpend(); // now 0 available, timer 0.1 s from returning one

            charges.Refund(); // perfect dodge

            Assert.That(charges.Available, Is.EqualTo(1));
            charges.Tick(0.11f);
            Assert.That(charges.Available, Is.EqualTo(2), "accumulated recharge progress must survive a refund");
        }

        [Test]
        public void RefundWhenFull_IsANoOp()
        {
            DashCharges charges = Make(out _);
            charges.Refund();
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void NeverExceedsMaxCharges()
        {
            DashCharges charges = Make(out _);
            charges.TrySpend();
            charges.Tick(100f);
            charges.Tick(100f);
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void NextChargeProgress_RunsZeroToOne()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            Assert.That(charges.NextChargeProgress, Is.EqualTo(1f), "full pool should read as complete");

            charges.TrySpend();
            Assert.That(charges.NextChargeProgress, Is.EqualTo(0f).Within(1e-4f));

            charges.Tick(settings.RechargeSeconds * 0.5f);
            Assert.That(charges.NextChargeProgress, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void PipFill_FullPoolReadsAllFull()
        {
            DashCharges charges = Make(out _);
            Assert.That(charges.GetChargeFill(0), Is.EqualTo(1f));
            Assert.That(charges.GetChargeFill(1), Is.EqualTo(1f));
        }

        [Test]
        public void PipFill_OnlyOnePipFillsAtATime()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.TrySpend();
            charges.Tick(settings.RechargeSeconds * 0.5f);

            Assert.That(charges.GetChargeFill(0), Is.EqualTo(0.5f).Within(0.01f), "the first empty pip fills");
            Assert.That(charges.GetChargeFill(1), Is.EqualTo(0f), "the second waits its turn");
        }

        [Test]
        public void PipFill_KeptPipStaysFullWhileTheOtherRecharges()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.Tick(settings.RechargeSeconds * 0.5f);

            Assert.That(charges.GetChargeFill(0), Is.EqualTo(1f));
            Assert.That(charges.GetChargeFill(1), Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void PipFill_OutOfRangeIndexIsEmpty()
        {
            DashCharges charges = Make(out _);
            Assert.That(charges.GetChargeFill(-1), Is.EqualTo(0f));
            Assert.That(charges.GetChargeFill(2), Is.EqualTo(0f));
        }

        [Test]
        public void RefillAll_ClearsEveryTimer()
        {
            DashCharges charges = Make(out _);
            charges.TrySpend();
            charges.TrySpend();
            charges.RefillAll();
            Assert.That(charges.Available, Is.EqualTo(2));
            Assert.That(charges.NextChargeProgress, Is.EqualTo(1f));
        }
    }
}
