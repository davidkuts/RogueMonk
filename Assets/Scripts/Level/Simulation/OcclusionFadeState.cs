using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// One wall's fade, eased. Engine-free so the timing is testable rather than eyeballed —
    /// <see cref="WallOccluder"/> is the adapter that owns one of these and pushes its value at a
    /// renderer.
    ///
    /// <para>Rate is expressed as <c>1 / duration</c> rather than as a speed, so a full 0 → 1
    /// traversal always takes exactly the authored number of seconds however the durations are
    /// retuned. Reversing mid-fade continues from wherever the value currently sits: a wall the
    /// player steps behind, out from, and behind again inside 0.4 s must never snap, and snapping
    /// to 0 before starting the fade back in is exactly the pop this easing exists to prevent.</para>
    ///
    /// <para>Fade-in and fade-out are separate durations because they answer different questions.
    /// Coming in wants to be quick — the actor is already hidden — while going out can afford to
    /// linger, since nothing is being concealed while it finishes.</para>
    /// </summary>
    public sealed class OcclusionFadeState
    {
        /// <summary>0 = solid, 1 = fully dithered down to the faded visibility level.</summary>
        public float Current { get; private set; }

        /// <summary>True when this wall is completely solid and can be skipped entirely.</summary>
        public bool IsSolid => Current <= 0f;

        /// <summary>
        /// Advances toward occluded (1) or clear (0) and returns the new value.
        ///
        /// <para>A duration of zero means "immediately", which is what makes the easing switchable
        /// off from data without a second code path.</para>
        /// </summary>
        public float Tick(float deltaTime, bool occluded, float fadeInSeconds, float fadeOutSeconds)
        {
            float target = occluded ? 1f : 0f;
            float duration = occluded ? fadeInSeconds : fadeOutSeconds;

            if (duration <= 0f || deltaTime <= 0f)
            {
                Current = duration <= 0f ? target : Current;
                return Current;
            }

            Current = Mathf.MoveTowards(Current, target, deltaTime / duration);
            return Current;
        }

        public void Reset() => Current = 0f;
    }
}
