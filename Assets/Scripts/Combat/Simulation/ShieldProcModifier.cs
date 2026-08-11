using System;

namespace Game.Combat
{
    /// <summary>
    /// Ward's Guard High: every Nth landed hit of one ability slot arms the one-hit shield.
    /// A counter riding the hit pipeline rather than a stat — but still attacker-side and
    /// still scoped by ability tag, never by button. The shield itself is
    /// <see cref="PlayerHealth"/>'s; this only decides when it is earned.
    /// </summary>
    public sealed class ShieldProcModifier : IHitModifier
    {
        readonly AbilityId scope;
        readonly int hitsPerShield;
        readonly Action armShield;

        int count;

        public ShieldProcModifier(AbilityId scope, int hitsPerShield, Action armShield)
        {
            this.scope = scope;
            this.hitsPerShield = Math.Max(1, hitsPerShield);
            this.armShield = armShield;
        }

        /// <summary>Hits counted so far toward the next shield, for tests and the overlay.</summary>
        public int Count => count;

        /// <summary>After the stat boons: counting is a side effect, order is only for legibility.</summary>
        public int Order => 55;

        public void Modify(ref HitContext context)
        {
            if (scope != AbilityId.None &&
                !(context.Attack is IAbilityTagged tagged && tagged.Ability == scope))
                return;

            count++;
            if (count < hitsPerShield)
                return;

            count = 0;
            armShield?.Invoke();
        }
    }
}
