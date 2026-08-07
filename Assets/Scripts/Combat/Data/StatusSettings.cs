using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What each status actually does, globally.
    ///
    /// The magnitudes live here rather than on the boon that applies them, so Burning always burns
    /// at the same rate whatever inflicted it. That keeps the statuses learnable — the player reads
    /// "on fire" once and knows what it costs — and it keeps boons to the one decision that is
    /// theirs: <em>whether</em> to apply a status, not how hard it bites.
    ///
    /// <see cref="StatusEffectContainer"/> deliberately stores only durations for the same reason.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Status Settings", fileName = "StatusSettings")]
    public sealed class StatusSettings : ScriptableObject
    {
        [Header("Burning")]
        [SerializeField, Tooltip("Damage per second while Burning. Applied in ticks rather than continuously so it reads as a series of hits.")]
        float burnDamagePerSecond = 9f;
        [SerializeField, Tooltip("Seconds between burn ticks. Each tick is its own damage number and its own flash.")]
        float burnTickSeconds = 0.4f;

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

        public float BurnDamagePerSecond => burnDamagePerSecond;
        public float BurnTickSeconds => Mathf.Max(0.05f, burnTickSeconds);
        public float ChillMoveSpeedMultiplier => chillMoveSpeedMultiplier;
        public float RootMoveSpeedMultiplier => rootMoveSpeedMultiplier;

        /// <summary>Damage dealt by one burn tick.</summary>
        public float BurnDamagePerTick => burnDamagePerSecond * BurnTickSeconds;

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
            if (statuses.Has(StatusEffect.Rooted)) return rootedTint;
            if (statuses.Has(StatusEffect.Chilled)) return chilledTint;
            return null;
        }
    }
}
