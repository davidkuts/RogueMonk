using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What each status actually does, globally.
    ///
    /// The magnitudes live here rather than on the boon that applies them, so a chill always slows
    /// by the same amount whatever inflicted it. That keeps the statuses learnable — the player
    /// reads "slowed" once and knows what it costs — and it keeps boons to the one decision that is
    /// theirs: <em>whether</em> to apply a status, not how hard it bites.
    ///
    /// ⚠️ Burning's magnitudes are NO LONGER HERE (M22B). Burn became a stacking damage-over-time,
    /// and the whole point of that model is that each instance carries its own total, scaled by the
    /// rarity of the boon that applied it — a single global rate cannot express it. What survives
    /// here is the tint: the flag says a body is burning, this says what that looks like. The damage
    /// lives on <see cref="DotDefinition"/>.
    ///
    /// <see cref="StatusEffectContainer"/> deliberately stores only durations for the same reason.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Status Settings", fileName = "StatusSettings")]
    public sealed class StatusSettings : ScriptableObject
    {
        [Header("Chilled")]
        [SerializeField, Range(0f, 1f), Tooltip("Move speed multiplier while Chilled. The point is to make a runner catchable, not to freeze it.")]
        float chillMoveSpeedMultiplier = 0.45f;

        [Header("Rooted")]
        [SerializeField, Range(0f, 1f), Tooltip("Move speed multiplier while Rooted. Zero pins the enemy in place — it can still attack, so this is control rather than a stun.")]
        float rootMoveSpeedMultiplier;

        [Header("Presentation")]
        [SerializeField] Color burningTint = new Color(1f, 0.45f, 0.15f);
        [SerializeField] Color chilledTint = new Color(0.45f, 0.8f, 1f);
        [SerializeField] Color rootedTint = new Color(0.5f, 0.85f, 0.35f);
        [SerializeField, Tooltip("Fray's decay lane. Sicklier and browner than Nature's green so a decaying body does not read as a healthy one.")]
        Color decayingTint = new Color(0.62f, 0.72f, 0.35f);

        public float ChillMoveSpeedMultiplier => chillMoveSpeedMultiplier;
        public float RootMoveSpeedMultiplier => rootMoveSpeedMultiplier;

        /// <summary>
        /// Combined move-speed multiplier for whatever is currently active. Root outranks chill:
        /// they do not multiply, or two statuses would make an enemy slower than being pinned.
        /// </summary>
        public float MoveSpeedMultiplier(StatusEffectContainer statuses)
        {
            if (statuses == null)
                return 1f;

            if (statuses.Has(StatusEffect.Rooted))
                return rootMoveSpeedMultiplier;

            if (statuses.Has(StatusEffect.Chilled))
                return chillMoveSpeedMultiplier;

            return 1f;
        }

        /// <summary>The tint for the strongest active status, or null when there is none.</summary>
        public Color? Tint(StatusEffectContainer statuses)
        {
            if (statuses == null)
                return null;

            // Ordered by how much the player needs to know about it.
            if (statuses.Has(StatusEffect.Burning)) return burningTint;
            if (statuses.Has(StatusEffect.Decaying)) return decayingTint;
            if (statuses.Has(StatusEffect.Rooted)) return rootedTint;
            if (statuses.Has(StatusEffect.Chilled)) return chilledTint;
            return null;
        }
    }
}
