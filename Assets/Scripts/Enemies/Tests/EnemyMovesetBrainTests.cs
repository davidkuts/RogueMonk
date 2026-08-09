using System.Collections.Generic;
using Game.Combat;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// The multi-move trash brain. What is worth pinning here is the behaviour that a playtest
    /// would only ever show as "that felt wrong": a chain that drops halfway, a cooldown spent on
    /// an attack that never happened, and RNG draws that depend on how the player moved.
    /// </summary>
    public class EnemyMovesetBrainTests
    {
        const float Step = 1f / 60f;

        static EnemyMovesetBrain Make(
            out FakeEnemyDefinition definition,
            IReadOnlyList<IEnemyMove> moves,
            uint seed = 7u)
        {
            definition = new FakeEnemyDefinition { AggroRange = 12f, AttackRange = 2.4f, AttackCooldownSeconds = 1f };
            return new EnemyMovesetBrain(definition, moves, new XorShiftRandom(seed));
        }

        /// <summary>Runs the brain until it commits, or gives up. Returns the move it chose.</summary>
        static IEnemyMove RunUntilAttack(EnemyMovesetBrain brain, float distance, int maxSteps = 600)
        {
            for (int i = 0; i < maxSteps; i++)
            {
                brain.Tick(Step, distance, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
                if (brain.WantsToAttack)
                    return brain.CurrentMove;
            }

            return null;
        }

        [Test]
        public void ChoosesOnlyAMoveWhoseRangeBandContainsTheTarget()
        {
            var near = new FakeEnemyMove { Id = "bite", MinRange = 0f, MaxRange = 2f };
            var far = new FakeEnemyMove { Id = "pounce", MinRange = 4f, MaxRange = 8f };

            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { near, far });

            Assert.AreEqual("pounce", RunUntilAttack(brain, distance: 6f)?.Id);
        }

        [Test]
        public void DoesNotAttackWhileTheAttackTokenIsDenied()
        {
            var move = new FakeEnemyMove { MaxRange = 3f };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { move });

            for (int i = 0; i < 120; i++)
            {
                brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: false);
                Assert.IsFalse(brain.WantsToAttack, "an enemy with no token must never commit");
            }

            Assert.AreEqual(EnemyState.Waiting, brain.State);

            // And the moment a slot frees up it goes.
            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
            Assert.IsTrue(brain.WantsToAttack);
        }

        [Test]
        public void ADeniedTokenSpendsNoCooldownAndNoRandomness()
        {
            var move = new FakeEnemyMove { MaxRange = 3f, MoveCooldownSeconds = 5f };

            var definition = new FakeEnemyDefinition { AggroRange = 12f, AttackCooldownSeconds = 1f };
            var random = new XorShiftRandom(99u);
            var brain = new EnemyMovesetBrain(definition, new IEnemyMove[] { move }, random);

            for (int i = 0; i < 300; i++)
                brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: false);

            // The whole reason the token is a Tick argument rather than a veto after the fact:
            // committing and then being refused would burn the move's cooldown on an attack that
            // never happened, and the enemy would stand there for five seconds looking broken.
            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
            Assert.IsTrue(brain.WantsToAttack, "the move must still be off cooldown after a long wait for a token");
        }

        [Test]
        public void WaitingForATokenStillRepositions()
        {
            var move = new FakeEnemyMove { MinRange = 0f, MaxRange = 2f };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { move });

            brain.Tick(Step, 6f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: false);

            // A pack that freezes solid while it waits its turn reads as broken; the circling is
            // what makes the flankers threatening in the first place.
            Assert.Greater(brain.MoveSpeedFraction, 0f);
        }

        [Test]
        public void BacksAwayWhenTheTargetIsInsideItsMinimumRange()
        {
            var move = new FakeEnemyMove { Id = "spit", MinRange = 5f, MaxRange = 9f };

            var definition = new FakeEnemyDefinition { AggroRange = 20f };
            definition.Ranged = new RangedProfile { KiteSpeedFraction = 0.7f };

            var brain = new EnemyMovesetBrain(definition, new IEnemyMove[] { move }, new XorShiftRandom(3u));
            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);

            Assert.Less(brain.MoveSpeedFraction, 0f, "Sailspit backpedals when the player closes");
            Assert.AreEqual(EnemyState.Reposition, brain.State);
            Assert.AreEqual(-0.7f, brain.MoveSpeedFraction, 0.0001f, "backing off is slower than closing, so it can be cornered");
        }

        [Test]
        public void AChainRunsEveryLinkInOrderWithoutReChoosing()
        {
            var move = new FakeEnemyMove { Id = "snap", MaxRange = 3f, Links = FakeEnemyMove.Chain(2), LinkDelaySeconds = 0.1f };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { move });

            RunUntilAttack(brain, 2f);
            Assert.AreEqual("link0", brain.PendingAttack.Id);
            Assert.AreEqual(0, brain.LinkIndex);

            brain.NotifyLinkFinished();

            for (int i = 0; i < 60; i++)
            {
                brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: false);
                if (brain.WantsToAttack)
                    break;
            }

            // Note attackPermitted was false: a chain already holds its token, so the second half
            // of a combo must never be blocked by the queue it is already at the front of.
            Assert.IsTrue(brain.WantsToAttack);
            Assert.AreEqual("link1", brain.PendingAttack.Id);
            Assert.AreEqual(1, brain.LinkIndex);
        }

        [Test]
        public void AStaggerAbandonsTheRestOfAChain()
        {
            var move = new FakeEnemyMove { Id = "snap", MaxRange = 3f, Links = FakeEnemyMove.Chain(3) };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { move });

            RunUntilAttack(brain, 2f);
            brain.NotifyLinkFinished();

            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: true, attackPermitted: true);

            Assert.AreEqual(EnemyState.Staggered, brain.State);
            Assert.IsNull(brain.CurrentMove, "resuming link two would deliver the back half of a combo the player already broke");
            Assert.IsFalse(brain.IsMidChain);
        }

        [Test]
        public void TheSpawnGraceStopsAnAttackButNotTheChase()
        {
            var move = new FakeEnemyMove { MaxRange = 3f };

            var definition = new FakeEnemyDefinition { AggroRange = 12f, SpawnGraceSeconds = 1.2f };
            var brain = new EnemyMovesetBrain(definition, new IEnemyMove[] { move }, new XorShiftRandom(11u));

            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
            Assert.IsFalse(brain.WantsToAttack, "a body that just materialised must not swing before it can be read");

            // WantsToAttack is true for exactly the frame it commits, so this has to catch the
            // edge rather than sample the state afterwards — by then the move is on its cooldown
            // and the flag has already cleared.
            float elapsed = Step;
            bool committed = false;

            for (int i = 0; i < 200 && !committed; i++)
            {
                brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
                elapsed += Step;
                committed = brain.WantsToAttack;
            }

            Assert.IsTrue(committed, "the grace is a delay, not a permanent gate");
            Assert.GreaterOrEqual(elapsed, definition.SpawnGraceSeconds, "it must not swing before the grace is actually up");
        }

        [Test]
        public void RepositioningOutOfRangeConsumesNoRandomness()
        {
            var move = new FakeEnemyMove { MinRange = 0f, MaxRange = 2f };

            var definition = new FakeEnemyDefinition { AggroRange = 30f };
            var random = new XorShiftRandom(17u);
            var brain = new EnemyMovesetBrain(definition, new IEnemyMove[] { move }, random);

            for (int i = 0; i < 500; i++)
                brain.Tick(Step, 20f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);

            // If kiting drew from the stream, how long a player spent at range would change every
            // later draw in the run and the seed would stop reproducing it. Nothing in generation
            // would catch that, because generation is long finished by then.
            var reference = new XorShiftRandom(17u);
            Assert.AreEqual(reference.NextFloat(), random.NextFloat(), 1e-9f,
                "an enemy that never attacked must have consumed no draws");
        }

        [Test]
        public void RepeatsAreDiscouragedButNeverForbiddenWhenNothingElseIsLegal()
        {
            var only = new FakeEnemyMove { Id = "bite", MaxRange = 3f, MoveCooldownSeconds = 0f };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { only });

            Assert.AreEqual("bite", RunUntilAttack(brain, 2f)?.Id);
            brain.NotifyLinkFinished();

            // The repeat penalty zeroes the only legal weight; repeating beats standing still.
            Assert.AreEqual("bite", RunUntilAttack(brain, 2f)?.Id);
        }

        [Test]
        public void AMoveOnCooldownIsNotChosenAgainImmediately()
        {
            var quick = new FakeEnemyMove { Id = "quick", MaxRange = 3f, MoveCooldownSeconds = 10f };
            var slow = new FakeEnemyMove { Id = "slow", MaxRange = 3f, MoveCooldownSeconds = 0f };

            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { quick, slow });

            var seen = new HashSet<string>();
            for (int round = 0; round < 6; round++)
            {
                IEnemyMove chosen = RunUntilAttack(brain, 2f);
                if (chosen != null)
                    seen.Add(chosen.Id);
                brain.NotifyLinkFinished();
            }

            Assert.IsTrue(seen.Contains("slow"));
            Assert.LessOrEqual(seen.Count, 2);
        }

        [Test]
        public void GoesIdleAndDropsItsMoveWhenTheTargetLeavesAggroRange()
        {
            var move = new FakeEnemyMove { Id = "bite", MaxRange = 3f, Links = FakeEnemyMove.Chain(2) };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { move });

            RunUntilAttack(brain, 2f);
            brain.NotifyLinkFinished();

            brain.Tick(Step, 50f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);

            Assert.AreEqual(EnemyState.Idle, brain.State);
            Assert.IsNull(brain.CurrentMove);
        }

        [Test]
        public void ADeadBrainStopsWantingAnything()
        {
            var move = new FakeEnemyMove { MaxRange = 3f };
            EnemyMovesetBrain brain = Make(out _, new IEnemyMove[] { move });

            brain.NotifyDied();
            brain.Tick(Step, 1f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);

            Assert.IsFalse(brain.WantsToAttack);
            Assert.AreEqual(0f, brain.MoveSpeedFraction);
        }

        [Test]
        public void TheInitialCooldownJitterDelaysTheFirstAttack()
        {
            var move = new FakeEnemyMove { MaxRange = 3f };
            var definition = new FakeEnemyDefinition { AggroRange = 12f, SpawnGraceSeconds = 0f };

            var brain = new EnemyMovesetBrain(
                definition, new IEnemyMove[] { move }, new XorShiftRandom(31u), 0.45f, initialCooldownJitter: 2f);

            Assert.Greater(brain.CooldownRemaining, 0f, "a jittered body must not be ready on its first frame");
            Assert.LessOrEqual(brain.CooldownRemaining, 2f, "the jitter must stay inside its stated bound");

            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
            Assert.IsFalse(brain.WantsToAttack);
        }

        [Test]
        public void TwoBodiesOnDifferentStreamsDoNotShareAnAttackClock()
        {
            var definition = new FakeEnemyDefinition { AggroRange = 12f };

            // The point of the jitter. Two Swiftjaws spawned in the same frame otherwise run
            // identical timers forever — they commit together and recover together, and the player
            // faces one doubled threat on a metronome instead of the offset pair the design asks
            // for. The attack token decides who goes first; it does not keep them out of phase.
            var a = new EnemyMovesetBrain(
                definition, new IEnemyMove[] { new FakeEnemyMove() }, new XorShiftRandom(101u), 0.45f, 2f);
            var b = new EnemyMovesetBrain(
                definition, new IEnemyMove[] { new FakeEnemyMove() }, new XorShiftRandom(202u), 0.45f, 2f);

            Assert.AreNotEqual(a.CooldownRemaining, b.CooldownRemaining);
        }

        [Test]
        public void ZeroJitterLeavesTheBrainReadyImmediately()
        {
            var definition = new FakeEnemyDefinition { AggroRange = 12f, SpawnGraceSeconds = 0f };
            var brain = new EnemyMovesetBrain(
                definition, new IEnemyMove[] { new FakeEnemyMove { MaxRange = 3f } }, new XorShiftRandom(5u), 0.45f, 0f);

            // Default, so every enemy authored without a jitter behaves exactly as before — and,
            // importantly, consumes no draw, keeping their streams byte-identical.
            Assert.AreEqual(0f, brain.CooldownRemaining);

            brain.Tick(Step, 2f, hasTarget: true, isAttacking: false, isStaggered: false, attackPermitted: true);
            Assert.IsTrue(brain.WantsToAttack);
        }

        [Test]
        public void TheSameSeedReplaysTheSameSequenceOfMoves()
        {
            string[] Run(uint seed)
            {
                var moves = new IEnemyMove[]
                {
                    new FakeEnemyMove { Id = "a", MaxRange = 3f, MoveCooldownSeconds = 0.2f },
                    new FakeEnemyMove { Id = "b", MaxRange = 3f, MoveCooldownSeconds = 0.2f },
                    new FakeEnemyMove { Id = "c", MaxRange = 3f, MoveCooldownSeconds = 0.2f },
                };

                EnemyMovesetBrain brain = Make(out _, moves, seed);

                var chosen = new List<string>();
                for (int i = 0; i < 8; i++)
                {
                    IEnemyMove move = RunUntilAttack(brain, 2f);
                    chosen.Add(move?.Id ?? "-");
                    brain.NotifyLinkFinished();
                }

                return chosen.ToArray();
            }

            CollectionAssert.AreEqual(Run(4242u), Run(4242u));
        }
    }
}
