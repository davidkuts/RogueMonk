using System.Collections.Generic;

namespace Game.Level
{
    /// <summary>
    /// Everything the HUD needs to draw a boss bar, and nothing about how a boss works.
    ///
    /// Declared here rather than in Game.Enemies so Game.UI never has to reference the enemy
    /// assembly: a boss bar is an <em>encounter</em> concept, and encounters are what
    /// <see cref="LevelDirector"/> already owns. Widening the UI's reach to every enemy's internals
    /// would buy nothing this does not.
    /// </summary>
    public interface IBossEncounter
    {
        /// <summary>Name shown on the bar and the room banner.</summary>
        string DisplayName { get; }

        /// <summary>0..1. The only progress signal in a fight with no healing.</summary>
        float HealthFraction { get; }

        /// <summary>Zero-based. 0 is the opening phase.</summary>
        int PhaseIndex { get; }

        /// <summary>Total phases including the opener, so the bar can draw its dividers.</summary>
        int PhaseCount { get; }

        /// <summary>Health fractions at which each later phase begins, for the divider ticks.</summary>
        IReadOnlyList<float> PhaseThresholds { get; }

        /// <summary>False once the boss is down, including while its death beat plays.</summary>
        bool IsAlive { get; }
    }
}
