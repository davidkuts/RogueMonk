using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The part of a body a hit landed on.
    ///
    /// <para>DESIGN.md's Armored tier is a bar on the <em>whole enemy</em>: strip it once and the
    /// enemy behaves as tier 1 forever. That is the right model for a fully-plated archetype, and
    /// it is left exactly as it was. It cannot describe an Ambershell, whose skull and dome are
    /// amber while its tail base and underside are not — the same enemy answering the same swing
    /// differently depending on where the player stood.</para>
    ///
    /// <para>So armour gets a second granularity rather than a replacement: the bar stays
    /// per-enemy, and this describes one collider. An enemy may have both, either, or neither.</para>
    ///
    /// <para>Expressed as a <em>reduction</em> rather than a multiplier on purpose:
    /// <c>default(HitZone)</c> then means "ordinary flesh, full damage, nothing blocked", so a hit
    /// that never looked for a zone is neutral instead of silently dealing zero.</para>
    /// </summary>
    public struct HitZone
    {
        /// <summary>Zone name, for logs and test assertions. Null on an unzoned hit.</summary>
        public string Id;

        /// <summary>0 = full damage, 0.7 = seventy percent absorbed.</summary>
        public float DamageReduction;

        /// <summary>
        /// True while this plate refuses poise damage entirely. Amber does not absorb a stagger,
        /// it declines to have one — so the poise bar is not touched at all rather than chipped.
        /// </summary>
        public bool BlocksStagger;

        /// <summary>
        /// True while the plate is intact. Drives the pull-resistance rule and the amber tint;
        /// goes false for the window after the plate is cracked.
        /// </summary>
        public bool IsArmored;

        /// <summary>Fraction of incoming damage that survives the plate. 1 on an unzoned hit.</summary>
        public float DamageMultiplier => Mathf.Clamp01(1f - DamageReduction);

        /// <summary>True when this zone changes nothing, i.e. the hit landed on ordinary flesh.</summary>
        public bool IsNeutral => DamageReduction <= 0f && !BlocksStagger && !IsArmored;
    }
}
