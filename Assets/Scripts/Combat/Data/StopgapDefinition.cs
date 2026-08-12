using UnityEngine;

namespace Game.Combat
{
    /// <summary>What a Stopgap does when activated.</summary>
    public enum StopgapKind
    {
        /// <summary>Stored Rewind: instant 2-second personal rewind (position + health).</summary>
        StoredRewind = 0,

        /// <summary>Pocket Freeze: a stasis burst around Cole.</summary>
        PocketFreeze = 1,

        /// <summary>
        /// Wound Spring: instant Vortex recharge.
        ///
        /// <para>⚠️ Currently DISABLED on its asset. The vortex cooldown is deliberately 0 — the
        /// Undertow is spammable, governed by the pull-immunity window instead — so there is never
        /// anything for this to refund. The logic is intact and correct; switch the asset back on
        /// if a non-zero vortex cooldown ever returns.</para>
        /// </summary>
        WoundSpring = 2,
    }

    /// <summary>
    /// One Stopgap — canned emergency time (REWARDS.md §5). This task ships grant and carry
    /// only; the dedicated activation input (D-pad reserved) and the effects are explicitly a
    /// later task, so the effect values live here already but nothing reads them yet.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Stopgap Definition", fileName = "Stopgap")]
    public sealed class StopgapDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "Stopgap";
        [SerializeField, TextArea(2, 3)] string description = "";
        [SerializeField] StopgapKind kind = StopgapKind.StoredRewind;
        [SerializeField, Tooltip("Effect magnitude: rewind seconds, freeze seconds. Unused by Wound Spring.")]
        float effectSeconds = 2f;

        [SerializeField, Tooltip("Which D-pad direction this Stopgap lives on. One of each is carried at a time, and the HUD lights this button when you hold one. Two Stopgaps must never share a direction.")]
        Game.Core.Player.StopgapSlot slot = Game.Core.Player.StopgapSlot.Down;

        [SerializeField, Tooltip("Off keeps the asset and its logic but stops it ever being granted — the same switch Recalibration and SupplyDrop use in the reward config. Wound Spring is off because the vortex cooldown is 0, so it has nothing to refund.")]
        bool enabled = true;

        [SerializeField, Tooltip("Short label drawn beside this Stopgap's button on the HUD. Falls back to the display name.")]
        string hudLabel = "";

        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public StopgapKind Kind => kind;
        public float EffectSeconds => effectSeconds;
        public Game.Core.Player.StopgapSlot Slot => slot;
        public bool Enabled => enabled;

        /// <summary>What the HUD writes beside the button. Short — it sits next to a d-pad, not in a menu.</summary>
        public string HudLabel => string.IsNullOrWhiteSpace(hudLabel) ? DisplayName.ToUpperInvariant() : hudLabel;
    }
}
