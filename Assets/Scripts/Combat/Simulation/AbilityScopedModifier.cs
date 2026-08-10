using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A hit modifier that applies only to hits thrown by one ability slot. This is what makes
    /// "Overclock ATK" mean the combo and nothing else: the filter reads the attack's ability
    /// tag, never a button, so input rebinding cannot break a boon.
    ///
    /// <para><see cref="AbilityId.None"/> as the scope means "every hit" — the escape hatch for
    /// future global passives.</para>
    /// </summary>
    public sealed class AbilityScopedModifier : IHitModifier
    {
        readonly AbilityId scope;
        readonly float damageMultiplier;
        readonly float poiseMultiplier;
        readonly StatusEffect? status;
        readonly float statusSeconds;

        public AbilityScopedModifier(
            AbilityId scope,
            float damageMultiplier,
            float poiseMultiplier,
            StatusEffect? status,
            float statusSeconds)
        {
            this.scope = scope;
            this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
            this.poiseMultiplier = Mathf.Max(0f, poiseMultiplier);
            this.status = status;
            this.statusSeconds = Mathf.Max(0f, statusSeconds);
        }

        /// <summary>Runs with the elemental boons: after setup modifiers, before final-say ones.</summary>
        public int Order => 50;

        public void Modify(ref HitContext context)
        {
            if (!AppliesTo(context.Attack))
                return;

            context.Damage *= damageMultiplier;
            context.PoiseDamage *= poiseMultiplier;

            if (status.HasValue && statusSeconds > 0f && context.Target != null)
                context.Target.Statuses.Apply(status.Value, statusSeconds);
        }

        bool AppliesTo(IAttackDefinition attack)
        {
            if (scope == AbilityId.None)
                return true;

            return attack is IAbilityTagged tagged && tagged.Ability == scope;
        }
    }
}
