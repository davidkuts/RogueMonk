using NUnit.Framework;
using UnityEngine;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// The swarm is the one archetype whose behaviour cannot be eyeballed. "Did they clump?" and
    /// "did they stop seeking?" are questions a test answers and a playtest only feels.
    /// </summary>
    public class BoidsSteeringTests
    {
        static BoidsWeights Default => new BoidsWeights
        {
            Separation = 1.6f,
            Cohesion = 0.5f,
            Alignment = 0.35f,
            Seek = 1.0f,
            NeighbourRadius = 3.0f,
            SeparationRadius = 1.0f,
        };

        [Test]
        public void ALoneBoidSteersStraightAtTheTarget()
        {
            var positions = new[] { Vector3.zero };
            var velocities = new[] { Vector3.zero };

            Vector3 steer = BoidsSteering.Steer(0, positions, velocities, 1, new Vector3(10f, 0f, 0f), Default);

            Assert.AreEqual(1f, steer.x, 0.001f);
            Assert.AreEqual(0f, steer.z, 0.001f);
        }

        [Test]
        public void SteeringIsAlwaysPlanar()
        {
            var positions = new[] { Vector3.zero, new Vector3(0.4f, 5f, 0f) };
            var velocities = new[] { Vector3.zero, new Vector3(0f, 9f, 0f) };

            // The arena has no ceilings and the swarm is a carpet. A Y component would have birds
            // climbing each other, and the CharacterController would fight it every frame.
            Vector3 steer = BoidsSteering.Steer(0, positions, velocities, 2, new Vector3(3f, 8f, 3f), Default);

            Assert.AreEqual(0f, steer.y, 1e-6f);
        }

        [Test]
        public void TwoBoidsOnTopOfEachOtherPushApart()
        {
            var positions = new[] { Vector3.zero, new Vector3(0.2f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };

            // Target is straight ahead of both, so seek alone would drive them together. Separation
            // has to win at 0.2m or the carpet collapses to a single dot.
            var weights = Default;
            Vector3 steer = BoidsSteering.Steer(0, positions, velocities, 2, new Vector3(0.2f, 0f, 0f), weights);

            Assert.Less(steer.x, 0f, "the crowded boid must be pushed away from its neighbour");
        }

        [Test]
        public void SeparationBeatsSeekWhenCrowdedAndLosesWhenSpaced()
        {
            var weights = Default;

            var crowded = new[] { Vector3.zero, new Vector3(0.25f, 0f, 0f) };
            var spaced = new[] { Vector3.zero, new Vector3(2.5f, 0f, 0f) };
            var velocities = new[] { Vector3.zero, Vector3.zero };
            var target = new Vector3(10f, 0f, 0f);

            Vector3 crowdedSteer = BoidsSteering.Steer(0, crowded, velocities, 2, target, weights);
            Vector3 spacedSteer = BoidsSteering.Steer(0, spaced, velocities, 2, target, weights);

            // This is the whole balance: too close and it backs off, comfortably spaced and it
            // presses the attack. If it never backs off they pile; if it never presses, they orbit.
            Assert.Less(crowdedSteer.x, 0f);
            Assert.Greater(spacedSteer.x, 0f);
        }

        [Test]
        public void NeighboursOutsideTheRadiusAreIgnored()
        {
            var weights = Default;
            var velocities = new[] { Vector3.zero, Vector3.zero };

            // A bird 40m away must not drag on this one, or the flock becomes one global average
            // and the cost grows with the square of the count for no behavioural gain.
            var far = new[] { Vector3.zero, new Vector3(40f, 0f, 0f) };
            Vector3 withFar = BoidsSteering.Steer(0, far, velocities, 2, new Vector3(0f, 0f, 10f), weights);

            var alone = new[] { Vector3.zero };
            Vector3 soloVelocities0 = Vector3.zero;
            Vector3 solo = BoidsSteering.Steer(0, alone, new[] { soloVelocities0 }, 1, new Vector3(0f, 0f, 10f), weights);

            Assert.AreEqual(solo.x, withFar.x, 0.001f);
            Assert.AreEqual(solo.z, withFar.z, 0.001f);
        }

        [Test]
        public void ABoidStandingOnTheTargetProducesNoNaN()
        {
            var positions = new[] { Vector3.zero };
            var velocities = new[] { Vector3.zero };

            Vector3 steer = BoidsSteering.Steer(0, positions, velocities, 1, Vector3.zero, Default);

            Assert.IsFalse(float.IsNaN(steer.x) || float.IsNaN(steer.z), "a zero-length pull must not normalize to NaN");
            Assert.AreEqual(Vector3.zero, steer);
        }

        [Test]
        public void OutOfRangeIndicesAreRefusedRatherThanThrowing()
        {
            var positions = new[] { Vector3.zero };
            var velocities = new[] { Vector3.zero };

            // The roster changes as birds die, and a stale index must never take the game down.
            Assert.AreEqual(Vector3.zero, BoidsSteering.Steer(5, positions, velocities, 1, Vector3.one, Default));
            Assert.AreEqual(Vector3.zero, BoidsSteering.Steer(-1, positions, velocities, 1, Vector3.one, Default));
            Assert.AreEqual(Vector3.zero, BoidsSteering.Steer(0, null, velocities, 1, Vector3.one, Default));
        }

        [Test]
        public void TheResultIsNormalisedSoSpeedIsTheCallersToDecide()
        {
            var positions = new[] { Vector3.zero, new Vector3(0.3f, 0f, 0.1f), new Vector3(-0.4f, 0f, 0.2f) };
            var velocities = new[] { Vector3.zero, Vector3.one, -Vector3.one };

            Vector3 steer = BoidsSteering.Steer(0, positions, velocities, 3, new Vector3(9f, 0f, 4f), Default);

            Assert.AreEqual(1f, steer.magnitude, 0.001f, "weights decide direction; the definition's MoveSpeed decides speed");
        }
    }
}
