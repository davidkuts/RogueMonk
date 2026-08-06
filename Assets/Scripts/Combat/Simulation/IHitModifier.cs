namespace Game.Combat
{
    /// <summary>
    /// One stage of the hit-resolution pipeline. Elemental boons become implementations of
    /// this; the MVP ships none. Modifiers run in ascending <see cref="Order"/> and may
    /// change any field of the context, including cancelling the hit.
    /// </summary>
    public interface IHitModifier
    {
        /// <summary>Ascending run order. Ties keep insertion order.</summary>
        int Order { get; }

        void Modify(ref HitContext context);
    }
}
