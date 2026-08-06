using NUnit.Framework;

namespace Game.Enemies.Tests
{
    public class MeleeEnemyBrainTests
    {
        const float Step = 1f / 60f;

        static MeleeEnemyBrain Make(out FakeEnemyDefinition definition)
        {
            definition = new FakeEnemyDefinition { AggroRange = 12f, AttackRange = 2.4f, AttackCooldownSeconds = 1.1f };
            return new MeleeEnemyBrain(definition);
        }

        [Test]
        public void IdlesWithoutATarget()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.Tick(Step, 5f, hasTarget: false, isAttacking: false, isStaggered: false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        [Test]
        public void IdlesBeyondAggroRange()
        {
            MeleeEnemyBrain brain = Make(out FakeEnemyDefinition definition);
            brain.Tick(Step, definition.AggroRange + 1f, true, false, false);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void ChasesInsideAggroButOutsideAttackRange()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.Tick(Step, 6f, true, false, false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(1f));
            Assert.That(brain.WantsToAttack, Is.False);
        }

        [Test]
        public void CommitsToAnAttackInRange()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.Tick(Step, 2f, true, false, false);

            Assert.That(brain.WantsToAttack, Is.True);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Attacking));
        }

        [Test]
        public void WantsToAttackIsASingleFrameEdge()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.Tick(Step, 2f, true, false, false);
            brain.Tick(Step, 2f, true, true, false); // now attacking

            Assert.That(brain.WantsToAttack, Is.False, "the edge must not re-fire while the attack runs");
        }

        [Test]
        public void DoesNotMoveWhileAttacking()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.Tick(Step, 6f, true, isAttacking: true, isStaggered: false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Attacking));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        [Test]
        public void CooldownBlocksTheNextAttack_GuaranteeingAPunishWindow()
        {
            MeleeEnemyBrain brain = Make(out FakeEnemyDefinition definition);
            brain.NotifyAttackFinished();

            brain.Tick(Step, 2f, true, false, false);
            Assert.That(brain.WantsToAttack, Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Cooldown));

            brain.Tick(definition.AttackCooldownSeconds, 2f, true, false, false);
            brain.Tick(Step, 2f, true, false, false);
            Assert.That(brain.WantsToAttack, Is.True);
        }

        [Test]
        public void HoldsPositionInRangeDuringCooldown()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.NotifyAttackFinished();
            brain.Tick(Step, 2f, true, false, false);

            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f), "it should not jitter in and out of range");
        }

        [Test]
        public void StillChasesWhileOnCooldownIfTheTargetRunsAway()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.NotifyAttackFinished();
            brain.Tick(Step, 8f, true, false, false);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(1f));
        }

        [Test]
        public void StaggerOverridesEverything()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.Tick(Step, 1f, true, isAttacking: false, isStaggered: true);

            Assert.That(brain.State, Is.EqualTo(EnemyState.Staggered));
            Assert.That(brain.WantsToAttack, Is.False);
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        [Test]
        public void InterruptionImposesTheCooldown_NoInstantRetaliation()
        {
            // Being staggered out of a wind-up must not let the enemy swing the instant it recovers.
            MeleeEnemyBrain brain = Make(out _);
            brain.NotifyInterrupted();

            brain.Tick(Step, 2f, true, false, false);
            Assert.That(brain.WantsToAttack, Is.False);
            Assert.That(brain.CooldownRemaining, Is.GreaterThan(0f));
        }

        [Test]
        public void StateChangedFiresOnTransitionsOnly()
        {
            MeleeEnemyBrain brain = Make(out _);
            int changes = 0;
            brain.StateChanged += (_, __) => changes++;

            brain.Tick(Step, 6f, true, false, false); // Idle -> Chase
            brain.Tick(Step, 6f, true, false, false); // still Chase
            brain.Tick(Step, 6f, true, false, false);

            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void CooldownDoesNotTickDownWhileAttacking()
        {
            MeleeEnemyBrain brain = Make(out _);
            brain.NotifyAttackFinished();
            float before = brain.CooldownRemaining;

            brain.Tick(0.5f, 2f, true, isAttacking: true, isStaggered: false);

            Assert.That(brain.CooldownRemaining, Is.EqualTo(before).Within(1e-4f));
        }
    }
}
