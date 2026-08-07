using UnityEngine;

namespace Game.Combat
{
    /// <summary>What a boon does to a hit. One entry per element (DESIGN.md § elemental boons).</summary>
    public enum BoonKind
    {
        /// <summary>Fire — sets the target burning, which ticks damage over time.</summary>
        Ember = 0,

        /// <summary>Ice — chills the target, halving its movement so a kiter becomes catchable.</summary>
        Frost = 1,

        /// <summary>Wind — throws the target, trading damage for control.</summary>
        Gale = 2,

        /// <summary>Earth — enormous poise damage, so armour strips and staggers arrive far sooner.</summary>
        Stone = 3,

        /// <summary>Nature — roots the target in place. It can still swing; it cannot chase.</summary>
        Bramble = 4,

        /// <summary>Force — no trick, just a bigger number and a heavier landing.</summary>
        Focus = 5,
    }

    /// <summary>
    /// One elemental boon. Tuning is data (CLAUDE.md rule 2); the behaviour is chosen by
    /// <see cref="Kind"/> and built by <see cref="CreateModifier"/>.
    ///
    /// Boons are offensive only for now: the modifier pipeline is wired on the *attacker's*
    /// resolver, and the player's outgoing hits are the ones that flow through theirs. A defensive
    /// boon would have to register on every enemy resolver instead, which is a different job.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Boon Definition", fileName = "Boon")]
    public sealed class BoonDefinition : ScriptableObject
    {
        [SerializeField] BoonKind kind = BoonKind.Ember;
        [SerializeField] string displayName = "Boon";
        [SerializeField, TextArea(2, 3), Tooltip("Shown on the offer card. Say what it does in plain language, not in stats.")]
        string description = "";
        [SerializeField, Tooltip("Damage type stamped onto hits, which drives spark colour and any future resistances.")]
        DamageType damageType = DamageType.Physical;
        [SerializeField, Tooltip("Card and HUD colour. Saturated hues are gameplay information, and a boon is exactly that.")]
        Color tint = Color.white;

        [Header("Magnitude")]
        [SerializeField, Tooltip("Damage multiplier applied to every hit.")]
        float damageMultiplier = 1f;
        [SerializeField, Tooltip("Poise damage multiplier. Stone lives here.")]
        float poiseMultiplier = 1f;
        [SerializeField, Tooltip("Knockback multiplier. Gale lives here.")]
        float knockbackMultiplier = 1f;
        [SerializeField, Tooltip("Extra hitstop, in seconds.")]
        float hitstopBonus;

        [Header("Status")]
        [SerializeField, Tooltip("Status applied on hit. How hard it bites is StatusSettings' business, not the boon's.")]
        StatusEffect status = StatusEffect.Burning;
        [SerializeField, Tooltip("How long the status lasts. Zero applies nothing.")]
        float statusSeconds;

        public BoonKind Kind => kind;
        public string Id => name;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public DamageType DamageType => damageType;
        public Color Tint => tint;

        /// <summary>Builds the runtime effect. One instance per run — boons are not shared state.</summary>
        public BoonModifier CreateModifier() => new BoonModifier(
            damageType,
            damageMultiplier,
            poiseMultiplier,
            knockbackMultiplier,
            hitstopBonus,
            statusSeconds > 0f ? status : (StatusEffect?)null,
            statusSeconds);
    }
}
