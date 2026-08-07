using System;
using UnityEngine;

namespace Game.Combat
{
    public enum HitboxKind
    {
        Sphere = 0,
        Box = 1,

        /// <summary>
        /// A wedge centred on facing — a swing, not a bubble.
        ///
        /// A sphere in front of an attacker punishes standing anywhere near it, which reads as
        /// "the attack just happens to you". A slice can be stepped out of sideways, so the answer
        /// to a telegraph becomes a decision about where to stand.
        /// </summary>
        Arc = 2,
    }

    /// <summary>
    /// Attack hitbox volume, described in the attacker's local space (Z forward).
    /// Pure data — the adapter turns it into a physics query.
    /// </summary>
    [Serializable]
    public struct HitboxShape
    {
        [Tooltip("Sphere, box, or arc (a pizza-slice wedge centred on facing).")]
        public HitboxKind Kind;

        [Tooltip("Offset from the attacker's origin, in attacker-local space (Z = forward). Arcs normally sit at zero, since a swing starts at the attacker.")]
        public Vector3 LocalOffset;

        [Tooltip("Sphere and arc: radius in metres. For an arc this is its reach.")]
        public float Radius;

        [Tooltip("Box only: full extents in metres.")]
        public Vector3 Size;

        [Tooltip("Arc only: total width of the wedge in degrees, centred on facing. 360 is a full circle.")]
        public float ArcDegrees;

        /// <summary>Converts the local offset to world space for an attacker at <paramref name="origin"/> facing <paramref name="forward"/>.</summary>
        public Vector3 WorldCenter(Vector3 origin, Vector3 forward)
        {
            forward.y = 0f;
            Quaternion rotation = forward.sqrMagnitude > 0f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : Quaternion.identity;

            return origin + rotation * LocalOffset;
        }

        public static HitboxShape DefaultSphere => new HitboxShape
        {
            Kind = HitboxKind.Sphere,
            LocalOffset = new Vector3(0f, 0f, 1f),
            Radius = 1f,
            Size = Vector3.one,
            ArcDegrees = 360f,
        };

        /// <summary>The effective wedge width, treating an unset value as a full circle.</summary>
        public float EffectiveArcDegrees => ArcDegrees <= 0f ? 360f : Mathf.Min(ArcDegrees, 360f);
    }
}
