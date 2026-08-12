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

        /// <summary>Echo's lane: higher rarity makes the repeat hit harder.</summary>
        RepeatPower = 9,

        /// <summary>Flux's lane: higher rarity makes the roll come up more often.</summary>
        ProcChance = 10,

        /// <summary>
        /// A damage-over-time's total damage per instance. Higher rarity burns HARDER, never
        /// LONGER — DoT duration is fixed across every tier by rule, so it is deliberately not a
        /// scalable stat at all.
        /// </summary>
        DotDamage = 11,
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

        [Header("DoT lane — the slot's hits APPLY a stacking damage-over-time")]
        [SerializeField, Tooltip("The DoT type this boon's hits apply. Empty for boons with no DoT. Every damage event of the slot applies its own independent instance — one Undertow cast is three of them — so this is the whole of 'a burn boon on the vortex'. The instance's total damage is the type's base scaled by this card's rarity; its DURATION is fixed and never scales.")]
        DotDefinition dot;

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

        [Header("Echo lane — the hit happens again, later, weaker")]
        [SerializeField, Range(0f, 1f), Tooltip("Power of the repeat as a fraction of what actually landed: 0.4 = 40%. Zero disables the whole Echo lane on this boon.")]
        float repeatFraction;
        [SerializeField, Tooltip("How long the repeat is owed for. Long enough to read as a separate beat rather than a double-hit.")]
        float repeatDelaySeconds = 0.8f;
        [SerializeField, Tooltip("Cadence: repeat every Nth qualifying hit. 1 repeats every one. The counter runs across the whole fight rather than resetting per combo.")]
        int repeatEveryNHits = 1;
        [SerializeField, Tooltip("Standing Wave's lane: count EVERY instance of damage the player deals rather than one ability slot's. Leave off for slot-scoped echoes.")]
        bool repeatAnyAbility;

        [Header("Flux lane — chance-based, rolled from a stream derived off the run")]
        [SerializeField, Range(0f, 1f), Tooltip("Chance the slot's hit multiplies. Zero disables the whole Flux lane on this boon.")]
        float procChance;
        [SerializeField, Tooltip("What a successful roll multiplies the finished damage by. 3 = Noise Floor's crit.")]
        float procMultiplier = 3f;
        [SerializeField, Tooltip("Extra hitstop on a successful roll, so a crit lands like one instead of feeling like a jab that happened to print a big number.")]
        float procHitstopBonus = 0.05f;
        [SerializeField, Range(0f, 1f), Tooltip("Skip Frame: chance a Blink refunds its own charge. Not a hit modifier — it patches the dash.")]
        float dashRefundChance;

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
        public DotDefinition Dot => dot;

        /// <summary>
        /// Total damage one applied instance deals, at this rarity. Rarity scales the DAMAGE and
        /// never the duration — <see cref="DotDefinition.DurationSeconds"/> is not reachable from
        /// any scaling path, so the rule cannot be broken from here.
        /// </summary>
        public float ScaledDotDamage(float rarityScalar) =>
            dot != null ? Scale(ScaledStat.DotDamage, dot.BaseTotalDamage, rarityScalar) : 0f;
        public float IFrameBonus => iFrameBonus;
        public float DodgeGraceBonus => dodgeGraceBonus;
        public int ShieldEveryNHits => shieldEveryNHits;
        public float ShieldRegenSeconds => shieldRegenSeconds;
        public float RepeatFraction => repeatFraction;
        public float RepeatDelaySeconds => repeatDelaySeconds;
        public int RepeatEveryNHits => Mathf.Max(1, repeatEveryNHits);
        public bool RepeatAnyAbility => repeatAnyAbility;
        public float ProcChance => procChance;
        public float ProcMultiplier => procMultiplier;
        public float ProcHitstopBonus => procHitstopBonus;
        public float DashRefundChance => dashRefundChance;

        /// <summary>
        /// The repeat's power, clamped at 100%.
        ///
        /// <para>An echo louder than the sound that made it is wrong twice over. BOONS.md §2 makes
        /// Echo "weak per-instance, compounding over a fight" — the lane's identity is volume, not
        /// spikes — and §7 reserves "repeats at 100% power instead of reduced" as the payoff of the
        /// Mara + Denny Resonance. An Epic that sailed past full power would quietly deliver an
        /// unbuilt feature's reward.</para>
        /// </summary>
        public float ScaledRepeatFraction(float rarityScalar) =>
            Mathf.Min(1f, Scale(ScaledStat.RepeatPower, repeatFraction, rarityScalar));

        /// <summary>
        /// The rolled chance. Clamped at 0.9 however high the rarity scalar goes: a Flux boon that
        /// always procs has stopped being variance and become a flat damage boon, which is
        /// Overclock's job and would collapse the giver's whole identity.
        /// </summary>
        public float ScaledProcChance(float rarityScalar) =>
            Mathf.Min(0.9f, Scale(ScaledStat.ProcChance, procChance, rarityScalar));

        public float ScaledDashRefundChance(float rarityScalar) =>
            Mathf.Min(0.9f, Scale(ScaledStat.ProcChance, dashRefundChance, rarityScalar));

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

        /// <summary>
        /// The marked stat as one card line, at the ACTUAL value the given rarity delivers —
        /// the card must never quote the Normal number for a Rare roll. Formatting lives here
        /// beside the data so the label and the effect cannot drift apart.
        /// </summary>
        public string ScaledStatLabel(float rarityScalar)
        {
            switch (scaledStat)
            {
                case ScaledStat.DamageBonus:
                    return $"+{Mathf.RoundToInt(Scale(ScaledStat.DamageBonus, damageBonus, rarityScalar) * 100f)}% DAMAGE";
                case ScaledStat.PoiseBonus:
                    return $"+{Mathf.RoundToInt(Scale(ScaledStat.PoiseBonus, poiseBonus, rarityScalar) * 100f)}% ARMOR DECAY";
                case ScaledStat.StatusSeconds:
                    return $"{StatusName(status)} {Scale(ScaledStat.StatusSeconds, statusSeconds, rarityScalar):0.#}s";
                case ScaledStat.IFrameBonus:
                    return $"+{Mathf.RoundToInt(Scale(ScaledStat.IFrameBonus, iFrameBonus, rarityScalar) * 100f)}% I-FRAMES";
                case ScaledStat.DodgeGraceBonus:
                    return $"+{Mathf.RoundToInt(Scale(ScaledStat.DodgeGraceBonus, dodgeGraceBonus, rarityScalar) * 100f)}% SPLIT-SECOND WINDOW";
                case ScaledStat.ShieldProcRate:
                    return $"SHIELD EVERY {ScaledShieldEveryNHits(rarityScalar)} HITS";
                case ScaledStat.ShieldRegenRate:
                    return $"SHIELD EVERY {ScaledShieldRegenSeconds(rarityScalar):0.#}s";
                case ScaledStat.VsStatusDamageBonus:
                    return $"+{Mathf.RoundToInt(Scale(ScaledStat.VsStatusDamageBonus, vsStatusDamageBonus, rarityScalar) * 100f)}% DAMAGE VS {StatusName(vsStatus)}";
                case ScaledStat.VsStatusPoiseBonus:
                    return $"+{Mathf.RoundToInt(Scale(ScaledStat.VsStatusPoiseBonus, vsStatusPoiseBonus, rarityScalar) * 100f)}% ARMOR BREAK VS {StatusName(vsStatus)}";
                case ScaledStat.RepeatPower:
                    return repeatEveryNHits > 1
                        ? $"EVERY {Ordinal(repeatEveryNHits)} HIT REPEATS AT {Mathf.RoundToInt(ScaledRepeatFraction(rarityScalar) * 100f)}%"
                        : $"REPEATS AT {Mathf.RoundToInt(ScaledRepeatFraction(rarityScalar) * 100f)}%";
                case ScaledStat.DotDamage:
                    // The duration is quoted alongside the damage on purpose: it is the number that
                    // does NOT move between tiers, and the card should make that visible rather
                    // than leave a player assuming an Epic also lasts longer.
                    return dot != null
                        ? $"{dot.DisplayName} {ScaledDotDamage(rarityScalar):0.#} OVER {dot.DurationSeconds:0.#}s"
                        : string.Empty;
                case ScaledStat.ProcChance:
                    // Flux must telegraph its odds on the card (BOONS.md §5) — a variance giver
                    // whose distribution is invisible is just a worse damage giver.
                    return dashRefundChance > 0f
                        ? $"{Mathf.RoundToInt(ScaledDashRefundChance(rarityScalar) * 100f)}% CHANCE TO REFUND THE BLINK"
                        : $"{Mathf.RoundToInt(ScaledProcChance(rarityScalar) * 100f)}% CHANCE OF {procMultiplier:0.#}x DAMAGE";
                default:
                    return string.Empty;
            }
        }

        /// <summary>"3rd", not "3th". A card the player reads has to read like English.</summary>
        static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13)
                return n + "th";

            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        static string StatusName(StatusEffect effect)
        {
            switch (effect)
            {
                case StatusEffect.Burning: return "FRAY";
                case StatusEffect.Chilled: return "CHILL";
                case StatusEffect.Rooted: return "ROOT";
                default: return effect.ToString().ToUpperInvariant();
            }
        }

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
