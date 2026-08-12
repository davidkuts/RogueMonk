using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Makes one ability slot's hits APPLY a damage-over-time. The hit is the applicator; the DoT
    /// is the damage.
    ///
    /// <para>This is the whole of "a burn boon on the Undertow". No vortex code knows about burn:
    /// the spin already resolves one hit per enemy per tick through the resolver, so three ticks
    /// land three separate instances with no extra plumbing anywhere. Attach the same boon to ATK
    /// and every punch applies one instead.</para>
    ///
    /// <para>Runs at 55 — just after <see cref="AbilityScopedModifier"/>, so the damage numbers it
    /// sits beside are already settled, and well before the final-say modifiers. Like the status
    /// application it sits next to, it fires during the pipeline rather than after the hit lands,
    /// so a later modifier vetoing the hit would still leave the DoT applied. That is existing
    /// behaviour rather than new: nothing in the game vetoes a hit today, and changing it would
    /// mean moving both this and the status application together.</para>
    /// </summary>
    public sealed class DotApplicationModifier : IHitModifier
    {
        readonly AbilityId scope;
        readonly IDotDefinition definition;
        readonly float totalDamage;

        /// <summary>
        /// <paramref name="totalDamage"/> is already rarity-scaled by the caller, the same way
        /// every other lane bakes its scalar in at grant time. Rarity buys damage inside a fixed
        /// duration, so the duration is never passed here at all — it is read off the definition.
        /// </summary>
        public DotApplicationModifier(AbilityId scope, IDotDefinition definition, float totalDamage)
        {
            this.scope = scope;
            this.definition = definition;
            this.totalDamage = Mathf.Max(0f, totalDamage);
        }

        public int Order => 55;

        /// <summary>The type this applies, for the card label and the debug overlay.</summary>
        public IDotDefinition Definition => definition;

        public void Modify(ref HitContext context)
        {
            if (definition == null || totalDamage <= 0f)
                return;

            if (!AppliesTo(context.Attack))
                return;

            if (context.Target == null || context.Target.Dots == null)
                return;

            context.Target.Dots.Apply(definition, totalDamage);
        }

        bool AppliesTo(IAttackDefinition attack)
        {
            if (scope == AbilityId.None)
                return true;

            return attack is IAbilityTagged tagged && tagged.Ability == scope;
        }
    }
}
