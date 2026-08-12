using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Stopgaps Cole carries (REWARDS.md §5): canned emergency time, spent on the D-pad.
    ///
    /// <para><b>One slot per direction, not a shared carry cap.</b> Each Stopgap owns a d-pad
    /// direction and holds one of itself there. That is a readability decision as much as a
    /// balance one: a pooled cap of two meant the player had to remember WHICH two they were
    /// holding, and the answer lived nowhere on screen. Now the button you would press is the
    /// slot, so the HUD can just light it.</para>
    ///
    /// <para>Four directions is the ceiling, forever. That is deliberate — panic buttons, not a
    /// hoardable resource.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StopgapInventory : MonoBehaviour
    {
        [SerializeField] StopgapSettings settings;

        /// <summary>One carried Stopgap per direction. Null means the slot is empty.</summary>
        readonly Dictionary<StopgapSlot, StopgapDefinition> slots = new Dictionary<StopgapSlot, StopgapDefinition>();

        /// <summary>Every direction, in HUD order. Iterated by the widget so it can draw empties too.</summary>
        public static readonly StopgapSlot[] AllSlots =
        {
            StopgapSlot.Up, StopgapSlot.Down, StopgapSlot.Left, StopgapSlot.Right,
        };

        public StopgapSettings Settings => settings;

        /// <summary>How many Stopgaps are held across all slots.</summary>
        public int Count
        {
            get
            {
                int held = 0;
                foreach (KeyValuePair<StopgapSlot, StopgapDefinition> entry in slots)
                {
                    if (entry.Value != null)
                        held++;
                }

                return held;
            }
        }

        /// <summary>Most that can be held at once — one per direction.</summary>
        public int CarryCap => AllSlots.Length;

        /// <summary>Raised whenever any slot changes, for the HUD.</summary>
        public event Action Changed;

        /// <summary>What is sitting on <paramref name="slot"/>, or null.</summary>
        public StopgapDefinition Get(StopgapSlot slot) =>
            slots.TryGetValue(slot, out StopgapDefinition held) ? held : null;

        public bool Has(StopgapSlot slot) => Get(slot) != null;

        /// <summary>
        /// Takes a Stopgap into its OWN slot. Refused — visibly — when that slot is already full,
        /// which is the same "the reward is wasted, and you can see it" rule the cap had.
        /// </summary>
        public bool TryAdd(StopgapDefinition stopgap)
        {
            if (stopgap == null)
                return false;

            if (Get(stopgap.Slot) != null)
            {
                GameLog.Info(LogCategory.Combat,
                    $"STOPGAP refused  {stopgap.DisplayName} - already holding one on {stopgap.Slot}");
                return false;
            }

            slots[stopgap.Slot] = stopgap;
            GameLog.Info(LogCategory.Combat,
                $"STOPGAP carried  {stopgap.DisplayName} on {stopgap.Slot} ({Count}/{CarryCap})");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Spends whatever is on <paramref name="slot"/>. False when that slot is empty.</summary>
        public bool TryConsume(StopgapSlot slot, out StopgapDefinition stopgap)
        {
            stopgap = Get(slot);
            if (stopgap == null)
                return false;

            slots[slot] = null;
            GameLog.Info(LogCategory.Combat,
                $"STOPGAP spent  {stopgap.DisplayName} from {slot} ({Count}/{CarryCap} left)");
            Changed?.Invoke();
            return true;
        }

        public void ClearForNewRun()
        {
            slots.Clear();
            Changed?.Invoke();
        }
    }
}
