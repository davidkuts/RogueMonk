using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The six people on the channel (BOONS.md §2). Code-level stubs; giver names, voices and
    /// element flavour are presentation-layer data for a later pass.
    /// </summary>
    public enum GiverId
    {
        /// <summary>Mara — Fire. Pure damage, no riders, ever.</summary>
        Overclock = 0,

        /// <summary>Dr. Reeve — Nature. Decay: DoT and armour decay.</summary>
        Fray = 1,

        /// <summary>Percy — Ice. Control: slows, roots, stops.</summary>
        Stasis = 2,

        /// <summary>Denny — Force. Things happen twice.</summary>
        Echo = 3,

        /// <summary>Frank — Earth. Insurance: shields, wider windows, zero direct damage.</summary>
        Ward = 4,

        /// <summary>The unknown waveform — Wind. Variance.</summary>
        Flux = 5,
    }

    /// <summary>
    /// One transmission boon: a stat patch on one ability slot, offered in the 3-choice draft
    /// a Transmission reward opens. Capsule-phase scaffolding — only stat modifiers riding the
    /// existing hit pipeline (plus the Ward i-frame patch, which rides the dash instead).
    ///
    /// <para>All numbers are the NORMAL-tier values; the offer's tier scales them through
    /// <see cref="RarityScalarSettings"/> at grant time — rarity scales numbers, never
    /// mechanics. Budget rule (BOONS.md §3): a rider costs 30–50% of the damage portion, which
    /// is why a statusless Overclock ATK is +40% while Stasis ATK is +20% + slow.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Transmission Boon", fileName = "TransmissionBoon")]
    public sealed class TransmissionBoonDefinition : ScriptableObject
    {
        [SerializeField] GiverId giver = GiverId.Overclock;
        [SerializeField, Tooltip("The ability slot this boon patches. Never a button.")]
        AbilityId ability = AbilityId.ATK;
        [SerializeField] string displayName = "Boon";
        [SerializeField, TextArea(2, 3)] string description = "";

        [Header("Hit pipeline (Normal-tier values, scaled by rarity)")]
        [SerializeField, Tooltip("Added damage fraction on the slot's hits: 0.4 = +40%.")]
        float damageBonus;
        [SerializeField, Tooltip("Added poise-damage fraction on the slot's hits.")]
        float poiseBonus;
        [SerializeField, Tooltip("Status applied by the slot's hits, for statusSeconds > 0.")]
        StatusEffect status = StatusEffect.Burning;
        [SerializeField, Tooltip("Status duration at Normal tier. Zero applies nothing. Magnitude stays StatusSettings' business.")]
        float statusSeconds;

        [Header("Dash patch (Ward's lane — not a hit modifier)")]
        [SerializeField, Tooltip("Added dash i-frame fraction: 0.4 = i-frames cover 40% more of the dash. Zero for every boon that is not Ward BLINK.")]
        float iFrameBonus;
        [SerializeField, Tooltip("Added Split Second grace fraction: 0.35 = the perfect-dodge window past the i-frames grows 35%. Ward's Read the Room; zero elsewhere.")]
        float dodgeGraceBonus;

        [Header("Shield proc (Ward's lane — a counter on the pipeline)")]
        [SerializeField, Tooltip("Every Nth landed hit of this boon's slot arms the one-hit shield. 0 disables. Rarity divides N (a Rare shields more often).")]
        int shieldEveryNHits;

        public GiverId Giver => giver;
        public AbilityId Ability => ability;
        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public float DamageBonus => damageBonus;
        public float PoiseBonus => poiseBonus;
        public StatusEffect Status => status;
        public float StatusSeconds => statusSeconds;
        public float IFrameBonus => iFrameBonus;
        public float DodgeGraceBonus => dodgeGraceBonus;
        public int ShieldEveryNHits => shieldEveryNHits;

        /// <summary>Builds the runtime hit modifier at a tier, or null for a boon with no hit-side effect.</summary>
        public AbilityScopedModifier CreateModifier(float rarityScalar)
        {
            if (damageBonus <= 0f && poiseBonus <= 0f && statusSeconds <= 0f)
                return null;

            return new AbilityScopedModifier(
                ability,
                1f + damageBonus * rarityScalar,
                1f + poiseBonus * rarityScalar,
                statusSeconds > 0f ? status : (StatusEffect?)null,
                statusSeconds * rarityScalar);
        }
    }
}
