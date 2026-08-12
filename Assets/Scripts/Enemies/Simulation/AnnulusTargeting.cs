using Game.Core.Rng;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Picks a ground point in a ring around a target, for a lobbed area-denial shot.
    ///
    /// <para><b>Why a ring and not the player.</b> A glob aimed straight at the player is either
    /// dodgeable — in which case it always lands where they just were and denies nothing — or it is
    /// not, which breaks DESIGN.md's "no guaranteed damage". Landing it <em>near</em> them makes it
    /// an area-denial tool instead of an attack: it costs the player ground rather than health, and
    /// where the ground goes is not something they can simply out-run. The inner radius is what
    /// stops it being a point-blank hit; the outer is what stops it being irrelevant.</para>
    ///
    /// <para>Engine-free so the distribution can be asserted without a scene. The caller is
    /// responsible for checking the result is actually walkable — that needs a NavMesh, which is a
    /// scene concern.</para>
    /// </summary>
    public static class AnnulusTargeting
    {
        /// <summary>
        /// A planar offset whose length lies between <paramref name="minRadius"/> and
        /// <paramref name="maxRadius"/>.
        ///
        /// <para>The radius is drawn through a square root rather than linearly. Interpolating the
        /// radius directly would bunch points toward the middle of the ring, because the area of a
        /// band grows with its radius — and a "random spot around you" that reliably favoured one
        /// distance would be learned as a pattern within a room or two.</para>
        ///
        /// <para>⚠️ <paramref name="stream"/> must be a stream DERIVED off the run, never the run
        /// stream itself. How many globs a Sailspit throws depends entirely on how the player
        /// fights, so spending run draws here would mean two players quoting the same seed got
        /// different levels — the rule DESIGN.md set for boss moves, M16 set for reward content and
        /// M19 set for Flux.</para>
        /// </summary>
        public static Vector3 PickOffset(IRandomSource stream, float minRadius, float maxRadius)
        {
            if (stream == null)
                return Vector3.zero;

            float min = Mathf.Max(0f, Mathf.Min(minRadius, maxRadius));
            float max = Mathf.Max(min, Mathf.Max(minRadius, maxRadius));

            float angle = stream.NextFloat() * Mathf.PI * 2f;

            // Uniform over the ANNULUS AREA: r = sqrt(lerp(min^2, max^2, u)).
            float t = stream.NextFloat();
            float radius = Mathf.Sqrt(Mathf.Lerp(min * min, max * max, t));

            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>
        /// True when <paramref name="offset"/> lies inside the ring, within a small tolerance.
        /// Exists so a test can state the contract rather than re-deriving the arithmetic.
        /// </summary>
        public static bool IsInAnnulus(Vector3 offset, float minRadius, float maxRadius, float tolerance = 0.001f)
        {
            float min = Mathf.Max(0f, Mathf.Min(minRadius, maxRadius));
            float max = Mathf.Max(min, Mathf.Max(minRadius, maxRadius));

            offset.y = 0f;
            float length = offset.magnitude;

            return length >= min - tolerance && length <= max + tolerance;
        }
    }
}
