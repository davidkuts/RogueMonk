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
    /// Which of a boon's numbers its rarity scales. Every boon marks exactly ONE (human rule
    /// 2026-08-11): the quality upgrade does not care what the boon is — it multiplies the
    /// marked stat by the rarity scalar (×1 / ×1.5 / ×2 of base) and touches nothing else.
    /// </summary>
    public enum ScaledStat
    {
        DamageBonus = 0,
        PoiseBonus = 1,
        StatusSeconds = 2,
        IFrameBonus = 3,
        DodgeGraceBonus = 4,

        /// <summary>Guard High's cadence: higher rarity divides the hits-per-shield count.</summary>
        ShieldProcRate = 5,

        /// <summary>Stay Standing's cadence: higher rarity divides the regen interval.</summary>
        ShieldRegenRate = 6,

        VsStatusDamageBonus = 7,
        VsStatusPoiseBonus = 8,
    }

    /// <summary>
    /// One transmission boon: a stat patch on one ability slot, offered in the draft a
    /// Transmission reward opens. Capsule-phase scaffolding — only stat modifiers riding the
    /// existing hit pipeline (plus the Ward patches, which ride the dash and the shield).
    ///
    /// <para>All numbers are the NORMAL-tier values. The offer's rolled rarity scales the ONE
    /// stat marked by <see cref="ScaledStat"/> through <see cref="RarityScalarSettings"/> at
    /// grant time; every other number stays at base — rarity scales the marked number, never
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
        [SerializeField, Tooltip("The ONE stat this boon's rarity scales. Everything else stays at its base value whatever the quality.")]
        ScaledStat scaledStat = ScaledStat.DamageBonus;

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
        [SerializeField, Tooltip("Ward's Stay Standing: the one-hit shield re-arms this many seconds after it is lost. 0 disables. Rarity divides the interval.")]
        float shieldRegenSeconds;

        [Header("Conditional bonus (vs a status the target carries)")]
        [SerializeField, Tooltip("The status the target must be under for the bonus below. Only read when a bonus is non-zero.")]
        StatusEffect vsStatus = StatusEffect.Burning;
        [SerializeField, Tooltip("Added damage fraction against targets under vsStatus. Fray's Entropy Field lane.")]
        float vsStatusDamageBonus;
        [SerializeField, Tooltip("Added poise/armor-break fraction against targets under vsStatus.")]
        float vsStatusPoiseBonus;

        public GiverId Giver => giver;
        public AbilityId Ability => ability;
        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public ScaledStat Scaled => scaledStat;
        public float DamageBonus => damageBonus;
        public float PoiseBonus => poiseBonus;
        public StatusEffect Status => status;
        public float StatusSeconds => statusSeconds;
        public float IFrameBonus => iFrameBonus;
        public float DodgeGraceBonus => dodgeGraceBonus;
        public int ShieldEveryNHits => shieldEveryNHits;
        public float ShieldRegenSeconds => shieldRegenSeconds;
        public StatusEffect VsStatus => vsStatus;
        public float VsStatusDamageBonus => vsStatusDamageBonus;
        public float VsStatusPoiseBonus => vsStatusPoiseBonus;

        /// <summary>The marked stat gets the scalar; every other number is handed back at base.</summary>
        float Scale(ScaledStat stat, float value, float rarityScalar) =>
            scaledStat == stat ? value * rarityScalar : value;

        public float ScaledIFrameBonus(float rarityScalar) => Scale(ScaledStat.IFrameBonus, iFrameBonus, rarityScalar);

        public float ScaledDodgeGraceBonus(float rarityScalar) => Scale(ScaledStat.DodgeGraceBonus, dodgeGraceBonus, rarityScalar);

        public float ScaledVsStatusDamageBonus(float rarityScalar) => Scale(ScaledStat.VsStatusDamageBonus, vsStatusDamageBonus, rarityScalar);

        public float ScaledVsStatusPoiseBonus(float rarityScalar) => Scale(ScaledStat.VsStatusPoiseBonus, vsStatusPoiseBonus, rarityScalar);

        /// <summary>Cadence stat: rarity DIVIDES the count so a better card shields more often.</summary>
        public int ScaledShieldEveryNHits(float rarityScalar) =>
            scaledStat == ScaledStat.ShieldProcRate
                ? Mathf.Max(2, Mathf.RoundToInt(shieldEveryNHits / Mathf.Max(0.01f, rarityScalar)))
                : shieldEveryNHits;

        /// <summary>Cadence stat: rarity DIVIDES the interval so a better card re-arms sooner.</summary>
        public float ScaledShieldRegenSeconds(float rarityScalar) =>
            scaledStat == ScaledStat.ShieldRegenRate
                ? shieldRegenSeconds / Mathf.Max(0.01f, rarityScalar)
                : shieldRegenSeconds;

        /// <summary>Builds the runtime hit modifier at a rarity, or null for a boon with no hit-side effect.</summary>
        public AbilityScopedModifier CreateModifier(float rarityScalar)
        {
            if (damageBonus <= 0f && poiseBonus <= 0f && statusSeconds <= 0f)
                return null;

            float seconds = Scale(ScaledStat.StatusSeconds, statusSeconds, rarityScalar);
            return new AbilityScopedModifier(
                ability,
                1f + Scale(ScaledStat.DamageBonus, damageBonus, rarityScalar),
                1f + Scale(ScaledStat.PoiseBonus, poiseBonus, rarityScalar),
                seconds > 0f ? status : (StatusEffect?)null,
                seconds);
        }
    }
}
