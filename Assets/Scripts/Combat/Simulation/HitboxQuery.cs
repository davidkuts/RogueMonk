using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Turns a <see cref="HitboxShape"/> into physics, in one place.
    ///
    /// The player, both trash controllers and the boss each ran their own copy of this. Three
    /// copies is tolerable; adding a fourth shape kind to four copies is how they drift apart, so
    /// the broad phase and the narrow phase live here now.
    ///
    /// Split into two calls because an arc cannot be expressed as a Unity collider: the broad phase
    /// is the sphere it fits inside, and <see cref="Contains"/> then rejects everything outside the
    /// wedge. Sphere and box have no narrow phase — the broad phase was already exact.
    /// </summary>
    public static class HitboxQuery
    {
        /// <summary>Runs the broad phase and returns how many colliders it wrote into <paramref name="results"/>.</summary>
        public static int Overlap(
            in HitboxShape shape,
            Vector3 origin,
            Vector3 forward,
            LayerMask layers,
            Collider[] results)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 center = shape.WorldCenter(origin, forward);

            if (shape.Kind == HitboxKind.Box)
            {
                return Physics.OverlapBoxNonAlloc(
                    center, shape.Size * 0.5f, results,
                    Quaternion.LookRotation(forward, Vector3.up), layers, QueryTriggerInteraction.Collide);
            }

            // Arc included: its bounding sphere is the broad phase, Contains does the rest.
            return Physics.OverlapSphereNonAlloc(
                center, Mathf.Max(0f, shape.Radius), results, layers, QueryTriggerInteraction.Collide);
        }

        /// <summary>
        /// Narrow phase. True when a point genuinely lies in the shape, which for an arc means
        /// inside the wedge rather than merely inside its bounding sphere.
        /// </summary>
        public static bool Contains(in HitboxShape shape, Vector3 origin, Vector3 forward, Vector3 point)
        {
            if (shape.Kind != HitboxKind.Arc)
                return true;

            float arc = shape.EffectiveArcDegrees;
            if (arc >= 360f)
                return true;

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return true;
            forward.Normalize();

            Vector3 toPoint = point - shape.WorldCenter(origin, forward);
            toPoint.y = 0f;

            // Standing exactly on the attacker has no bearing to test; count it as a hit rather
            // than letting someone hide inside the pivot point.
            if (toPoint.sqrMagnitude <= 0.0001f)
                return true;

            return Vector3.Angle(forward, toPoint) <= arc * 0.5f;
        }
    }
}
