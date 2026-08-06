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

        /// <summary>False while the action is committed (attack wind-up and active frames).</summary>
        bool AllowsDash { get; }

        /// <summary>Called when a dash actually starts, so the action can cut itself short.</summary>
        void CancelForDash();
    }
}
