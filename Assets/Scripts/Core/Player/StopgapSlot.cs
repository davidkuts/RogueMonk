namespace Game.Core.Player
{
    /// <summary>
    /// Which D-pad direction a Stopgap lives on.
    ///
    /// <para>Each Stopgap owns a direction and holds ONE of itself there, rather than everything
    /// sharing a pooled carry cap. That is what makes the HUD readable at a glance: the button you
    /// would press IS the slot, so "do I still have the rewind?" is answered by looking at the
    /// d-pad rather than by remembering what you picked up two rooms ago.</para>
    ///
    /// <para>Four directions means four Stopgaps at most, ever, which is a deliberate ceiling
    /// rather than an accident of the input device — REWARDS.md §5 wants panic buttons, not a
    /// hoardable resource.</para>
    ///
    /// <para>Lives in Game.Core because the input reader needs it and Game.Core cannot see
    /// Game.Combat, where the Stopgaps themselves live.</para>
    /// </summary>
    public enum StopgapSlot
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }
}
