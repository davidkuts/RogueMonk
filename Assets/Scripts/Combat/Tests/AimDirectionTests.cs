using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// Covers the precedence rule behind "my punch does not go where I am pointing".
    /// </summary>
    public class AimDirectionTests
    {
        const float Deadzone = 0.25f;

        [Test]
        public void TheStickWinsOverAnAcquiredTarget()
        {
            // The bug this encodes: auto-aim held facing while the player's stick was ignored, so a
            // punch thrown after dashing past an enemy flew off in the dash direction.
            Vector3 toTarget = Vector3.forward * 3f;

            bool resolved = AimAssist.TryResolveAimDirection(
                new Vector2(-1f, 0f), Deadzone, toTarget, hasTarget: true, out Vector3 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction.x, Is.EqualTo(-1f).Within(1e-4f), "the stick, not the target");
            Assert.That(direction.z, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void WithNoStickTheTargetIsUsed()
        {
            bool resolved = AimAssist.TryResolveAimDirection(
                Vector2.zero, Deadzone, new Vector3(0f, 0f, 5f), hasTarget: true, out Vector3 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction.z, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void WithNoStickAndNoTargetFacingIsLeftAlone()
        {
            bool resolved = AimAssist.TryResolveAimDirection(
                Vector2.zero, Deadzone, Vector3.zero, hasTarget: false, out Vector3 direction);

            Assert.That(resolved, Is.False);
            Assert.That(direction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheStickStillWinsWhenNoTargetWasAcquired()
        {
            // The exact failure case: no enemy inside the auto-aim cone at attack start used to
            // mean facing was frozen for the whole attack with no way to correct it.
            bool resolved = AimAssist.TryResolveAimDirection(
                new Vector2(0f, -1f), Deadzone, Vector3.zero, hasTarget: false, out Vector3 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction.z, Is.EqualTo(-1f).Within(1e-4f));
        }

        [Test]
        public void ADriftingStickInsideTheDeadzoneDoesNotStealAimFromTheTarget()
        {
            bool resolved = AimAssist.TryResolveAimDirection(
                new Vector2(0.1f, 0.05f), Deadzone, new Vector3(0f, 0f, 4f), hasTarget: true, out Vector3 direction);

            Assert.That(resolved, Is.True);
            Assert.That(direction.z, Is.EqualTo(1f).Within(1e-4f), "resting-stick noise must not override auto-aim");
        }

        [Test]
        public void TheResolvedDirectionIsAlwaysFlatAndNormalised()
        {
            AimAssist.TryResolveAimDirection(
                new Vector2(0.6f, 0.6f), Deadzone, Vector3.zero, hasTarget: false, out Vector3 fromStick);
            Assert.That(fromStick.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fromStick.y, Is.EqualTo(0f));

            AimAssist.TryResolveAimDirection(
                Vector2.zero, Deadzone, new Vector3(3f, 9f, 4f), hasTarget: true, out Vector3 fromTarget);
            Assert.That(fromTarget.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fromTarget.y, Is.EqualTo(0f), "height difference must not tilt an attack");
        }

        [Test]
        public void ATargetAtTheExactSamePositionIsNotAimedAt()
        {
            bool resolved = AimAssist.TryResolveAimDirection(
                Vector2.zero, Deadzone, Vector3.zero, hasTarget: true, out _);

            Assert.That(resolved, Is.False, "a zero direction would blank the character's facing");
        }
    }
}
