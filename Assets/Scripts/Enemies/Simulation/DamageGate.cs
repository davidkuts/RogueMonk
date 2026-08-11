using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// What a hit does against an enemy's damage gate — the riposte guard the elites wear.
    ///
    /// <para>An enemy that declares <c>OnlyDamagedBy</c> refuses all health damage until that
    /// attack lands ONCE. The first such hit deals its own damage <em>and</em> removes the guard
    /// for good (human call 2026-08-09, replacing "only the counter ever damages it"), so the
    /// guard is a lesson taught once per body rather than a shield that regrows.</para>
    ///
    /// <para>Engine-free so the rule is testable, on the <see cref="WindupInterrupt"/> precedent.
    /// It answers only "is this hit refused, and does it break the guard"; everything the refusal
    /// implies — the zeroed damage, the amber sheen, the refused burn — is applied by the callers
    /// that already own those things.</para>
    /// </summary>
    public static class DamageGate
    {
        /// <summary>The verdict for one hit against the gate.</summary>
        public readonly struct Verdict
        {
            /// <summary>True when the gate refused this hit: it deals no health damage.</summary>
            public bool Guarded { get; }

            /// <summary>True when this hit is the key that breaks the guard. It still deals its damage.</summary>
            public bool Breaking { get; }

            public Verdict(bool guarded, bool breaking)
            {
                Guarded = guarded;
                Breaking = breaking;
            }
        }

        /// <summary>
        /// True while the guard stands. Also the burn rule: a damage-over-tick bypasses the hit
        /// resolver, so without asking this separately a boon's burn would trickle past a guard
        /// that promises the health bar does not move before the counter lands.
        /// </summary>
        public static bool IsUp(IAttackDefinition key, bool alreadyBroken) =>
            key != null && !alreadyBroken;

        /// <summary>
        /// Resolves <paramref name="incoming"/> against a gate keyed on <paramref name="key"/>.
        ///
        /// <para>Matching is by <c>Id</c> rather than by reference so the gate is generic data —
        /// it names an attack, not the Riposte specifically — and an ungated enemy (null key)
        /// always returns "not guarded, not breaking" without touching the incoming hit.</para>
        /// </summary>
        public static Verdict Resolve(IAttackDefinition key, bool alreadyBroken, IAttackDefinition incoming)
        {
            if (!IsUp(key, alreadyBroken))
                return new Verdict(false, false);

            bool breaking = incoming != null && incoming.Id == key.Id;
            return new Verdict(!breaking, breaking);
        }
    }
}
