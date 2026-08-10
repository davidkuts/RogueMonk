using UnityEngine;

namespace Game.Combat
{
    /// <summary>What a Stopgap does when activated. Activation itself is a later task.</summary>
    public enum StopgapKind
    {
        /// <summary>Stored Rewind: instant 2-second personal rewind (position + health).</summary>
        StoredRewind = 0,

        /// <summary>Pocket Freeze: a stasis burst around Cole.</summary>
        PocketFreeze = 1,

        /// <summary>Wound Spring: instant Vortex recharge.</summary>
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
        [SerializeField, Tooltip("Effect magnitude for the later activation task: rewind seconds, freeze seconds. Unused by Wound Spring.")]
        float effectSeconds = 2f;

        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public StopgapKind Kind => kind;
        public float EffectSeconds => effectSeconds;
    }
}
