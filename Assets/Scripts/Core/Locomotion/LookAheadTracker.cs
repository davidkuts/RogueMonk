using System;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Engine-free camera look-ahead: a damped offset that leads the player in its facing
    /// direction, scaled by how fast it is moving. Standing still pulls the offset back to zero.
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

        public void Tick(Vector3 facing, float normalizedSpeed, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            facing.y = 0f;
            Vector3 target = facing.sqrMagnitude > 0f
                ? facing.normalized * (settings.LookAheadDistance * Mathf.Clamp01(normalizedSpeed))
                : Vector3.zero;

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
