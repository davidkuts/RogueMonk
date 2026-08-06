using System;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Engine-free planar locomotion simulation: conditions stick input, accelerates
    /// toward the desired velocity, and rotates facing toward the movement direction.
    /// Produces a velocity; it never moves anything itself — the MonoBehaviour adapter
    /// feeds the result into CharacterController.Move.
    /// </summary>
    public sealed class PlayerLocomotion
    {
        readonly ILocomotionSettings settings;

        /// <summary>Planar (XZ) velocity in m/s. Y is always 0 — gravity is the adapter's job.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>Normalized planar facing direction. Never zero.</summary>
        public Vector3 Facing { get; private set; } = Vector3.forward;

        /// <summary>Current speed as a 0..1 fraction of MaxSpeed.</summary>
        public float NormalizedSpeed =>
            settings.MaxSpeed <= 0f ? 0f : Mathf.Clamp01(Velocity.magnitude / settings.MaxSpeed);

        /// <summary>Velocity as a fraction of MaxSpeed — direction of travel, magnitude 0..1.</summary>
        public Vector3 NormalizedVelocity =>
            settings.MaxSpeed <= 0f ? Vector3.zero : Vector3.ClampMagnitude(Velocity / settings.MaxSpeed, 1f);

        public PlayerLocomotion(ILocomotionSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Advances the simulation. <paramref name="rawMoveInput"/> is the unconditioned
        /// stick/WASD vector; X maps to world +X and Y to world +Z, which is correct because
        /// the gameplay camera yaw is fixed at 0 (see DESIGN.md § Camera).
        /// </summary>
        public void Tick(Vector2 rawMoveInput, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Vector2 conditioned = InputCurve.Condition(
                rawMoveInput, settings.InputDeadzone, settings.InputResponseExponent);

            Vector3 desired = new Vector3(conditioned.x, 0f, conditioned.y) * settings.MaxSpeed;
            bool hasInput = desired.sqrMagnitude > 0f;

            float rate = hasInput ? settings.Acceleration : settings.Deceleration;
            Velocity = Vector3.MoveTowards(Velocity, desired, rate * deltaTime);

            if (hasInput)
                TurnToward(desired.normalized, deltaTime);
        }

        /// <summary>Zeroes velocity without touching facing (used on room transitions / respawn).</summary>
        public void Halt()
        {
            Velocity = Vector3.zero;
        }

        /// <summary>
        /// Overrides the planar velocity — used to hand momentum back after a dash so the
        /// player exits running rather than from a standstill. Not clamped to MaxSpeed:
        /// an over-speed exit is a legitimate feel choice, and Tick decelerates it back.
        /// </summary>
        public void SetVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            Velocity = velocity;
        }

        /// <summary>Forces facing to a planar direction. Ignored if the direction is degenerate.</summary>
        public void SetFacing(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0f)
                Facing = direction.normalized;
        }

        void TurnToward(Vector3 target, float deltaTime)
        {
            float maxRadians = settings.TurnSpeedDegPerSec * Mathf.Deg2Rad * deltaTime;
            Vector3 turned = Vector3.RotateTowards(Facing, target, maxRadians, 0f);
            turned.y = 0f;
            if (turned.sqrMagnitude > 0f)
                Facing = turned.normalized;
        }
    }
}
