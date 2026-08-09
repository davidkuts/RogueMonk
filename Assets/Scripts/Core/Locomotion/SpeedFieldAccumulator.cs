using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Gathers the slowing fields a body is standing in during one frame and resolves them to a
    /// single multiplier.
    ///
    /// <para><b>The slowest wins; they never stack.</b> ENEMIES_BIOME1.md § 2.3 requires it of
    /// Sailspit's stall zones, and the reason generalises: overlapping fields that multiplied would
    /// turn two of anything into a soft stun-lock, which is exactly the failure the design forbids
    /// — a stall zone shapes the arena, it never takes the player's turn away. Three 0.45 puddles
    /// multiply to 0.09, which is a hard lock; taking the minimum keeps them at 0.45 however many
    /// there are.</para>
    ///
    /// <para>Double-buffered because fields report themselves from their own Update and Unity does
    /// not order Updates. Reading what the fields finished writing last frame is stable and at
    /// worst one frame late; reading a half-filled accumulator would make the slow flicker
    /// depending on component order, which is far worse on a two-second puddle.</para>
    ///
    /// <para>Engine-free per CLAUDE.md rule 1, so the rule is testable without a scene.</para>
    /// </summary>
    public struct SpeedFieldAccumulator
    {
        float pending;
        float current;
        bool initialised;

        /// <summary>The multiplier to apply this frame. 1 when standing on clean ground.</summary>
        public float Current
        {
            get
            {
                EnsureInitialised();
                return current;
            }
        }

        /// <summary>
        /// Reports one field the body is standing in. Called any number of times per frame, in any
        /// order, by anything.
        /// </summary>
        public void Report(float multiplier)
        {
            EnsureInitialised();
            pending = Mathf.Min(pending, Mathf.Clamp01(multiplier));
        }

        /// <summary>
        /// Closes the frame. Whatever was reported becomes the value read next frame, and the
        /// accumulator reopens at 1 — so a body that has walked out of every field stops being
        /// slowed rather than keeping the last value it saw.
        /// </summary>
        public void EndFrame()
        {
            EnsureInitialised();
            current = pending;
            pending = 1f;
        }

        /// <summary>
        /// A default-constructed struct has both fields at 0, which would mean "frozen solid".
        /// Correcting it lazily keeps the type usable as a plain field with no constructor call.
        /// </summary>
        void EnsureInitialised()
        {
            if (initialised)
                return;

            initialised = true;
            pending = 1f;
            current = 1f;
        }
    }
}
