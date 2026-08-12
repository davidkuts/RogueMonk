using Game.Level;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// The wall fade's timing. Feel is the human's call in Play Mode; that a wall takes exactly the
    /// authored number of seconds, and never snaps, is a correctness question and belongs here.
    /// </summary>
    public sealed class OcclusionFadeStateTests
    {
        const float In = 0.2f;
        const float Out = 0.2f;

        static void Advance(OcclusionFadeState state, float seconds, bool occluded, float step = 1f / 60f)
        {
            for (float elapsed = 0f; elapsed < seconds - 0.0001f; elapsed += step)
                state.Tick(step, occluded, In, Out);
        }

        [Test]
        public void StartsSolid()
        {
            var state = new OcclusionFadeState();

            Assert.That(state.Current, Is.EqualTo(0f));
            Assert.That(state.IsSolid, Is.True);
        }

        [Test]
        public void ReachesFullFadeInExactlyTheFadeInDuration()
        {
            var state = new OcclusionFadeState();

            // One frame short is still short: the duration is a promise, not an approximation.
            state.Tick(In - 0.01f, true, In, Out);
            Assert.That(state.Current, Is.LessThan(1f));

            state.Tick(0.01f, true, In, Out);
            Assert.That(state.Current, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ReachesSolidInExactlyTheFadeOutDuration()
        {
            var state = new OcclusionFadeState();
            Advance(state, In, true);
            Assert.That(state.Current, Is.EqualTo(1f).Within(0.0001f));

            state.Tick(Out - 0.01f, false, In, Out);
            Assert.That(state.Current, Is.GreaterThan(0f));

            state.Tick(0.01f, false, In, Out);
            Assert.That(state.Current, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(state.IsSolid, Is.True);
        }

        [Test]
        public void HoldsAtFullFadeWhileStillOccluded()
        {
            var state = new OcclusionFadeState();
            Advance(state, In * 4f, true);

            Assert.That(state.Current, Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// The case the easing exists for: stepping behind a wall, out, and behind again inside
        /// half a second. Reversing has to continue from where the value currently sits — dropping
        /// to zero first and fading back up is the pop this is meant to prevent.
        /// </summary>
        [Test]
        public void ReversingMidFadeContinuesFromTheCurrentValue()
        {
            var state = new OcclusionFadeState();

            state.Tick(In * 0.5f, true, In, Out);
            Assert.That(state.Current, Is.EqualTo(0.5f).Within(0.0001f));

            state.Tick(Out * 0.25f, false, In, Out);
            Assert.That(state.Current, Is.EqualTo(0.25f).Within(0.0001f));

            state.Tick(In * 0.25f, true, In, Out);
            Assert.That(state.Current, Is.EqualTo(0.5f).Within(0.0001f));
        }

        /// <summary>Fade in and fade out are independent knobs, so a slow restore does not slow the cut.</summary>
        [Test]
        public void FadeInAndFadeOutUseTheirOwnDurations()
        {
            var state = new OcclusionFadeState();

            state.Tick(0.1f, true, 0.1f, 1f);
            Assert.That(state.Current, Is.EqualTo(1f).Within(0.0001f));

            state.Tick(0.1f, false, 0.1f, 1f);
            Assert.That(state.Current, Is.EqualTo(0.9f).Within(0.0001f));
        }

        /// <summary>Zero seconds means "immediately", which is how the easing is switched off from data.</summary>
        [Test]
        public void ZeroDurationSnaps()
        {
            var state = new OcclusionFadeState();

            state.Tick(1f / 60f, true, 0f, 0f);
            Assert.That(state.Current, Is.EqualTo(1f));

            state.Tick(1f / 60f, false, 0f, 0f);
            Assert.That(state.Current, Is.EqualTo(0f));
        }

        [Test]
        public void AZeroLengthFrameChangesNothing()
        {
            var state = new OcclusionFadeState();
            state.Tick(In * 0.5f, true, In, Out);
            float before = state.Current;

            state.Tick(0f, true, In, Out);

            Assert.That(state.Current, Is.EqualTo(before));
        }

        [Test]
        public void ResetReturnsToSolid()
        {
            var state = new OcclusionFadeState();
            Advance(state, In, true);

            state.Reset();

            Assert.That(state.Current, Is.EqualTo(0f));
        }
    }
}
