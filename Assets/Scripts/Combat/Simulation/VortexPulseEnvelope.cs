using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// How brightly the Undertow is currently burning, given how much it has just eaten.
    ///
    /// <para>The vortex should <em>feed on hits</em>: subtle when it catches nothing, visibly
    /// fiercer in the middle of a dense sequence, and settling back down on its own. That is one
    /// number with two operations — hits push it up, time pulls it down — so it lives here as plain
    /// C# where the curve can be asserted rather than eyeballed in play.</para>
    ///
    /// <para>Hits <b>accumulate</b> rather than retrigger. A single spin can damage nine times
    /// (three enemies × three ticks), and restarting a fixed flash on each would make a crowd look
    /// identical to a duel — the pulse has to be able to say "that was a lot at once".</para>
    /// </summary>
    public sealed class VortexPulseEnvelope
    {
        float level;

        /// <summary>0 at rest, 1 at full brightness. What the disc multiplies its alpha by.</summary>
        public float Level => level;

        public bool IsQuiet => level <= 0f;

        /// <summary>
        /// One enemy took one tick. Clamped at 1 so a boss surrounded by trash cannot blow the
        /// effect out into a white disc — past a point, "more" has to stop meaning "brighter".
        /// </summary>
        public void Add(float intensity)
        {
            if (intensity <= 0f)
                return;

            level = Mathf.Clamp01(level + intensity);
        }

        /// <summary>
        /// Decays linearly, so a pulse always takes exactly <paramref name="durationSeconds"/> to
        /// fall from full — predictable is what makes it tunable. An exponential tail would leave a
        /// long dim smear that reads as the effect failing to switch off.
        /// </summary>
        public void Tick(float deltaTime, float durationSeconds)
        {
            if (level <= 0f || deltaTime <= 0f)
                return;

            if (durationSeconds <= 0f)
            {
                level = 0f;
                return;
            }

            level = Mathf.Max(0f, level - deltaTime / durationSeconds);
        }

        /// <summary>Back to rest immediately — for a cast ending, or an interrupt.</summary>
        public void Reset() => level = 0f;
    }
}
