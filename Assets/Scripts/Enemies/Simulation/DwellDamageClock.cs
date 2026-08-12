using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Pays out one tick per whole interval of <em>continuous</em> standing, and nothing on entry.
    ///
    /// <para>The shape is deliberately forgiving. Damage on the frame of overlap would make brushing
    /// the edge of a puddle cost health, which is the same failure DESIGN.md § Player health already
    /// ruled out for contact damage — "brushing a body must not cost health". Here the first second
    /// is free, so walking through the goo is a decision about time rather than an instant tax, and
    /// the player who reads it and keeps moving pays nothing at all.</para>
    ///
    /// <para>Leaving resets the clock. "Continuous" has to mean continuous, or a player could accrue
    /// a tick across half a dozen separate brushes and be damaged by ground they were never really
    /// standing in.</para>
    /// </summary>
    public sealed class DwellDamageClock
    {
        float dwellSeconds;
        int ticksPaid;

        /// <summary>Seconds of unbroken standing so far. Zero whenever the target is outside.</summary>
        public float DwellSeconds => dwellSeconds;

        public int TicksPaid => ticksPaid;

        /// <summary>
        /// Advances the clock and returns how many ticks came due this frame.
        ///
        /// <para>Counted from accumulated dwell rather than by subtracting a countdown, so a long
        /// frame pays everything it passed over instead of silently swallowing a tick — the same
        /// guarantee the attack state machine gives a hitbox.</para>
        /// </summary>
        public int Tick(float deltaTime, bool inside, float intervalSeconds)
        {
            if (!inside)
            {
                dwellSeconds = 0f;
                ticksPaid = 0;
                return 0;
            }

            if (deltaTime > 0f)
                dwellSeconds += deltaTime;

            if (intervalSeconds <= 0f)
                return 0;

            int due = Mathf.FloorToInt(dwellSeconds / intervalSeconds);
            int newly = due - ticksPaid;
            if (newly <= 0)
                return 0;

            ticksPaid = due;
            return newly;
        }

        public void Reset()
        {
            dwellSeconds = 0f;
            ticksPaid = 0;
        }
    }
}
