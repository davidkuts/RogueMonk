using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// One attack's frame data and payload. Starting numbers come from
    /// DESIGN.md § Attacks &amp; combo; everything here is meant to be re-tuned by hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Attack Definition", fileName = "AttackDefinition")]
    public sealed class AttackDefinition : ScriptableObject, IAttackDefinition, IAbilityTagged
    {
        [Header("Ability slot")]
        [SerializeField, Tooltip("Which player ability this attack belongs to (None for enemy attacks). Slot-scoped boons filter hits by this — never by button, which the rebinding rule forbids.")]
        AbilityId ability = AbilityId.None;

        [Header("Frame data (seconds)")]
        [SerializeField, Tooltip("Committed wind-up. Never cancellable — this is the tell.")]
        float windupSeconds = 0.10f;
        [SerializeField, Tooltip("Hitbox-live window.")]
        float activeSeconds = 0.06f;
        [SerializeField, Tooltip("Trailing recovery. Dash-cancellable at the cost of a charge.")]
        float recoverySeconds = 0.18f;
        [SerializeField, Tooltip("True for all player attacks (DESIGN.md skill-ceiling rule).")]
        bool cancellableOnRecovery = true;
        [SerializeField, Tooltip("How long after this attack ends the combo stays alive.")]
        float comboWindowSeconds = 0.40f;

        [Header("Hitbox")]
        [SerializeField]
        HitboxShape hitbox = HitboxShape.DefaultSphere;

        [Header("Payload")]
        [SerializeField] float damage = 10f;
        [SerializeField, Tooltip("Physical for the whole MVP. Present from day one for the boon system.")]
        DamageType damageType = DamageType.Physical;
        [SerializeField, Tooltip("Poise damage. The kick is worth roughly three punches.")]
        float poiseDamage = 10f;
        [SerializeField, Tooltip("Knockback impulse in m/s applied along the hit direction.")]
        float knockback = 2f;
        [SerializeField, Tooltip("Freeze on connect. ~0.06 light, ~0.10 heavy.")]
        float hitstopSeconds = 0.06f;

        [SerializeField, Tooltip("Seconds of control taken from the PLAYER when this lands. 0 for everything ordinary — reserved for hits that must land like they look. Keep it near the 0.5s post-hit i-frame window so helplessness and invulnerability end together.")]
        float hitStaggerSeconds;

        [SerializeField, Tooltip("How hard the player is thrown when this lands, in m/s. Independent of Knockback, which moves enemies.")]
        float playerKnockback;

        [Header("Auto-aim")]
        [SerializeField, Range(0f, 360f), Tooltip("Full cone width around facing that can attract aim.")]
        float autoAimConeDegrees = 45f;
        [SerializeField, Tooltip("Furthest a target can be and still attract aim.")]
        float autoAimRangeMeters = 3f;
        [SerializeField, Tooltip("Turn rate onto the target during wind-up. Never an instant snap.")]
        float aimSnapSpeedDegPerSec = 540f;

        [Header("Movement")]
        [SerializeField, Range(0f, 1f), Tooltip("Walk speed while this attack runs. 0 roots the attacker.")]
        float moveSpeedMultiplier;

        [Header("Telegraph")]
        [SerializeField, Tooltip("What this telegraph MEANS. Anything but Custom resolves its colour from the TelegraphPalette, so the 'same colour = same threat' rule is enforced in one place instead of per asset.")]
        TelegraphChannel telegraphChannel = TelegraphChannel.Custom;
        [SerializeField, Tooltip("Colour flashed during wind-up. Used only when the channel is Custom — every attack authored before the palette existed keeps exactly the colour it had.")]
        Color telegraphColor = new Color(1f, 0.25f, 0.2f, 1f);

        [SerializeField, Range(1f, 3f), Tooltip("How LOUD this tell is, independent of how long it lasts. A short wind-up can still be fair if it is unmistakable — this is what pays for a fast attack, rather than shortening the warning and hoping.")]
        float telegraphEmphasis = 1f;

        public string Id => name;
        public AbilityId Ability => ability;
        public float WindupSeconds => windupSeconds;
        public float ActiveSeconds => activeSeconds;
        public float RecoverySeconds => recoverySeconds;
        public bool CancellableOnRecovery => cancellableOnRecovery;
        public float ComboWindowSeconds => comboWindowSeconds;
        public HitboxShape Hitbox => hitbox;
        public float Damage => damage;
        public DamageType DamageType => damageType;
        public float PoiseDamage => poiseDamage;
        public float Knockback => knockback;
        public float HitstopSeconds => hitstopSeconds;
        public float AutoAimConeDegrees => autoAimConeDegrees;
        public float AutoAimRangeMeters => autoAimRangeMeters;
        public float AimSnapSpeedDegPerSec => aimSnapSpeedDegPerSec;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        /// <summary>Wind-up telegraph colour. Not part of <see cref="IAttackDefinition"/> — presentation, not simulation.</summary>
        public Color TelegraphColor => telegraphColor;

        /// <summary>
        /// The threat class this telegraph belongs to. <see cref="TelegraphChannel.Custom"/> means
        /// "use <see cref="TelegraphColor"/> as authored".
        /// </summary>
        public TelegraphChannel TelegraphChannel => telegraphChannel;

        /// <summary>
        /// How emphatically this tell is drawn. Brightness and opacity, not duration.
        ///
        /// <para>DESIGN.md's rule is that difficulty comes from decision pressure, never from
        /// shortening tells. This is the lever that lets a fast attack stay fair: the warning gets
        /// <em>louder</em> rather than longer, so a 0.45 s wind-up can still be read at a glance.</para>
        /// </summary>
        public float TelegraphEmphasis => telegraphEmphasis;

        /// <summary>
        /// Seconds of control this takes from the player. 0 for every ordinary attack.
        ///
        /// <para>Not part of <see cref="IAttackDefinition"/>: it is a reaction the player's adapter
        /// applies, not a value the hit pipeline resolves, and putting it on the interface would
        /// oblige every test fake to carry a number that only one archetype uses.</para>
        /// </summary>
        public float HitStaggerSeconds => hitStaggerSeconds;

        /// <summary>How hard the player is thrown when this lands, in m/s.</summary>
        public float PlayerKnockback => playerKnockback;

        /// <summary>Convenience for the presenters: the colour this attack should actually flash.</summary>
        public Color ResolveTelegraphColor(TelegraphPalette palette)
        {
            Color resolved = TelegraphPalette.Resolve(palette, telegraphChannel, telegraphColor);

            if (telegraphEmphasis <= 1f)
                return resolved;

            // Brightened rather than hue-shifted: the grammar says a colour means a threat class,
            // so an emphatic tell must be the same colour shouted, never a different colour.
            resolved.r *= telegraphEmphasis;
            resolved.g *= telegraphEmphasis;
            resolved.b *= telegraphEmphasis;
            return resolved;
        }

        /// <summary>Total length of the attack, for tooling and tests.</summary>
        public float TotalSeconds => windupSeconds + activeSeconds + recoverySeconds;
    }
}
