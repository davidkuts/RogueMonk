using System;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Engine-free camera look-ahead: a damped, rate-limited offset that leads the player
    /// along its <em>velocity</em>, not its facing. Driving it from velocity matters — on a
    /// direction reversal the velocity passes through zero, so the offset retracts along a
    /// straight line and returns. A facing-driven offset would instead sweep a lateral arc
    /// as the capsule spins, which reads as a nauseating camera swing.
    /// </summary>
    public sealed class LookAheadTracker
    {
        readonly ICameraLookAheadSettings settings;
        Vector3 smoothVelocity;

        /// <summary>Current planar offset to add to the player position.</summary>
        public Vector3 Offset { get; private set; }

        public LookAheadTracker(ICameraLookAheadSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Advances the offset. <paramref name="velocityFraction"/> is planar velocity divided
        /// by top speed, so its magnitude is 0..1 and its direction is the travel direction.
        /// </summary>
        public void Tick(Vector3 velocityFraction, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            velocityFraction.y = 0f;
            Vector3 target = Vector3.ClampMagnitude(velocityFraction, 1f) * settings.LookAheadDistance;

            Offset = Vector3.SmoothDamp(
                Offset, target, ref smoothVelocity, settings.LookAheadSmoothTime,
                float.PositiveInfinity, deltaTime);
        }

        public void Reset()
        {
            Offset = Vector3.zero;
            smoothVelocity = Vector3.zero;
        }
    }
}
