using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Bonus damage/poise against targets currently under a status — Fray's Entropy Field
    /// (frayed enemies take extra armor-break damage). Reads the target's own status
    /// container, so it composes with WHATEVER inflicted the status: the boon that applies it,
    /// another boon, or a future enemy effect.
    /// </summary>
    public sealed class StatusConditionalModifier : IHitModifier
    {
        readonly AbilityId scope;
        readonly StatusEffect required;
        readonly float damageMultiplier;
        readonly float poiseMultiplier;

        public StatusConditionalModifier(
            AbilityId scope, StatusEffect required, float damageMultiplier, float poiseMultiplier)
        {
            this.scope = scope;
            this.required = required;
            this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
            this.poiseMultiplier = Mathf.Max(0f, poiseMultiplier);
        }

        /// <summary>
        /// After the unconditional boons: a status-application boon at Order 50 must have its
        /// say first, or a hit that inflicts the status could not also profit from it.
        /// </summary>
        public int Order => 58;

        public void Modify(ref HitContext context)
        {
            if (context.Target == null || !context.Target.Statuses.Has(required))
                return;

            if (scope != AbilityId.None &&
                !(context.Attack is IAbilityTagged tagged && tagged.Ability == scope))
                return;

            context.Damage *= damageMultiplier;
            context.PoiseDamage *= poiseMultiplier;
        }
    }
}
