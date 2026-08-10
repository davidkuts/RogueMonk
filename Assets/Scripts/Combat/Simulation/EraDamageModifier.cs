using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Bonus damage against targets of one era — the Displaced Tooth Stray's passive
    /// (REWARDS.md §4: +damage vs enemies of the era the object was displaced from). Reads the
    /// target's era tag; a target with no tag is simply never matched.
    /// </summary>
    public sealed class EraDamageModifier : IHitModifier
    {
        readonly Era targetEra;
        readonly float damageMultiplier;

        public EraDamageModifier(Era targetEra, float damageMultiplier)
        {
            this.targetEra = targetEra;
            this.damageMultiplier = Mathf.Max(0f, damageMultiplier);
        }

        /// <summary>
        /// After the boons: an era bonus multiplies the hit the build actually produced, so it
        /// composes predictably with everything the player has stacked.
        /// </summary>
        public int Order => 60;

        public void Modify(ref HitContext context)
        {
            if (context.Target is IEraTagged tagged && tagged.Era == targetEra && targetEra != Era.None)
                context.Damage *= damageMultiplier;
        }
    }
}
