using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Engine-free Hades-style soft auto-aim: pick the nearest target inside a cone around
    /// facing, then rotate onto it over the wind-up. Never an instant snap — the player must
    /// still be able to read where the attack is going.
    /// </summary>
    public static class AimAssist
    {
        /// <summary>
        /// Selects the nearest candidate within <paramref name="rangeMeters"/> whose bearing
        /// from <paramref name="origin"/> lies inside <paramref name="coneDegrees"/> centred on
        /// <paramref name="facing"/>. Returns false when nothing qualifies.
        /// </summary>
        public static bool TrySelectTarget(
            Vector3 origin,
            Vector3 facing,
            IReadOnlyList<Vector3> candidates,
            float coneDegrees,
            float rangeMeters,
            out int index)
        {
            index = -1;
            if (candidates == null || candidates.Count == 0 || rangeMeters <= 0f)
                return false;

            facing.y = 0f;
            if (facing.sqrMagnitude <= 0f)
                return false;
            facing.Normalize();

            float halfCone = Mathf.Clamp(coneDegrees, 0f, 360f) * 0.5f;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 toTarget = candidates[i] - origin;
                toTarget.y = 0f;

                float sqrDistance = toTarget.sqrMagnitude;
                if (sqrDistance <= 0f || sqrDistance > rangeMeters * rangeMeters)
                    continue;

                if (Vector3.Angle(facing, toTarget) > halfCone)
                    continue;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    index = i;
                }
            }

            return index >= 0;
        }

        /// <summary>Rotates <paramref name="facing"/> toward <paramref name="desired"/> at a capped rate.</summary>
        public static Vector3 RotateFacing(Vector3 facing, Vector3 desired, float degreesPerSecond, float deltaTime)
        {
            facing.y = 0f;
            desired.y = 0f;

            if (facing.sqrMagnitude <= 0f || desired.sqrMagnitude <= 0f || deltaTime <= 0f)
                return facing.sqrMagnitude > 0f ? facing.normalized : Vector3.forward;

            float maxRadians = Mathf.Max(0f, degreesPerSecond) * Mathf.Deg2Rad * deltaTime;
            Vector3 turned = Vector3.RotateTowards(facing.normalized, desired.normalized, maxRadians, 0f);
            turned.y = 0f;

            return turned.sqrMagnitude > 0f ? turned.normalized : facing.normalized;
        }
    }
}
