using Game.Core.Player;

namespace Game.Combat
{
    /// <summary>What the player's attack button is worth at this instant.</summary>
    public enum AttackInputVerdict
    {
        /// <summary>Nothing is in the way — start the next step of the chain.</summary>
        Accept = 0,

        /// <summary>Held in the input buffer and thrown when the current action frees up.</summary>
        Buffer = 1,

        /// <summary>Thrown away outright. Not queued, not remembered.</summary>
        Discard = 2,
    }

    /// <summary>
    /// A snapshot of what the player is currently doing, as the interrupt rules need to see it.
    /// Engine-free and immutable so the policy can be exercised without a scene.
    /// </summary>
    public readonly struct AbilityState
    {
        public readonly bool IsAttacking;
        public readonly AbilityId Ability;
        public readonly AttackPhase Phase;
        public readonly float Elapsed;

        /// <summary>
        /// When the committed part of the move ends — wind-up + active. Not the whole move: the
        /// channel is the part that owns the player, and recovery is the settle afterwards.
        /// </summary>
        public readonly float ChannelEndSeconds;

        public AbilityState(
            bool isAttacking, AbilityId ability, AttackPhase phase, float elapsed, float channelEndSeconds)
        {
            IsAttacking = isAttacking;
            Ability = ability;
            Phase = phase;
            Elapsed = elapsed;
            ChannelEndSeconds = channelEndSeconds;
        }

        /// <summary>Nothing running.</summary>
        public static AbilityState Idle => default;

        public bool IsCommitted =>
            IsAttacking && (Phase == AttackPhase.Windup || Phase == AttackPhase.Active);

        /// <summary>
        /// True while the Undertow owns the player: wind-up plus the pull itself. Recovery is
        /// deliberately outside it, so chaining out of the settle keeps working exactly as it did.
        /// </summary>
        public bool IsInVortexChannel => IsCommitted && Ability == AbilityId.VORTEX;

        /// <summary>Seconds of channel left. Negative is impossible while the phase says committed.</summary>
        public float ChannelRemaining => ChannelEndSeconds - Elapsed;
    }

    /// <summary>
    /// The global ability-interrupt priority, in one engine-free place.
    ///
    /// <para>The rule set is short and it is the whole point of the class existing: <b>the Split
    /// Second and the Blink are answerable at any moment, in any state</b> — mid-Undertow,
    /// mid-combo, mid-recovery — and <b>the attack button cannot cut into an Undertow channel at
    /// all</b>. A press during the channel is discarded rather than queued, because a buffered
    /// punch that fires the instant a spin ends is a punch the player asked for half a second ago
    /// and has usually stopped wanting.</para>
    ///
    /// <para>⚠️ This deliberately overrides CLAUDE.md hard rule 6 / DESIGN.md § Attacks, which make
    /// wind-up and active frames uncancellable by anything. That was the rule the whole cancel
    /// system was built on, and it is being relaxed <em>only</em> for the two defensive answers, on
    /// an explicit instruction (2026-08-12). The cost of a dash-cancel is unchanged — it still
    /// spends a charge — and the attack button gains no new rights whatsoever.</para>
    /// </summary>
    public static class AbilityInterruptPolicy
    {
        /// <summary>
        /// Whether <paramref name="source"/> may cut <paramref name="state"/> short.
        ///
        /// <para>Every real interrupt source outranks everything, so this is true for anything but
        /// <see cref="InterruptSource.None"/>. It is written out rather than assumed because a rule
        /// nobody can find is a rule that grows exceptions: the next ability that wants interrupt
        /// rights adds a case here, where the tests are, instead of a condition somewhere in a
        /// MonoBehaviour.</para>
        /// </summary>
        public static bool AllowsInterrupt(in AbilityState state, InterruptSource source) =>
            source != InterruptSource.None;

        /// <summary>
        /// What to do with an attack press, given what is running and how long the Undertow's
        /// optional tail-buffer window is.
        ///
        /// <para><paramref name="vortexTailSeconds"/> at 0 — the shipped default — means the
        /// channel refuses the attack button outright for its whole length. Above 0 it opens a
        /// window at the very END of the channel during which a press is buffered instead, and
        /// fires as the spin ends. It exists because "I pressed attack just as the spin finished
        /// and nothing came out" is the one place the discard rule is likely to feel wrong; whether
        /// it actually does is a playtest question, so the feature ships wired and switched off.
        /// </para>
        /// </summary>
        public static AttackInputVerdict ResolveAttackInput(in AbilityState state, float vortexTailSeconds)
        {
            if (!state.IsAttacking)
                return AttackInputVerdict.Accept;

            if (state.IsInVortexChannel)
            {
                return vortexTailSeconds > 0f && state.ChannelRemaining <= vortexTailSeconds
                    ? AttackInputVerdict.Buffer
                    : AttackInputVerdict.Discard;
            }

            // Everything else keeps the mandatory ~150 ms buffer: a press during a committed punch
            // waits and throws the next step as recovery opens, which is what makes the chain feel
            // continuous rather than frame-perfect.
            return state.IsCommitted ? AttackInputVerdict.Buffer : AttackInputVerdict.Accept;
        }
    }
}
