using Game.Core.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class PlayerLocomotionTests
    {
        const float Step = 1f / 60f;
        const float Tolerance = 1e-3f;

        static PlayerLocomotion Make(out FakeMovementSettings settings)
        {
            settings = new FakeMovementSettings();
            return new PlayerLocomotion(settings);
        }

        static void Run(PlayerLocomotion locomotion, Vector2 input, float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
                locomotion.Tick(input, Step);
        }

        [Test]
        public void StartsAtRest_FacingForward()
        {
            PlayerLocomotion locomotion = Make(out _);
            Assert.That(locomotion.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(locomotion.Facing, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void HeldInput_AcceleratesToMaxSpeed()
        {
            PlayerLocomotion locomotion = Make(out FakeMovementSettings settings);
            Run(locomotion, Vector2.up, 1f);
            Assert.That(locomotion.Velocity.magnitude, Is.EqualTo(settings.MaxSpeed).Within(Tolerance));
            Assert.That(locomotion.NormalizedSpeed, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Velocity_NeverExceedsMaxSpeed()
        {
            PlayerLocomotion locomotion = Make(out FakeMovementSettings settings);
            // Diagonal keyboard input must not outrun cardinal input.
            Run(locomotion, new Vector2(1f, 1f), 2f);
            Assert.That(locomotion.Velocity.magnitude, Is.LessThanOrEqualTo(settings.MaxSpeed + Tolerance));
        }

        [Test]
        public void ReleasingInput_DeceleratesToRest()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.up, 1f);
            Run(locomotion, Vector2.zero, 1f);
            Assert.That(locomotion.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InputMapsToWorldAxes()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.right, 1f);
            Assert.That(locomotion.Velocity.x, Is.GreaterThan(0f));
            Assert.That(locomotion.Velocity.z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Velocity_IsAlwaysPlanar()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, new Vector2(0.4f, -0.9f), 0.5f);
            Assert.That(locomotion.Velocity.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void DeadzoneInput_DoesNotMove()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, new Vector2(0.1f, 0f), 1f);
            Assert.That(locomotion.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Facing_TurnsTowardInputDirection()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.right, 1f);
            Assert.That(Vector3.Angle(locomotion.Facing, Vector3.right), Is.LessThan(0.5f));
        }

        [Test]
        public void Facing_TurnRate_IsRateLimited()
        {
            PlayerLocomotion locomotion = Make(out FakeMovementSettings settings);
            settings.TurnSpeedDegPerSec = 90f;
            locomotion.Tick(Vector2.right, 0.5f); // 45° of the 90° turn
            Assert.That(Vector3.Angle(Vector3.forward, locomotion.Facing), Is.EqualTo(45f).Within(0.5f));
        }

        [Test]
        public void Facing_IsUnchangedWithoutInput()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.right, 1f);
            Vector3 facing = locomotion.Facing;
            Run(locomotion, Vector2.zero, 1f);
            Assert.That(locomotion.Facing, Is.EqualTo(facing));
        }

        [Test]
        public void Facing_StaysNormalizedAndPlanar()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, new Vector2(-0.7f, 0.7f), 1f);
            Assert.That(locomotion.Facing.magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(locomotion.Facing.y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void NormalizedVelocity_TracksDirectionAndFraction()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.right, 1f);
            Assert.That(locomotion.NormalizedVelocity.x, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(locomotion.NormalizedVelocity.z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void NormalizedVelocity_IsClampedToUnitLength()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, new Vector2(1f, 1f), 2f);
            Assert.That(locomotion.NormalizedVelocity.magnitude, Is.LessThanOrEqualTo(1f + Tolerance));
        }

        [Test]
        public void SpeedMultiplier_ScalesTheResultingSpeedLinearly()
        {
            // Scaling the input instead would run it back through the deadzone and response
            // curve, so "half speed" would silently come out well below half.
            PlayerLocomotion locomotion = Make(out FakeMovementSettings settings);
            for (int i = 0; i < 120; i++)
                locomotion.Tick(Vector2.up, Step, speedMultiplier: 0.5f);

            Assert.That(locomotion.Velocity.magnitude, Is.EqualTo(settings.MaxSpeed * 0.5f).Within(Tolerance));
        }

        [Test]
        public void SpeedMultiplierZero_BringsThePlayerToRest()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.up, 1f);
            for (int i = 0; i < 120; i++)
                locomotion.Tick(Vector2.up, Step, speedMultiplier: 0f);

            Assert.That(locomotion.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void AllowTurningFalse_MovesWithoutRotating()
        {
            // Strafing during an attack must not spin the character off its aim target.
            PlayerLocomotion locomotion = Make(out _);
            Vector3 facingBefore = locomotion.Facing;

            for (int i = 0; i < 60; i++)
                locomotion.Tick(Vector2.right, Step, speedMultiplier: 1f, allowTurning: false);

            Assert.That(locomotion.Velocity.x, Is.GreaterThan(0f), "the player should still move");
            Assert.That(locomotion.Facing, Is.EqualTo(facingBefore), "but facing must be untouched");
        }

        [Test]
        public void AllowTurningTrue_IsTheDefault()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.right, 1f);
            Assert.That(Vector3.Angle(locomotion.Facing, Vector3.right), Is.LessThan(0.5f));
        }

        [Test]
        public void Halt_ZeroesVelocityButKeepsFacing()
        {
            PlayerLocomotion locomotion = Make(out _);
            Run(locomotion, Vector2.right, 1f);
            Vector3 facing = locomotion.Facing;
            locomotion.Halt();
            Assert.That(locomotion.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(locomotion.Facing, Is.EqualTo(facing));
        }

        [Test]
        public void SetFacing_IgnoresDegenerateDirection()
        {
            PlayerLocomotion locomotion = Make(out _);
            locomotion.SetFacing(Vector3.up);
            Assert.That(locomotion.Facing, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void ZeroDeltaTime_IsANoOp()
        {
            PlayerLocomotion locomotion = Make(out _);
            locomotion.Tick(Vector2.up, 0f);
            Assert.That(locomotion.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void SimulationIsFrameRateIndependent_WithinTolerance()
        {
            PlayerLocomotion at60 = Make(out _);
            PlayerLocomotion at30 = Make(out _);
            for (int i = 0; i < 60; i++) at60.Tick(Vector2.up, 1f / 60f);
            for (int i = 0; i < 30; i++) at30.Tick(Vector2.up, 1f / 30f);
            Assert.That(at60.Velocity.magnitude, Is.EqualTo(at30.Velocity.magnitude).Within(Tolerance));
        }
    }
}
