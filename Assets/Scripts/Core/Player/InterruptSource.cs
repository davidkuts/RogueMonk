namespace Game.Core.Player
{
    /// <summary>
    /// What is asking to cut an in-progress action short.
    ///
    /// <para>Lives in Game.Core beside <see cref="IPlayerActionState"/> for the same reason
    /// <c>StopgapSlot</c> does: <see cref="PlayerMotor"/> has to name the reason it is cancelling an
    /// attack, and Game.Core cannot see Game.Combat. The dependency only runs one way.</para>
    ///
    /// <para>Every value except <see cref="None"/> outranks whatever is running. That is the whole
    /// interrupt priority: the Blink and the Split Second are always available, and nothing else is
    /// allowed to cut in. The ordering is not a numeric one — two interrupts never race, because
    /// each is processed in its own component in a pinned execution order.</para>
    /// </summary>
    public enum InterruptSource
    {
        /// <summary>Not an interrupt. Starting an action this way obeys the ordinary cancel rules.</summary>
        None = 0,

        /// <summary>The Blink. Accepted in any state; keeps its i-frames and its charge cost.</summary>
        Dash = 1,

        /// <summary>The Split Second's Riposte, when armed. Highest priority — it interrupts everything.</summary>
        Riposte = 2,
    }
}
