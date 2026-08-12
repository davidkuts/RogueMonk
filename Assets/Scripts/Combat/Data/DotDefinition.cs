using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// One kind of damage-over-time — burn, decay, whatever comes next. Implemented by a
    /// ScriptableObject at runtime and by fakes in tests, for the same reason
    /// <see cref="IAttackDefinition"/> is an interface: the simulation must never depend on an
    /// asset type.
    /// </summary>
    public interface IDotDefinition
    {
        string Id { get; }
        string DisplayName { get; }

        /// <summary>What element the ticks read as. Only used for logs and reports.</summary>
        DamageType DamageType { get; }

        /// <summary>
        /// How long ONE instance lasts. Constant across every rarity tier, by rule — rarity buys
        /// more damage in the same window, never a longer window (human rule 2026-08-12). No
        /// rarity-scaling API touches this value, so the rule holds by construction.
        /// </summary>
        float DurationSeconds { get; }

        /// <summary>Total damage one NORMAL-tier instance deals across its whole duration.</summary>
        float BaseTotalDamage { get; }

        /// <summary>Most concurrent instances of this type on one target. Zero is uncapped.</summary>
        int MaxStacks { get; }

        /// <summary>Colour of this type's floating number.</summary>
        Color NumberColor { get; }

        /// <summary>
        /// The status flag raised while any instance of this type is alive.
        ///
        /// <para>Presence only — the flag carries no damage of its own. It exists so
        /// <see cref="StatusConditionalModifier"/> ("+damage vs burning") and
        /// <see cref="StatusSettings"/>'s body tint keep answering the same question they always
        /// did, while the damage half of the mechanic moves into <see cref="DotContainer"/>.</para>
        /// </summary>
        StatusEffect StatusFlag { get; }
    }

    /// <summary>
    /// A damage-over-time type as data, so a new one is a content addition rather than code.
    ///
    /// <para><b>Every application is its own instance.</b> Reapplying never refreshes or resets
    /// what is already burning: one Undertow cast carrying a burn boon lands three separate
    /// instances, each expiring on its own clock, and each chipping in parallel. That is the whole
    /// reason this exists instead of the old <see cref="StatusEffect.Burning"/> model, which stored
    /// a single duration and scaled it by rarity.</para>
    ///
    /// <para><b>Rarity scales damage, never duration.</b> Total damage is the Normal-tier figure
    /// here, multiplied at grant time through the shared <see cref="RarityScalarSettings"/>
    /// (×1.0 / ×1.5 / ×2.0 of base, additive rather than compounding). Duration is authored once
    /// and every tier gets the same seconds — a Rare burn hurts more per second, not for longer.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/DoT Definition", fileName = "Dot")]
    public sealed class DotDefinition : ScriptableObject, IDotDefinition
    {
        [SerializeField, Tooltip("Shown on a boon card. The asset name is the identifier.")]
        string displayName = "BURN";

        [SerializeField, Tooltip("What the ticks read as in logs and reports. Presentation only — the number's colour comes from this asset, not from the damage-type palette.")]
        DamageType damageType = DamageType.Fire;

        [SerializeField, Tooltip("How long ONE instance lasts. THE SAME AT EVERY RARITY, by rule: a better card deals more damage in this window, never a longer one.")]
        float durationSeconds = 4f;

        [SerializeField, Tooltip("Total damage one NORMAL-tier instance deals over its whole duration. Rare and Epic multiply this by 1.5 and 2 through the shared rarity scalars.")]
        float baseTotalDamage = 4f;

        [SerializeField, Tooltip("Most concurrent instances on one target. ZERO IS UNCAPPED, which is the shipped setting — this is a safety valve for future scaling, not active balance.")]
        int maxStacks;

        [SerializeField, Tooltip("Colour of this type's floating numbers. Must stay clear of the reserved set: amber is Armored/hardened time, the dash blue is the player's time kit, and venom green is the Sailspit's floor.")]
        Color numberColor = new Color(1f, 0.45f, 0.15f, 1f);

        [SerializeField, Tooltip("The status flag raised while any instance is alive. Presence only — it carries no damage. Keeps 'bonus damage vs burning' boons and the body tint working.")]
        StatusEffect statusFlag = StatusEffect.Burning;

        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public DamageType DamageType => damageType;
        public float DurationSeconds => Mathf.Max(0.01f, durationSeconds);
        public float BaseTotalDamage => Mathf.Max(0f, baseTotalDamage);
        public int MaxStacks => Mathf.Max(0, maxStacks);
        public Color NumberColor => numberColor;
        public StatusEffect StatusFlag => statusFlag;
    }
}
