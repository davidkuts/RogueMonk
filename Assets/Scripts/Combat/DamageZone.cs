using System;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Marks one collider as a distinct place to be hit — an amber plate, or the soft seam beside
    /// it. Sits on the collider itself, never on the body, because the whole point is that two
    /// colliders on the same enemy answer the same swing differently.
    ///
    /// <para>Cracking is the escape valve the design asks for: an Ambershell baited into a wall
    /// breaks its own plating, which turns every armored zone soft for a window and then
    /// re-hardens. That is a timer here rather than a second component, so "cracked" and "intact"
    /// cannot drift out of step with what the hit resolution actually reads.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DamageZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Name used in hit logs and tests. 'dome', 'tail-base', 'flank'.")]
        string zoneId = "plate";

        [SerializeField, Tooltip("True for an amber plate. False marks a soft zone, which exists so the body has somewhere that is honestly weak.")]
        bool armored = true;

        [SerializeField, Range(0f, 1f), Tooltip("Fraction of damage the plate eats while intact. ENEMIES_BIOME1.md 3.1 asks for ~0.7 on Ambershell.")]
        float damageReduction = 0.7f;

        [SerializeField, Tooltip("Intact amber refuses poise damage outright rather than absorbing it, so a plated zone can never be staggered however long it is beaten on.")]
        bool blocksStagger = true;

        float crackedRemaining;

        /// <summary>Raised when the plate cracks or re-hardens, so the body can retint.</summary>
        public event Action<bool> CrackedChanged;

        /// <summary>True while this zone is temporarily soft after being cracked.</summary>
        public bool IsCracked => crackedRemaining > 0f;

        /// <summary>True while the plate is up: armored, uncracked, and actually reducing something.</summary>
        public bool IsIntactArmor => armored && !IsCracked;

        public string ZoneId => zoneId;

        public float CrackedRemaining => crackedRemaining;

        void Update()
        {
            if (crackedRemaining <= 0f)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            crackedRemaining -= deltaTime;
            if (crackedRemaining > 0f)
                return;

            crackedRemaining = 0f;
            GameLog.Info(LogCategory.Enemy, $"zone '{zoneId}' re-hardened");
            CrackedChanged?.Invoke(false);
        }

        /// <summary>
        /// Opens this plate for <paramref name="seconds"/>. Extends rather than restarts, so a
        /// second crack can never shorten a window the player has already earned.
        /// </summary>
        public void Crack(float seconds)
        {
            if (!armored || seconds <= 0f)
                return;

            bool wasCracked = IsCracked;
            crackedRemaining = Mathf.Max(crackedRemaining, seconds);

            if (wasCracked)
                return;

            GameLog.Info(LogCategory.Enemy, $"zone '{zoneId}' CRACKED for {crackedRemaining:0.0}s - weak point open");
            CrackedChanged?.Invoke(true);
        }

        /// <summary>
        /// Turns this zone's plating on or off at runtime.
        ///
        /// <para>For the Tyrant, whose flanks and skull <em>become</em> armoured partway through the
        /// fight — ENEMIES_BIOME1.md § 4 Phase 2, "the player must re-learn where to hit". A zone
        /// authored soft and hardened later is the same object throughout, so the hit pipeline, the
        /// tint and the pull-resistance rule all follow automatically.</para>
        /// </summary>
        public void SetArmored(bool value, float reduction = -1f)
        {
            armored = value;

            if (reduction >= 0f)
                damageReduction = Mathf.Clamp01(reduction);

            // Hardening cancels any crack: the plate is new, not repaired.
            if (value)
                crackedRemaining = 0f;

            blocksStagger = value;
            CrackedChanged?.Invoke(IsCracked);

            GameLog.Info(LogCategory.Enemy,
                $"zone '{zoneId}' {(value ? "HARDENED" : "softened")} - x{Describe().DamageMultiplier:0.00}");
        }

        /// <summary>Ends the crack window early. Used when a body is reset or pooled.</summary>
        public void ReHarden()
        {
            if (crackedRemaining <= 0f)
                return;

            crackedRemaining = 0f;
            CrackedChanged?.Invoke(false);
        }

        /// <summary>
        /// The engine-free description handed to the hit pipeline. A cracked plate reports itself
        /// as ordinary flesh, which is exactly what "temporary weak point" has to mean — one place
        /// deciding it, so the damage, the stagger and the amber tint can never disagree.
        /// </summary>
        public HitZone Describe()
        {
            if (IsCracked)
                return default;

            return new HitZone
            {
                Id = zoneId,
                DamageReduction = armored ? damageReduction : 0f,
                BlocksStagger = armored && blocksStagger,
                IsArmored = armored,
            };
        }
    }
}
