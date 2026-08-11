using Game.Core.Rng;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The unknown waveform's lane: a hit that sometimes hits much harder.
    ///
    /// <para>BOONS.md §5 gives Flux "EV parity with other givers; distribution is the identity" —
    /// Noise Floor (15% for 3×) and Dropped Packet (50% for 2×) are the same modifier with
    /// different numbers.</para>
    ///
    /// <para><b>The roll comes from a stream derived off the run, never the run stream itself.</b>
    /// How many times this rolls depends entirely on how the player fights, so drawing from the
    /// run stream would desynchronise every later draw and a quoted seed would stop reproducing
    /// its own level. DESIGN.md settled this for boss move selection and M16 settled it again for
    /// reward content; this is the third instance of the same rule.</para>
    /// </summary>
    public sealed class ChanceCritModifier : IHitModifier
    {
        readonly AbilityId scope;
        readonly float chance;
        readonly float multiplier;
        readonly float hitstopBonus;
        readonly IRandomSource stream;

        /// <summary>Rolls that came up, for the log and the debug overlay.</summary>
        public int Procs { get; private set; }

        public ChanceCritModifier(
            AbilityId scope, float chance, float multiplier, float hitstopBonus, IRandomSource stream)
        {
            this.scope = scope;
            this.chance = Mathf.Clamp01(chance);
            this.multiplier = Mathf.Max(1f, multiplier);
            this.hitstopBonus = Mathf.Max(0f, hitstopBonus);
            this.stream = stream;
        }

        /// <summary>
        /// Order 70: after the flat damage boons have decided what the hit is worth, so a crit
        /// multiplies the finished number rather than the base — a crit that ignored the rest of
        /// the loadout would make Flux worse the more the player had invested elsewhere.
        /// </summary>
        public int Order => 70;

        public void Modify(ref HitContext context)
        {
            if (stream == null || context.Damage <= 0f)
                return;

            if (!(context.Attack is IAbilityTagged tagged && tagged.Ability == scope))
                return;

            if (stream.NextFloat(0f, 1f) >= chance)
                return;

            Procs++;
            context.Damage *= multiplier;

            // A crit reads as a heavy hit, so the spark, the rumble and the damage number all grow
            // with it rather than a 3× landing with the feedback of a jab.
            //
            // It deliberately does NOT stamp a damage type. Flux is Wind, but claiming the hit
            // would blank whatever element another giver's boon had already put on it — the exact
            // rule M12 settled when a pure-damage boon was overwriting an elemental one.
            context.HitstopSeconds += hitstopBonus;
        }
    }
}
