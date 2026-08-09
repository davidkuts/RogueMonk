using UnityEngine;

namespace Game.Enemies
{
    /// <summary>Tuning for one flock. Lives on the enemy definition, never in code.</summary>
    public struct BoidsWeights
    {
        /// <summary>Push away from neighbours that are too close. The one that stops a pile.</summary>
        public float Separation;

        /// <summary>Pull toward the neighbours' average position, so the carpet stays a carpet.</summary>
        public float Cohesion;

        /// <summary>Match the neighbours' average heading, so it moves as one thing.</summary>
        public float Alignment;

        /// <summary>Pull toward the target. Without it the flock is beautiful and harmless.</summary>
        public float Seek;

        /// <summary>Neighbours further than this are ignored entirely.</summary>
        public float NeighbourRadius;

        /// <summary>Separation only pushes inside this, and it is smaller than the neighbour radius.</summary>
        public float SeparationRadius;
    }

    /// <summary>
    /// Boids-lite: separation, cohesion, alignment, plus a pull toward the target.
    ///
    /// <para>Engine-free per CLAUDE.md rule 1, and worth keeping that way for a second reason — a
    /// swarm is the one archetype whose behaviour is impossible to eyeball. "Did they clump?" and
    /// "did they oscillate?" are questions a test answers and a playtest only feels.</para>
    ///
    /// <para>Deliberately not a full boids implementation. ENEMIES_BIOME1.md § 2.4 wants a moving
    /// carpet that clogs space, not a flight simulation: separation stops them stacking into one
    /// body, cohesion keeps them readable as a single mass, and seek makes them a threat. Alignment
    /// is the cheapest of the four to lose if the count ever needs raising.</para>
    /// </summary>
    public static class BoidsSteering
    {
        /// <summary>
        /// Steering for one member, given its neighbours. Returns a direction to accelerate along;
        /// callers scale it by speed. Never returns a vector with a Y component — the swarm is a
        /// carpet, and the arena has no ceilings to fly at.
        /// </summary>
        /// <param name="index">Which member is being steered. Excluded from its own neighbourhood.</param>
        public static Vector3 Steer(
            int index,
            Vector3[] positions,
            Vector3[] velocities,
            int count,
            Vector3 target,
            in BoidsWeights weights)
        {
            if (positions == null || velocities == null || index < 0 || index >= count)
                return Vector3.zero;

            Vector3 self = positions[index];

            Vector3 separation = Vector3.zero;
            Vector3 centre = Vector3.zero;
            Vector3 heading = Vector3.zero;
            int neighbours = 0;

            float neighbourSqr = weights.NeighbourRadius * weights.NeighbourRadius;
            float separationSqr = weights.SeparationRadius * weights.SeparationRadius;

            for (int i = 0; i < count; i++)
            {
                if (i == index)
                    continue;

                Vector3 offset = positions[i] - self;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;

                if (sqr > neighbourSqr || sqr <= 0.000001f)
                    continue;

                neighbours++;
                centre += positions[i];
                heading += velocities[i];

                if (sqr >= separationSqr)
                    continue;

                // Inverse-distance so the push grows sharply as bodies converge. A flat push lets
                // them settle into each other and the carpet becomes one dot.
                separation -= offset / sqr;
            }

            Vector3 steering = Vector3.zero;

            if (neighbours > 0)
            {
                centre /= neighbours;
                heading /= neighbours;

                steering += Normalize(separation) * weights.Separation;
                steering += Normalize(Flatten(centre - self)) * weights.Cohesion;
                steering += Normalize(Flatten(heading)) * weights.Alignment;
            }

            steering += Normalize(Flatten(target - self)) * weights.Seek;

            return Normalize(Flatten(steering));
        }

        static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static Vector3 Normalize(Vector3 v) =>
            v.sqrMagnitude > 0.000001f ? v.normalized : Vector3.zero;
    }
}
