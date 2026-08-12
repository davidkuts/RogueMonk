using Game.Level;
using NUnit.Framework;
using UnityEngine;

namespace Game.Level.Tests
{
    /// <summary>
    /// Which walls are even allowed to fade.
    ///
    /// <para>Built after the first playtest: the side walls run alongside a camera that looks north
    /// and down, so a cast to somebody hugging one grazed its length and opened a hole in a wall the
    /// player was standing in <em>front</em> of. Only the camera-facing wall can genuinely hide
    /// anything, and the rule is derived from geometry rather than a name so a rotated room still
    /// picks the right one.</para>
    ///
    /// <para>The predicate lives on a MonoBehaviour and needs a transform, so these build throwaway
    /// GameObjects rather than calling into a pure type. That is the whole reason the arithmetic is
    /// stated here as well: it is the part that would break silently.</para>
    /// </summary>
    public sealed class WallFacingTests
    {
        // The camera looks north and down; only its planar direction matters.
        static readonly Vector3 CameraForward = Vector3.forward;
        const float RoomHalfWidth = 10f;

        static WallOccluder WallAt(Vector3 position)
        {
            var go = new GameObject("Wall");
            go.transform.position = position;
            var occluder = go.AddComponent<WallOccluder>();
            occluder.SetGroupCentre(Vector3.zero);
            return occluder;
        }

        static void Destroy(WallOccluder occluder)
        {
            if (occluder != null)
                Object.DestroyImmediate(occluder.gameObject);
        }

        [Test]
        public void OnlyTheCameraFacingWallMayFade()
        {
            WallOccluder south = WallAt(new Vector3(0f, 1.5f, -RoomHalfWidth));
            WallOccluder north = WallAt(new Vector3(0f, 1.5f, RoomHalfWidth));
            WallOccluder east = WallAt(new Vector3(RoomHalfWidth, 1.5f, 0f));
            WallOccluder west = WallAt(new Vector3(-RoomHalfWidth, 1.5f, 0f));

            Assert.That(south.FacesCamera(CameraForward, 0.5f), Is.True, "the south wall is the one in the way");
            Assert.That(north.FacesCamera(CameraForward, 0.5f), Is.False, "the far wall cannot hide anyone");
            Assert.That(east.FacesCamera(CameraForward, 0.5f), Is.False, "side walls run alongside the view");
            Assert.That(west.FacesCamera(CameraForward, 0.5f), Is.False, "side walls run alongside the view");

            Destroy(south); Destroy(north); Destroy(east); Destroy(west);
        }

        /// <summary>The rule is geometric, so turning the room turns which wall answers with it.</summary>
        [Test]
        public void ARotatedRoomPicksTheWallThatIsActuallyInTheWay()
        {
            WallOccluder east = WallAt(new Vector3(RoomHalfWidth, 1.5f, 0f));

            // Camera re-aimed to look west: the east wall is now the one between it and the room.
            Assert.That(east.FacesCamera(Vector3.left, 0.5f), Is.True);
            Assert.That(east.FacesCamera(Vector3.right, 0.5f), Is.False);

            Destroy(east);
        }

        /// <summary>Height must not enter into it — a wall is picked by where it stands, not how tall it is.</summary>
        [Test]
        public void VerticalOffsetDoesNotAffectTheAnswer()
        {
            WallOccluder low = WallAt(new Vector3(0f, 0.5f, -RoomHalfWidth));
            WallOccluder high = WallAt(new Vector3(0f, 40f, -RoomHalfWidth));

            Assert.That(low.FacesCamera(CameraForward, 0.5f), Is.True);
            Assert.That(high.FacesCamera(CameraForward, 0.5f), Is.True);

            Destroy(low); Destroy(high);
        }

        /// <summary>Zero is the escape hatch: the filter is data, so it can be switched off entirely.</summary>
        [Test]
        public void AThresholdOfZeroAdmitsEveryWall()
        {
            WallOccluder north = WallAt(new Vector3(0f, 1.5f, RoomHalfWidth));

            Assert.That(north.FacesCamera(CameraForward, 0f), Is.True);

            Destroy(north);
        }

        /// <summary>
        /// A pillar standing on the room's own centre has no outward direction, and a wall fitted by
        /// hand has no room to measure against. Neither can answer, so neither is silently excluded.
        /// </summary>
        [Test]
        public void AnOccluderThatCannotAnswerIsLeftAlone()
        {
            WallOccluder pillar = WallAt(Vector3.zero);
            Assert.That(pillar.FacesCamera(CameraForward, 0.5f), Is.True);

            var bare = new GameObject("Ungrouped").AddComponent<WallOccluder>();
            bare.transform.position = new Vector3(0f, 1.5f, RoomHalfWidth);
            Assert.That(bare.FacesCamera(CameraForward, 0.5f), Is.True);

            Destroy(pillar);
            Object.DestroyImmediate(bare.gameObject);
        }
    }

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
