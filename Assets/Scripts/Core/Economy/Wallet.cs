using System;
using System.Collections.Generic;

namespace Game.Core.Economy
{
    /// <summary>
    /// Engine-free currency store for one player. Holds all four denominations; scope rules
    /// (what resets when a run ends, what persists to disk) are enforced by the callers that
    /// own those moments — the wallet itself only refuses the operations that must never
    /// exist, like negative amounts or cross-currency conversion (there is deliberately no
    /// exchange API of any kind).
    /// </summary>
    public sealed class Wallet
    {
        readonly Dictionary<CurrencyType, int> balances = new Dictionary<CurrencyType, int>
        {
            { CurrencyType.Seconds, 0 },
            { CurrencyType.Minutes, 0 },
            { CurrencyType.Hours, 0 },
            { CurrencyType.Amber, 0 },
        };

        /// <summary>Raised after any balance change, with the currency and its new balance.</summary>
        public event Action<CurrencyType, int> Changed;

        public int Get(CurrencyType currency) => balances[currency];

        /// <summary>Adds to a balance. Zero or negative amounts are ignored, not errors.</summary>
        public void Add(CurrencyType currency, int amount)
        {
            if (amount <= 0)
                return;

            balances[currency] += amount;
            Changed?.Invoke(currency, balances[currency]);
        }

        /// <summary>Spends from a balance. Refuses rather than going negative.</summary>
        public bool TrySpend(CurrencyType currency, int amount)
        {
            if (amount < 0 || balances[currency] < amount)
                return false;

            if (amount == 0)
                return true;

            balances[currency] -= amount;
            Changed?.Invoke(currency, balances[currency]);
            return true;
        }

        /// <summary>
        /// Directly sets a balance — for loading persistent currencies from a save. Not part
        /// of gameplay flow, which only ever adds and spends.
        /// </summary>
        public void SetForLoad(CurrencyType currency, int amount)
        {
            balances[currency] = Math.Max(0, amount);
            Changed?.Invoke(currency, balances[currency]);
        }

        /// <summary>
        /// Zeroes the run-scoped currencies (Seconds and Minutes). Called at run start and on
        /// death; Hours and Amber survive because they are the whole point of meta currency.
        /// </summary>
        public void ResetRunScoped()
        {
            balances[CurrencyType.Seconds] = 0;
            balances[CurrencyType.Minutes] = 0;
            Changed?.Invoke(CurrencyType.Seconds, 0);
            Changed?.Invoke(CurrencyType.Minutes, 0);
        }
    }
}
