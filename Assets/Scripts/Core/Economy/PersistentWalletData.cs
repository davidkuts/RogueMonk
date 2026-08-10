using System;

namespace Game.Core.Economy
{
    /// <summary>
    /// The slice of the wallet that survives the loop reset, in a shape JsonUtility can
    /// serialize. PROVISIONAL: this is the project's first piece of save data, created for the
    /// reward system because nothing else needed a save yet. If a real save system arrives it
    /// should absorb this file format rather than run beside it.
    /// </summary>
    [Serializable]
    public sealed class PersistentWalletData
    {
        public int hours;
        public int amber;

        public static PersistentWalletData FromWallet(Wallet wallet) => new PersistentWalletData
        {
            hours = wallet.Get(CurrencyType.Hours),
            amber = wallet.Get(CurrencyType.Amber),
        };

        public void ApplyTo(Wallet wallet)
        {
            wallet.SetForLoad(CurrencyType.Hours, hours);
            wallet.SetForLoad(CurrencyType.Amber, amber);
        }
    }
}
