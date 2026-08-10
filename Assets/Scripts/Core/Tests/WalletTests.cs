using Game.Core.Economy;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// The wallet: four denominations, no conversion API by construction, run-scoped reset
    /// touches only Seconds and Minutes, and the persistent slice round-trips.
    /// </summary>
    public class WalletTests
    {
        [Test]
        public void AddAndSpendKeepDenominationsSeparate()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Seconds, 10);
            wallet.Add(CurrencyType.Minutes, 25);
            wallet.Add(CurrencyType.Hours, 3);
            wallet.Add(CurrencyType.Amber, 1);

            Assert.That(wallet.Get(CurrencyType.Seconds), Is.EqualTo(10));
            Assert.That(wallet.Get(CurrencyType.Minutes), Is.EqualTo(25));
            Assert.That(wallet.Get(CurrencyType.Hours), Is.EqualTo(3));
            Assert.That(wallet.Get(CurrencyType.Amber), Is.EqualTo(1));

            Assert.That(wallet.TrySpend(CurrencyType.Minutes, 20), Is.True);
            Assert.That(wallet.Get(CurrencyType.Minutes), Is.EqualTo(5));
            Assert.That(wallet.Get(CurrencyType.Seconds), Is.EqualTo(10), "spending Minutes never touches Seconds");
        }

        [Test]
        public void SpendingRefusesRatherThanGoingNegative()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Minutes, 5);

            Assert.That(wallet.TrySpend(CurrencyType.Minutes, 6), Is.False);
            Assert.That(wallet.Get(CurrencyType.Minutes), Is.EqualTo(5), "a refused spend changes nothing");
            Assert.That(wallet.TrySpend(CurrencyType.Minutes, -1), Is.False, "negative spends are refused outright");
        }

        [Test]
        public void NegativeAndZeroAddsAreIgnored()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Seconds, -5);
            wallet.Add(CurrencyType.Seconds, 0);
            Assert.That(wallet.Get(CurrencyType.Seconds), Is.EqualTo(0));
        }

        [Test]
        public void RunScopedResetSparesTheMetaCurrencies()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Seconds, 40);
            wallet.Add(CurrencyType.Minutes, 90);
            wallet.Add(CurrencyType.Hours, 7);
            wallet.Add(CurrencyType.Amber, 2);

            wallet.ResetRunScoped();

            Assert.That(wallet.Get(CurrencyType.Seconds), Is.EqualTo(0), "Seconds die with the run");
            Assert.That(wallet.Get(CurrencyType.Minutes), Is.EqualTo(0), "Minutes reset on death");
            Assert.That(wallet.Get(CurrencyType.Hours), Is.EqualTo(7), "Hours survive the loop reset");
            Assert.That(wallet.Get(CurrencyType.Amber), Is.EqualTo(2), "Amber survives the loop reset");
        }

        [Test]
        public void ChangedFiresWithTheNewBalance()
        {
            var wallet = new Wallet();
            CurrencyType? lastCurrency = null;
            int lastBalance = -1;
            wallet.Changed += (currency, balance) =>
            {
                lastCurrency = currency;
                lastBalance = balance;
            };

            wallet.Add(CurrencyType.Minutes, 12);
            Assert.That(lastCurrency, Is.EqualTo(CurrencyType.Minutes));
            Assert.That(lastBalance, Is.EqualTo(12));
        }

        [Test]
        public void PersistentSliceRoundTrips()
        {
            var wallet = new Wallet();
            wallet.Add(CurrencyType.Hours, 11);
            wallet.Add(CurrencyType.Amber, 4);
            wallet.Add(CurrencyType.Minutes, 99);

            PersistentWalletData data = PersistentWalletData.FromWallet(wallet);

            var restored = new Wallet();
            data.ApplyTo(restored);

            Assert.That(restored.Get(CurrencyType.Hours), Is.EqualTo(11));
            Assert.That(restored.Get(CurrencyType.Amber), Is.EqualTo(4));
            Assert.That(restored.Get(CurrencyType.Minutes), Is.EqualTo(0),
                "run currency is deliberately not part of the persistent slice");
        }
    }
}
