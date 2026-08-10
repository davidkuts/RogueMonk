using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Which passive a Stray carries. One kind per launch Stray; adding a Stray whose passive
    /// fits an existing kind is pure data, and only a genuinely new mechanic earns a new kind.
    /// </summary>
    public enum StrayKind
    {
        /// <summary>SO defined, no logic yet — the passive's system does not exist.</summary>
        NotYetImplemented = 0,

        /// <summary>Gauntlet Buckle: a one-hit shield after each successful Split Second.</summary>
        ShieldAfterPerfectDodge = 1,

        /// <summary>Linen Scrap: Splices rewind deeper.</summary>
        SpliceDepthBonus = 2,

        /// <summary>Displaced Tooth: bonus damage vs enemies of a tagged era.</summary>
        EraDamageBonus = 3,
    }

    /// <summary>
    /// One Stray — an object that fell out of its era and drifted in the timestream until the
    /// Second Hand snagged it (REWARDS.md §4). One equipped at a time; the passive derives
    /// from what the object was, and the origin era is the foreshadowing: the player holds a
    /// piece of Egypt hours before arriving.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Stray Definition", fileName = "Stray")]
    public sealed class StrayDefinition : ScriptableObject
    {
        [SerializeField] string displayName = "Stray";
        [SerializeField, TextArea(2, 3)] string description = "";
        [SerializeField, Tooltip("The era this object fell out of — usually NOT the biome it is found in.")]
        Era originEra = Era.None;
        [SerializeField] StrayKind kind = StrayKind.NotYetImplemented;

        [Header("Passive values (meaning depends on kind)")]
        [SerializeField, Tooltip("SpliceDepthBonus: added rewind fraction (0.25 = +25% deeper). EraDamageBonus: added damage fraction (0.15 = +15%). Unused by the shield.")]
        float value = 0.25f;
        [SerializeField, Tooltip("EraDamageBonus only: the era the bonus applies against.")]
        Era targetEra = Era.None;

        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Era OriginEra => originEra;
        public StrayKind Kind => kind;
        public float Value => value;
        public Era TargetEra => targetEra;
    }
}
