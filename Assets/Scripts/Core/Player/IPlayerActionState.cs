namespace Game.Core.Player
{
    /// <summary>
    /// What an in-progress action (an attack, later a cast) permits the motor to do.
    /// Lives in Game.Core so <see cref="PlayerMotor"/> can consult combat state without
    /// Game.Core having to reference Game.Combat — the dependency only runs one way.
    /// Resolved at runtime via GetComponent; absent means "everything allowed".
    /// </summary>
    public interface IPlayerActionState
    {
        /// <summary>Walk speed multiplier this action imposes. 1 = unrestricted, 0 = rooted.</summary>
        float MoveSpeedMultiplier { get; }

        /// <summary>
        /// False while the action owns facing — an attack aiming at its target must not be
        /// spun off it by the movement stick. The player strafes instead.
        /// </summary>
        bool AllowsTurning { get; }

        /// <summary>
        /// Whether <paramref name="source"/> may cut the current action short right now.
        ///
        /// <para>The Blink and the Split Second are answerable at any moment, in any state, so this
        /// is true for both whatever is running. It exists as a question rather than as an assumed
        /// yes because the caller must not spend a dash charge on a cancel that is going to be
        /// refused, and because the rule is worth having in one named place.</para>
        /// </summary>
        bool AllowsInterrupt(InterruptSource source);

        /// <summary>False while the action is committed (attack wind-up and active frames).</summary>
        bool AllowsDash { get; }

        /// <summary>
        /// Called once the interrupting action has actually started, so the current one can cut
        /// itself short. Never called speculatively — a refused dash or riposte must leave the
        /// action the player was already in completely untouched.
        /// </summary>
        void CancelFor(InterruptSource source);
    }
}
