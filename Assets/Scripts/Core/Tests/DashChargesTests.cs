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
        public void ChargesRechargeIndependently_NotAsASharedCooldown()
        {
            // DESIGN.md: staggered independent recharge. Spending a second charge must not
            // restart or delay the first one's timer.
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.Tick(1f);
            charges.TrySpend();
            Assert.That(charges.Available, Is.EqualTo(0));

            charges.Tick(settings.RechargeSeconds - 1f + 0.01f); // first timer elapses
            Assert.That(charges.Available, Is.EqualTo(1));

            charges.Tick(1f); // second timer elapses one second later
            Assert.That(charges.Available, Is.EqualTo(2));
        }

        [Test]
        public void RefundReturnsTheMostRecentSpend()
        {
            DashCharges charges = Make(out FakeDashSettings settings);
            charges.TrySpend();
            charges.Tick(2f); // first charge is nearly back
            charges.TrySpend();

            charges.Refund();

            Assert.That(charges.Available, Is.EqualTo(1));
            // The older, nearly-complete timer must survive the refund.
            charges.Tick(settings.RechargeSeconds - 2f + 0.01f);
            Assert.That(charges.Available, Is.EqualTo(2));
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
        public void RefillAll_ClearsEveryTimer()
        {
            DashCharges charges = Make(out _);
            charges.TrySpend();
            charges.TrySpend();
            charges.RefillAll();
            Assert.That(charges.Available, Is.EqualTo(2));
        }
    }
}
