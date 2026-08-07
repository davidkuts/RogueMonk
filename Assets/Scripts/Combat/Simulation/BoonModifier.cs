using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A boon's effect on a hit. Engine-free, and the first thing to occupy the modifier pipeline
    /// permanently — it was built and tested in M3 and has been empty ever since.
    ///
    /// Every boon is the same shape: some multipliers, and optionally a status to apply. That is
    /// deliberate. Six bespoke modifier classes would each need their own tests and their own
    /// ordering argument, when what actually differs between fire and ice is two numbers and an
    /// enum. Anything that genuinely cannot be expressed this way earns its own type later.
    /// </summary>
    public sealed class BoonModifier : IHitModifier
    {
        readonly DamageType damageType;
        readonly float damageMultiplier;
        readonly float poiseMultiplier;
        readonly float knockbackMultiplier;
        readonly float hitstopBonus;
        readonly StatusEffect? status;
        readonly float statusSeconds;

        public BoonModifier(
            DamageType damageType,
            float damageMultiplier,
            float poiseMultiplier,
            float knockbackMultiplier,
            float hitstopBonus,
            StatusEffect? status,
            float statusSeconds)
        {
            this.damageType = damageType;
            this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
            this.poiseMultiplier = Mathf.Max(0f, poiseMultiplier);
            this.knockbackMultiplier = Mathf.Max(0f, knockbackMultiplier);
            this.hitstopBonus = Mathf.Max(0f, hitstopBonus);
            this.status = status;
            this.statusSeconds = Mathf.Max(0f, statusSeconds);
        }

        /// <summary>
        /// Boons run in the middle of the pipeline: after anything that sets a hit up, before
        /// anything that has the final say. Ties keep insertion order, so stacking two boons
        /// applies them in the order they were picked — which is the order the player expects.
        /// </summary>
        public int Order => 50;

        public void Modify(ref HitContext context)
        {
            context.Damage *= damageMultiplier;
            context.PoiseDamage *= poiseMultiplier;
            context.Knockback *= knockbackMultiplier;
            context.HitstopSeconds += hitstopBonus;

            // Physical is the default, so only claim the hit if this boon actually is elemental.
            // Otherwise a pure-damage boon would blank the element of one that ran before it.
            if (damageType != DamageType.Physical)
                context.DamageType = damageType;

            // Applied here rather than by the target, because the boon is what knows the duration.
            // The magnitude is StatusSettings' business — see the note on that type.
            if (status.HasValue && statusSeconds > 0f && context.Target != null)
                context.Target.Statuses.Apply(status.Value, statusSeconds);
        }
    }
}
