using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Steers a body around whatever is directly in front of it.
    ///
    /// <para>Local avoidance rather than pathfinding. DESIGN.md bakes a NavMesh per room prefab and
    /// that is the real long-term answer for routing; this is the cheap layer that stops a body
    /// grinding along the side of a crate in the meantime — which is what a raptor circling the
    /// player was doing, and it reads as the enemy being stupid rather than as the enemy being
    /// blocked.</para>
    ///
    /// <para>It deflects rather than stops. A body that halts at an obstacle looks broken; a body
    /// that slides along it looks clumsy; a body that <em>picks a side and goes round</em> looks
    /// like it meant to. The side is chosen by whichever way the obstacle's surface already points,
    /// so it commits to the shorter way round instead of oscillating in front of it.</para>
    /// </summary>
    public static class ObstacleAvoidance
    {
        /// <summary>
        /// Returns a heading that keeps <paramref name="desired"/>'s intent but goes around
        /// anything solid in the way. Returns <paramref name="desired"/> unchanged when the path is
        /// clear, which is the overwhelmingly common case and costs one spherecast.
        /// </summary>
        /// <param name="probeDistance">How far ahead to look. Roughly a second of travel works well.</param>
        /// <param name="strength">0 ignores obstacles entirely, 1 turns almost fully along the surface.</param>
        public static Vector3 Deflect(
            Vector3 origin,
            Vector3 desired,
            float bodyRadius,
            float probeDistance,
            LayerMask obstacleLayers,
            float strength = 0.85f)
        {
            desired.y = 0f;
            if (desired.sqrMagnitude <= 0.0001f || probeDistance <= 0f || obstacleLayers.value == 0)
                return desired;

            Vector3 heading = desired.normalized;

            // Cast from body height rather than the feet: a floor collider directly underneath is
            // not an obstacle, and starting at ground level would report one on every frame.
            Vector3 from = origin;

            if (!Physics.SphereCast(from, bodyRadius * 0.9f, heading, out RaycastHit hit,
                    probeDistance, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                return desired;
            }

            // Slide the heading along the surface. The normal is flattened first, so a sloped or
            // uneven face still deflects horizontally instead of steering the body into the floor.
            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude <= 0.0001f)
                return desired;

            normal.Normalize();

            Vector3 along = Vector3.ProjectOnPlane(heading, normal);
            along.y = 0f;

            // Hitting a face dead-on leaves no tangent to slide along, so pick a side deliberately
            // rather than letting a near-zero vector normalize into noise. Sign comes from the
            // body's own offset from the contact, so two enemies at the same crate part around it.
            if (along.sqrMagnitude <= 0.0001f)
            {
                Vector3 offset = from - hit.point;
                offset.y = 0f;
                float side = Vector3.Dot(Vector3.Cross(Vector3.up, heading), offset) >= 0f ? 1f : -1f;
                along = Vector3.Cross(Vector3.up, heading) * side;
            }

            along.Normalize();

            // Closer obstacles deflect harder, so a body only commits to going round once the thing
            // is genuinely in the way — otherwise it would curve around obstacles it would have
            // missed anyway.
            float urgency = 1f - Mathf.Clamp01(hit.distance / probeDistance);
            float blend = Mathf.Clamp01(strength) * urgency;

            Vector3 result = Vector3.Lerp(heading, along, blend);
            result.y = 0f;

            return result.sqrMagnitude > 0.0001f ? result.normalized * desired.magnitude : desired;
        }
    }
}
