using System;

namespace Game.Level
{
    /// <summary>
    /// What a room is for. A flags enum because a template may be authored to serve more than
    /// one purpose, and the generator needs to ask "which templates can host this role?".
    /// </summary>
    [Flags]
    public enum RoomRole
    {
        None = 0,

        /// <summary>An ordinary fight room.</summary>
        Standard = 1 << 0,

        /// <summary>The level's final encounter. Always last, always exactly one.</summary>
        Boss = 1 << 1,

        Any = Standard | Boss,
    }
}
