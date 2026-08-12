using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What a hit actually did, after everything had its say.
    ///
    /// <para>Distinct from <see cref="HitContext"/> on purpose. The context is what the attacker
    /// <em>intended</em>; this is what the target <em>received</em>, which is a different number
    /// whenever an amber plate reduced it or a damage gate refused it outright. A floating number
    /// quoting the intended damage over an enemy that took none would be worse than no number at
    /// all — it would be a lie told in the one place the player is looking.</para>
    /// </summary>
    public readonly struct DamageReport
    {
        /// <summary>Health actually lost. Zero when the hit was refused.</summary>
        public readonly float Amount;

        /// <summary>Where to draw it.</summary>
        public readonly Vector3 Point;

        /// <summary>What element landed, for the colour.</summary>
        public readonly DamageType Type;

        /// <summary>True when a damage gate turned the hit away, so the feedback can say so.</summary>
        public readonly bool Refused;

        /// <summary>
        /// The hit's hitstop, carried raw rather than pre-classified as "heavy". What counts as a
        /// heavy hit is a tuning value and belongs in the presentation layer that draws it, not
        /// baked into the body that reports it.
        /// </summary>
        public readonly float HitstopSeconds;

        /// <summary>
        /// The damage-over-time type this tick came from, or null for an ordinary hit.
        ///
        /// <para>Carried rather than inferred, because a DoT tick and a Fire hit are the same
        /// <see cref="DamageType"/> and must not look the same on screen: a tick is smaller, wears
        /// its own colour, does not punch, and is pooled into one figure a second. A presentation
        /// layer that had to guess would eventually guess wrong.</para>
        /// </summary>
        public readonly IDotDefinition Dot;

        public DamageReport(
            float amount, Vector3 point, DamageType type, bool refused, float hitstopSeconds,
            IDotDefinition dot = null)
        {
            Amount = amount;
            Point = point;
            Type = type;
            Refused = refused;
            HitstopSeconds = hitstopSeconds;
            Dot = dot;
        }
    }
}
