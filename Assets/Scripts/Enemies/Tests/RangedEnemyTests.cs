using NUnit.Framework;
using UnityEngine;

namespace Game.Enemies.Tests
{
    public class RangedEnemyBrainTests
    {
        const float Step = 1f / 60f;

        static RangedEnemyBrain Make(out FakeEnemyDefinition definition)
        {
            definition = new FakeEnemyDefinition
            {
                AggroRange = 16f,
                AttackCooldownSeconds = 1.6f,
                Ranged = new RangedProfile
                {
                    PreferredMinRange = 5f,
                    PreferredMaxRange = 9f,
                    ProjectileSpeed = 9f,
                    ProjectileLifetime = 4f,
                    ProjectileRadius = 0.35f,
                    KiteSpeedFraction = 0.7f,
                },
            };

            return new RangedEnemyBrain(definition);
        }

        [Test]
        public void IdlesBeyondAggroRange()
        {
            RangedEnemyBrain brain = Make(out FakeEnemyDefinition definition);
            brain.Tick(Step, definition.AggroRange + 1f, true, false, false);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void ClosesInWhenTooFar()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.Tick(Step, 12f, true, false, false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(1f), "positive means closing");
        }

        [Test]
        public void BacksAwayWhenTooClose()
        {
            RangedEnemyBrain brain = Make(out FakeEnemyDefinition definition);
            brain.Tick(Step, 2f, true, false, false);

            Assert.That(brain.MoveSpeedFraction, Is.LessThan(0f), "negative means retreating");
            Assert.That(brain.MoveSpeedFraction,
                Is.EqualTo(-definition.Ranged.KiteSpeedFraction).Within(1e-4f));
        }

        [Test]
        public void RetreatIsSlowerThanAdvance_SoThePlayerCanAlwaysCornerIt()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.Tick(Step, 12f, true, false, false);
            float advance = brain.MoveSpeedFraction;

            brain.Tick(Step, 2f, true, false, false);
            float retreat = Mathf.Abs(brain.MoveSpeedFraction);

            Assert.That(retreat, Is.LessThan(advance));
        }

        [Test]
        public void FiresInsideThePreferredBand()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.Tick(Step, 7f, true, false, false);

            Assert.That(brain.WantsToAttack, Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attacking));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f), "it should plant to shoot");
        }

        [Test]
        public void HoldsStillWhileShooting_TheWindupMustNotSlideAway()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.Tick(Step, 2f, true, isAttacking: true, isStaggered: false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attacking));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f), "even inside the min range it stays put mid-telegraph");
        }

        [Test]
        public void CooldownGatesTheNextShot()
        {
            RangedEnemyBrain brain = Make(out FakeEnemyDefinition definition);
            brain.NotifyAttackFinished();

            brain.Tick(Step, 7f, true, false, false);
            Assert.That(brain.WantsToAttack, Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Cooldown));

            brain.Tick(definition.AttackCooldownSeconds, 7f, true, false, false);
            brain.Tick(Step, 7f, true, false, false);
            Assert.That(brain.WantsToAttack, Is.True);
        }

        [Test]
        public void StillRepositionsWhileOnCooldown()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.NotifyAttackFinished();
            brain.Tick(Step, 2f, true, false, false);

            Assert.That(brain.MoveSpeedFraction, Is.LessThan(0f), "kiting does not wait for the cooldown");
        }

        [Test]
        public void StaggerOverridesEverything()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.Tick(Step, 7f, true, false, isStaggered: true);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Staggered));
            Assert.That(brain.WantsToAttack, Is.False);
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        [Test]
        public void InterruptionImposesTheCooldown()
        {
            RangedEnemyBrain brain = Make(out _);
            brain.NotifyInterrupted();

            brain.Tick(Step, 7f, true, false, false);
            Assert.That(brain.WantsToAttack, Is.False);
        }

        [Test]
        public void TheBandHasHysteresis_NoJitterAtOneDistance()
        {
            // Standing anywhere inside the band produces no movement at all, so the enemy
            // cannot oscillate between advance and retreat.
            RangedEnemyBrain brain = Make(out _);
            foreach (float distance in new[] { 5.1f, 6f, 7f, 8f, 8.9f })
            {
                brain.Tick(Step, distance, true, false, false);
                Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f), $"distance {distance} should be settled");
            }
        }
    }

    public class ProjectileMotionTests
    {
        [Test]
        public void TravelsAlongItsHeadingAtSpeed()
        {
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 5f);
            motion.Tick(0.5f);

            Assert.That(motion.Position.z, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(motion.DistanceTravelled, Is.EqualTo(5f).Within(1e-3f));
        }

        [Test]
        public void HeadingIsNormalizedAndPlanar()
        {
            var motion = new ProjectileMotion(Vector3.zero, new Vector3(3f, 9f, 4f), 10f, 5f);
            Assert.That(motion.Direction.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(motion.Direction.y, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void DoesNotHome_TheHeadingIsFixedAtLaunch()
        {
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 5f);
            Vector3 first = motion.Tick(0.1f);
            Vector3 second = motion.Tick(0.1f);

            Assert.That(Vector3.Angle(first, second), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void ExpiresAfterItsLifetime()
        {
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 1f);
            motion.Tick(0.9f);
            Assert.That(motion.Expired, Is.False);

            motion.Tick(0.2f);
            Assert.That(motion.Expired, Is.True);
        }

        [Test]
        public void TheFinalStepIsClippedToTheRemainingLifetime()
        {
            // Otherwise a projectile overshoots its stated range on the frame it dies.
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 1f);
            motion.Tick(10f);

            Assert.That(motion.DistanceTravelled, Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void ExpiredProjectilesStopMoving()
        {
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 0.1f);
            motion.Tick(1f);
            Vector3 resting = motion.Position;

            Assert.That(motion.Tick(1f), Is.EqualTo(Vector3.zero));
            Assert.That(motion.Position, Is.EqualTo(resting));
        }

        [Test]
        public void ExpireEndsItImmediately()
        {
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 5f);
            motion.Expire();
            Assert.That(motion.Expired, Is.True);
        }

        [Test]
        public void ZeroDeltaTimeIsANoOp()
        {
            var motion = new ProjectileMotion(Vector3.zero, Vector3.forward, 10f, 5f);
            Assert.That(motion.Tick(0f), Is.EqualTo(Vector3.zero));
        }
    }
}
