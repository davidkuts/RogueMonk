using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Stopgaps Cole carries (REWARDS.md §5): capped at two, granted by room rewards,
    /// spent by an activation input that is deliberately NOT built yet — D-pad stays reserved
    /// for it. This component is the carry half only: count, cap, and the HUD's view of both.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StopgapInventory : MonoBehaviour
    {
        [SerializeField] StopgapSettings settings;

        readonly List<StopgapDefinition> carried = new List<StopgapDefinition>();

        public IReadOnlyList<StopgapDefinition> Carried => carried;

        public int Count => carried.Count;

        public int CarryCap => settings != null ? settings.CarryCap : 2;

        public StopgapSettings Settings => settings;

        /// <summary>Raised whenever the carried set changes, for HUD pips.</summary>
        public event Action Changed;

        /// <summary>Adds one carried Stopgap. Refuses over the cap — the reward is then wasted, visibly.</summary>
        public bool TryAdd(StopgapDefinition stopgap)
        {
            if (stopgap == null)
                return false;

            if (carried.Count >= CarryCap)
            {
                GameLog.Info(LogCategory.Combat,
                    $"STOPGAP refused  {stopgap.DisplayName} - already carrying {carried.Count}/{CarryCap}");
                return false;
            }

            carried.Add(stopgap);
            GameLog.Info(LogCategory.Combat,
                $"STOPGAP carried  {stopgap.DisplayName} ({carried.Count}/{CarryCap})");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Spends the oldest carried Stopgap. First in, first out: the player picked them up in an
        /// order and a panic press should not have to guess which one it will burn.
        /// </summary>
        public bool TryConsume(out StopgapDefinition stopgap)
        {
            stopgap = null;
            if (carried.Count == 0)
                return false;

            stopgap = carried[0];
            carried.RemoveAt(0);

            GameLog.Info(LogCategory.Combat,
                $"STOPGAP spent  {stopgap.DisplayName} ({carried.Count}/{CarryCap} left)");
            Changed?.Invoke();
            return true;
        }

        /// <summary>What the next press would spend, for the HUD. Null when nothing is carried.</summary>
        public StopgapDefinition Next => carried.Count > 0 ? carried[0] : null;

        public void ClearForNewRun()
        {
            carried.Clear();
            Changed?.Invoke();
        }
    }
}
