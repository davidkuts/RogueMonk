using System;
using UnityEngine;

namespace Game.Combat
{
    public enum HitboxKind
    {
        Sphere = 0,
        Box = 1,
    }

    /// <summary>
    /// Attack hitbox volume, described in the attacker's local space (Z forward).
    /// Pure data — the adapter turns it into a physics query.
    /// </summary>
    [Serializable]
    public struct HitboxShape
    {
        [Tooltip("Sphere or box volume.")]
        public HitboxKind Kind;

        [Tooltip("Offset from the attacker's origin, in attacker-local space (Z = forward).")]
        public Vector3 LocalOffset;

        [Tooltip("Sphere only: radius in metres.")]
        public float Radius;

        [Tooltip("Box only: full extents in metres.")]
        public Vector3 Size;

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
        };
    }
}
