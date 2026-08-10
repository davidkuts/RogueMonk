using System.IO;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Core.Economy
{
    /// <summary>
    /// The player's wallet as a scene object: owns the engine-free <see cref="Economy.Wallet"/>
    /// and the persistence of its meta half. Hours and Amber are loaded on Awake and written
    /// through on every change — a roguelike's meta currency must survive a crash as surely as
    /// a clean quit, and the amounts are tiny enough that writing a few dozen bytes per boss
    /// kill costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        const string SaveFileName = "wallet.json";

        public Wallet Wallet { get; } = new Wallet();

        string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        void Awake()
        {
            Load();
            Wallet.Changed += OnWalletChanged;
        }

        void OnDestroy() => Wallet.Changed -= OnWalletChanged;

        void OnWalletChanged(CurrencyType currency, int balance)
        {
            if (currency == CurrencyType.Hours || currency == CurrencyType.Amber)
                Save();
        }

        void Load()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return;

                var data = JsonUtility.FromJson<PersistentWalletData>(File.ReadAllText(SavePath));
                data?.ApplyTo(Wallet);
                GameLog.Info(LogCategory.Core,
                    $"wallet loaded  {Wallet.Get(CurrencyType.Hours)} Hours, {Wallet.Get(CurrencyType.Amber)} Amber");
            }
            catch (System.Exception e)
            {
                // A corrupt save must never brick the game; starting the meta at zero beats
                // an exception loop in Awake.
                GameLog.Error(LogCategory.Core, $"wallet load failed ({e.Message}) - starting empty");
            }
        }

        void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(PersistentWalletData.FromWallet(Wallet)));
            }
            catch (System.Exception e)
            {
                GameLog.Error(LogCategory.Core, $"wallet save failed: {e.Message}");
            }
        }
    }
}
