using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Watches the hits of one ability slot and owes a scaled repeat of every Nth one.
    ///
    /// <para>Runs at Order 90 — deliberately after the damage boons at 50 and the conditional
    /// bonuses at 58, because Echo repeats <em>what actually landed</em>. A repeat computed before
    /// Overclock multiplied the hit would quietly punish stacking the two givers, which is the
    /// opposite of "compounding over a fight".</para>
    ///
    /// <para>It reads the context and schedules; it never writes to it. The repeat is a separate
    /// damage instance arriving later, not a change to the hit in flight.</para>
    /// </summary>
    public sealed class EchoRepeatModifier : IHitModifier
    {
        readonly AbilityId scope;
        readonly bool anyAbility;
        readonly float fraction;
        readonly float delaySeconds;
        readonly int everyNHits;
        readonly EchoRepeatScheduler scheduler;

        int landed;

        /// <summary>
        /// </summary>
        /// <param name="scope">The slot whose hits echo.</param>
        /// <param name="anyAbility">True for the PASSIVE lane (Standing Wave), which counts every
        /// instance of damage the player deals rather than one slot's.</param>
        /// <param name="fraction">Power of the repeat, 0.4 = 40% of what landed.</param>
        /// <param name="delaySeconds">How long the repeat is owed for.</param>
        /// <param name="everyNHits">Cadence. 1 repeats every qualifying hit.</param>
        public EchoRepeatModifier(
            AbilityId scope, bool anyAbility, float fraction, float delaySeconds,
            int everyNHits, EchoRepeatScheduler scheduler)
        {
            this.scope = scope;
            this.anyAbility = anyAbility;
            this.fraction = Mathf.Max(0f, fraction);
            this.delaySeconds = Mathf.Max(0f, delaySeconds);
            this.everyNHits = Mathf.Max(1, everyNHits);
            this.scheduler = scheduler;
        }

        public int Order => 90;

        public void Modify(ref HitContext context)
        {
            if (scheduler == null || context.Target == null || context.Damage <= 0f)
                return;

            if (!anyAbility && !(context.Attack is IAbilityTagged tagged && tagged.Ability == scope))
                return;

            // The counter runs across the whole fight rather than resetting per combo: "every 4th
            // instance of any damage" is a rhythm the player feels over a room, and resetting it
            // on any pause would make it fire almost never.
            landed++;
            if (landed % everyNHits != 0)
                return;

            scheduler.Schedule(
                context.Target, context.Damage * fraction, context.DamageType, delaySeconds);
        }
    }
}
