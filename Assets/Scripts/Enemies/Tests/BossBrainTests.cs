using System.Collections.Generic;
using Game.Combat;
using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// Drives a <see cref="BossBrain"/> the way <c>BossController</c> does: finish any running
    /// attack, tick, then start whatever the brain asked for. Keeping the order identical is the
    /// point — a harness that ticked first would hide ordering bugs the real controller would hit.
    /// </summary>
    internal sealed class BossHarness
    {
        readonly float attackSeconds;
        bool attacking;
        float attackRemaining;

        public BossHarness(IBossDefinition definition, uint seed = 1234u, float attackSeconds = 0.4f)
        {
            this.attackSeconds = attackSeconds;
            Brain = new BossBrain(definition, new XorShiftRandom(seed));
        }

        public BossBrain Brain { get; }
        public float Distance { get; set; } = 2f;
        public float HealthFraction { get; set; } = 1f;
        public bool HasTarget { get; set; } = true;
        public bool IsAttacking => attacking;

        /// <summary>Ids of every link thrown, in order.</summary>
        public List<string> ThrownLinks { get; } = new List<string>();

        /// <summary>Ids of every move committed to, in order.</summary>
        public List<string> ThrownMoves { get; } = new List<string>();

        public void Step(float deltaTime)
        {
            if (attacking)
            {
                attackRemaining -= deltaTime;
                if (attackRemaining <= 0f)
                {
                    attacking = false;
                    Brain.NotifyLinkFinished();
                }
            }

            IBossMove before = Brain.CurrentMove;
            Brain.Tick(deltaTime, Distance, HasTarget, attacking, HealthFraction);

            if (Brain.WantsToAttack)
            {
                if (!ReferenceEquals(before, Brain.CurrentMove))
                    ThrownMoves.Add(Brain.CurrentMove.Id);

                ThrownLinks.Add(Brain.PendingAttack.Id);
                attacking = true;
                attackRemaining = attackSeconds;
            }
        }

        public void Run(int steps, float deltaTime = 0.05f)
        {
            for (int i = 0; i < steps; i++)
                Step(deltaTime);
        }
    }

    public class BossBrainTests
    {
        static FakeBossDefinition TwoMovesInOneBand()
        {
            return new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                RepeatWeightMultiplier = 1f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "a", MinRange = 0f, MaxRange = 5f, MoveCooldownSeconds = 0f },
                    new FakeBossMove { Id = "b", MinRange = 0f, MaxRange = 5f, MoveCooldownSeconds = 0f },
                },
            };
        }

        // --- Determinism -------------------------------------------------------------------

        [Test]
        public void SameSeedProducesTheSameMoveSequence()
        {
            var a = new BossHarness(TwoMovesInOneBand(), seed: 777u);
            var b = new BossHarness(TwoMovesInOneBand(), seed: 777u);

            a.Run(400);
            b.Run(400);

            Assert.That(a.ThrownMoves.Count, Is.GreaterThan(20), "the harness should be throwing moves");
            CollectionAssert.AreEqual(a.ThrownMoves, b.ThrownMoves);
        }

        [Test]
        public void DifferentSeedsProduceDifferentSequences()
        {
            var a = new BossHarness(TwoMovesInOneBand(), seed: 1u);
            var b = new BossHarness(TwoMovesInOneBand(), seed: 2u);

            a.Run(400);
            b.Run(400);

            CollectionAssert.AreNotEqual(a.ThrownMoves, b.ThrownMoves);
        }

        [Test]
        public void RepositioningConsumesNoRandomness()
        {
            // Draws must be a function of moves thrown, not of how long the player kites — else
            // the same seed would diverge based on player behaviour.
            var kited = new BossHarness(TwoMovesInOneBand(), seed: 99u) { Distance = 20f };
            kited.Run(200);
            Assert.That(kited.ThrownMoves, Is.Empty, "nothing should be legal at 20 m");

            kited.Distance = 2f;
            kited.Run(200);

            var direct = new BossHarness(TwoMovesInOneBand(), seed: 99u) { Distance = 2f };
            direct.Run(200);

            Assert.That(kited.ThrownMoves[0], Is.EqualTo(direct.ThrownMoves[0]));
        }

        // --- Legality gates ----------------------------------------------------------------

        [Test]
        public void AMoveOutsideItsBandIsNeverChosen()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "near", MinRange = 0f, MaxRange = 3f, MoveCooldownSeconds = 0f },
                    new FakeBossMove { Id = "far", MinRange = 6f, MaxRange = 12f, MoveCooldownSeconds = 0f },
                },
            };

            var harness = new BossHarness(definition, seed: 5u) { Distance = 8f };
            harness.Run(500);

            Assert.That(harness.ThrownMoves, Is.Not.Empty);
            CollectionAssert.DoesNotContain(harness.ThrownMoves, "near");
        }

        [Test]
        public void AMoveAboveTheCurrentPhaseIsNeverChosenUntilTheThresholdIsCrossed()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Phases = new IBossPhase[] { new FakeBossPhase { HealthFractionThreshold = 0.5f } },
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "opener", MaxRange = 5f, MoveCooldownSeconds = 0f },
                    new FakeBossMove { Id = "late", MaxRange = 5f, MoveCooldownSeconds = 0f, UnlockedAtPhase = 1 },
                },
            };

            var harness = new BossHarness(definition, seed: 11u);
            harness.Run(300);
            CollectionAssert.DoesNotContain(harness.ThrownMoves, "late");

            harness.HealthFraction = 0.4f;
            harness.Run(300);
            CollectionAssert.Contains(harness.ThrownMoves, "late");
        }

        [Test]
        public void AZeroWeightMoveIsNeverChosen()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "live", MaxRange = 5f, MoveCooldownSeconds = 0f },
                    new FakeBossMove { Id = "disabled", MaxRange = 5f, MoveCooldownSeconds = 0f, SelectionWeight = 0f },
                },
            };

            var harness = new BossHarness(definition, seed: 13u);
            harness.Run(400);

            Assert.That(harness.ThrownMoves, Is.Not.Empty);
            CollectionAssert.DoesNotContain(harness.ThrownMoves, "disabled");
        }

        [Test]
        public void APerMoveCooldownBlocksThatMoveButNotTheOthers()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                RepeatWeightMultiplier = 1f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "slow", MaxRange = 5f, MoveCooldownSeconds = 60f },
                    new FakeBossMove { Id = "fast", MaxRange = 5f, MoveCooldownSeconds = 0f },
                },
            };

            var harness = new BossHarness(definition, seed: 17u);
            harness.Run(400);

            int slowCount = harness.ThrownMoves.FindAll(id => id == "slow").Count;
            Assert.That(slowCount, Is.EqualTo(1), "a 60 s cooldown allows exactly one use in this window");
            Assert.That(harness.ThrownMoves.Count, Is.GreaterThan(10), "the other move must keep going");
        }

        [Test]
        public void EveryMoveOnCooldownWaitsInsteadOfCrashing()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[] { new FakeBossMove { Id = "only", MaxRange = 5f, MoveCooldownSeconds = 60f } },
            };

            var harness = new BossHarness(definition, seed: 19u);
            harness.Run(200);

            Assert.That(harness.ThrownMoves.Count, Is.EqualTo(1));
            Assert.That(harness.Brain.State, Is.EqualTo(BossState.Cooldown),
                "in band with nothing available is a wait, not a reposition");
            Assert.That(harness.Brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        [Test]
        public void AnEmptyMovesetNeverThrowsAndNeverAttacks()
        {
            var definition = new FakeBossDefinition { Moves = new IBossMove[0] };
            var harness = new BossHarness(definition, seed: 23u);

            Assert.DoesNotThrow(() => harness.Run(200));
            Assert.That(harness.ThrownMoves, Is.Empty);
            Assert.That(harness.Brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        [Test]
        public void TheRepeatPenaltyReducesRepeatsWithoutForbiddingThem()
        {
            var definition = TwoMovesInOneBand();
            definition.RepeatWeightMultiplier = 0.35f;

            var harness = new BossHarness(definition, seed: 29u);
            harness.Run(4000);

            List<string> thrown = harness.ThrownMoves;
            Assert.That(thrown.Count, Is.GreaterThan(200));

            int repeats = 0;
            for (int i = 1; i < thrown.Count; i++)
            {
                if (thrown[i] == thrown[i - 1])
                    repeats++;
            }

            float rate = repeats / (float)(thrown.Count - 1);
            // 0.35 against 1.0 gives ~0.26; uniform would be 0.5.
            Assert.That(rate, Is.GreaterThan(0.05f), "repeats must stay possible, never forbidden");
            Assert.That(rate, Is.LessThan(0.40f), "repeats must be visibly rarer than uniform");
        }

        [Test]
        public void TheRepeatPenaltyNeverDeadlocksWhenOnlyOneMoveIsLegal()
        {
            // Penalty of zero plus a single legal move would zero every weight; the brain must
            // repeat rather than stand still forever.
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                RepeatWeightMultiplier = 0f,
                Moves = new IBossMove[] { new FakeBossMove { Id = "only", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var harness = new BossHarness(definition, seed: 31u);
            harness.Run(300);

            Assert.That(harness.ThrownMoves.Count, Is.GreaterThan(10));
        }

        // --- Timing contracts --------------------------------------------------------------

        [Test]
        public void WantsToAttackIsASingleFrameEdge()
        {
            var harness = new BossHarness(TwoMovesInOneBand(), seed: 37u);

            harness.Step(0.05f);
            Assert.That(harness.Brain.WantsToAttack, Is.True, "it should commit on the first tick");

            harness.Brain.Tick(0.05f, 2f, true, true, 1f);
            Assert.That(harness.Brain.WantsToAttack, Is.False);
        }

        [Test]
        public void TheGlobalCooldownDoesNotTickDownWhileAttacking()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 1f,
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(41u));
            brain.Tick(0.05f, 2f, true, false, 1f);   // commits
            brain.NotifyLinkFinished();               // cooldown starts at 1 s

            float before = brain.CooldownRemaining;
            for (int i = 0; i < 10; i++)
                brain.Tick(0.1f, 2f, true, true, 1f); // a whole second, all of it mid-attack

            Assert.That(brain.CooldownRemaining, Is.EqualTo(before).Within(1e-4f),
                "a slow attack must not eat its own punish window");
        }

        [Test]
        public void ALaterPhaseShortensTheCooldown()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 1f,
                PhaseTransitionSeconds = 0f,
                Phases = new IBossPhase[]
                {
                    new FakeBossPhase { HealthFractionThreshold = 0.5f, CooldownMultiplier = 0.5f },
                },
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(43u));
            brain.Tick(0.05f, 2f, true, false, 1f);
            brain.NotifyLinkFinished();
            Assert.That(brain.CooldownRemaining, Is.EqualTo(1f).Within(1e-4f));

            brain.Tick(0.05f, 2f, true, false, 0.4f); // crosses the threshold
            Assert.That(brain.PhaseIndex, Is.EqualTo(1));

            brain.Tick(0.05f, 2f, true, false, 0.4f);
            brain.NotifyLinkFinished();
            Assert.That(brain.CooldownRemaining, Is.EqualTo(0.5f).Within(1e-4f));
        }

        // --- Phases ------------------------------------------------------------------------

        [Test]
        public void APhaseThresholdCrossedMidAttackDoesNotTransitionUntilTheAttackEnds()
        {
            // CLAUDE.md rule 6: a wind-up is never cancellable. A transition that fired mid-attack
            // would erase a swing the player has already committed to dodging.
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Phases = new IBossPhase[] { new FakeBossPhase { HealthFractionThreshold = 0.5f } },
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(47u));
            brain.Tick(0.05f, 2f, true, false, 1f);
            Assert.That(brain.WantsToAttack, Is.True);

            for (int i = 0; i < 20; i++)
            {
                brain.Tick(0.05f, 2f, true, true, 0.2f); // health well below the threshold
                Assert.That(brain.PhaseIndex, Is.EqualTo(0), $"tick {i}");
                Assert.That(brain.State, Is.EqualTo(BossState.Attacking), $"tick {i}");
            }

            brain.NotifyLinkFinished();
            brain.Tick(0.05f, 2f, true, false, 0.2f);

            Assert.That(brain.PhaseIndex, Is.EqualTo(1));
            Assert.That(brain.State, Is.EqualTo(BossState.PhaseTransition));
        }

        [Test]
        public void ThePhaseTransitionRootsTheBossForItsAuthoredLength()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                PhaseTransitionSeconds = 1.4f,
                Phases = new IBossPhase[] { new FakeBossPhase { HealthFractionThreshold = 0.5f } },
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(53u));
            brain.Tick(0.05f, 2f, true, false, 0.2f); // latches and transitions immediately
            Assert.That(brain.State, Is.EqualTo(BossState.PhaseTransition));

            for (float elapsed = 0f; elapsed < 1.2f; elapsed += 0.1f)
            {
                brain.Tick(0.1f, 2f, true, false, 0.2f);
                Assert.That(brain.State, Is.EqualTo(BossState.PhaseTransition), $"at {elapsed:0.00}s");
                Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f), "the boss is inert during a transition");
                Assert.That(brain.WantsToAttack, Is.False);
            }

            for (int i = 0; i < 5; i++)
                brain.Tick(0.1f, 2f, true, false, 0.2f);

            Assert.That(brain.State, Is.Not.EqualTo(BossState.PhaseTransition), "it must end");
        }

        [Test]
        public void ThePhaseTransitionClearsEveryMoveCooldown()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                PhaseTransitionSeconds = 0f,
                Phases = new IBossPhase[] { new FakeBossPhase { HealthFractionThreshold = 0.5f } },
                Moves = new IBossMove[] { new FakeBossMove { Id = "slow", MaxRange = 5f, MoveCooldownSeconds = 60f } },
            };

            var harness = new BossHarness(definition, seed: 59u);
            harness.Run(40);
            Assert.That(harness.ThrownMoves.Count, Is.EqualTo(1), "the 60 s cooldown should block a second use");

            harness.HealthFraction = 0.4f;
            harness.Run(60);

            Assert.That(harness.ThrownMoves.Count, Is.EqualTo(2),
                "a new phase starts clean so its moves are immediately available");
        }

        [Test]
        public void TwoThresholdsCrossedAtOnceAdvanceOnePhasePerTransition()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                PhaseTransitionSeconds = 0.5f,
                Phases = new IBossPhase[]
                {
                    new FakeBossPhase { HealthFractionThreshold = 0.7f },
                    new FakeBossPhase { HealthFractionThreshold = 0.4f },
                },
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(61u));

            brain.Tick(0.05f, 2f, true, false, 0.1f); // one huge hit crosses both thresholds
            Assert.That(brain.PhaseIndex, Is.EqualTo(1), "one transition at a time");

            for (int i = 0; i < 12; i++)
                brain.Tick(0.05f, 2f, true, false, 0.1f);

            Assert.That(brain.PhaseIndex, Is.EqualTo(2));
            Assert.That(brain.PhaseCount, Is.EqualTo(3), "two authored phases plus the implicit opener");
        }

        [Test]
        public void TheOpeningPhaseIsZeroAndIsCountedInPhaseCount()
        {
            var definition = new FakeBossDefinition
            {
                Phases = new IBossPhase[] { new FakeBossPhase() },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(67u));

            Assert.That(brain.PhaseIndex, Is.EqualTo(0));
            Assert.That(brain.PhaseCount, Is.EqualTo(2));
        }

        // --- Chains ------------------------------------------------------------------------

        [Test]
        public void AChainRearmsOnlyAfterItsLinkDelayHasElapsed()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove
                    {
                        Id = "sweep",
                        MaxRange = 5f,
                        MoveCooldownSeconds = 0f,
                        LinkDelaySeconds = 0.3f,
                        Links = FakeBossMove.Chain(2),
                    },
                },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(71u));

            brain.Tick(0.05f, 2f, true, false, 1f);
            Assert.That(brain.PendingAttack.Id, Is.EqualTo("link0"));

            brain.NotifyLinkFinished();

            // 0.25 s of a 0.30 s delay is not enough.
            for (int i = 0; i < 5; i++)
            {
                brain.Tick(0.05f, 2f, true, false, 1f);
                Assert.That(brain.WantsToAttack, Is.False, $"tick {i} fired early");
                Assert.That(brain.State, Is.EqualTo(BossState.Attacking),
                    "the gap inside a chain is not a punish window");
            }

            // The delay lands on a frame boundary, so allow the tick it falls in plus one more —
            // asserting an exact frame would only be testing float accumulation.
            bool rearmed = false;
            for (int i = 0; i < 2 && !rearmed; i++)
            {
                brain.Tick(0.05f, 2f, true, false, 1f);
                rearmed = brain.WantsToAttack;
            }

            Assert.That(rearmed, Is.True, "the second link must arrive once the delay elapses");
            Assert.That(brain.PendingAttack.Id, Is.EqualTo("link1"));
            Assert.That(brain.LinkIndex, Is.EqualTo(1));
        }

        [Test]
        public void AChainEndsAfterItsLastLinkAndStartsTheCooldown()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0.9f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove
                    {
                        Id = "sweep",
                        MaxRange = 5f,
                        MoveCooldownSeconds = 0f,
                        LinkDelaySeconds = 0f,
                        Links = FakeBossMove.Chain(2),
                    },
                },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(73u));

            brain.Tick(0.05f, 2f, true, false, 1f);
            brain.NotifyLinkFinished();
            Assert.That(brain.CooldownRemaining, Is.EqualTo(0f), "the chain is not over yet");

            brain.Tick(0.05f, 2f, true, false, 1f);
            Assert.That(brain.PendingAttack.Id, Is.EqualTo("link1"));

            brain.NotifyLinkFinished();
            Assert.That(brain.CooldownRemaining, Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(brain.CurrentMove, Is.Null);
        }

        [Test]
        public void AChainDoesNotRestartAcrossAPhaseTransition()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                PhaseTransitionSeconds = 1f,
                Phases = new IBossPhase[] { new FakeBossPhase { HealthFractionThreshold = 0.5f } },
                Moves = new IBossMove[]
                {
                    new FakeBossMove
                    {
                        Id = "sweep",
                        MaxRange = 5f,
                        MoveCooldownSeconds = 0f,
                        LinkDelaySeconds = 0.3f,
                        Links = FakeBossMove.Chain(2),
                    },
                },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(79u));

            brain.Tick(0.05f, 2f, true, false, 1f);   // link0 committed
            brain.NotifyLinkFinished();               // link1 now pending behind the delay

            brain.Tick(0.05f, 2f, true, false, 0.2f); // threshold crosses before the delay expires

            Assert.That(brain.PhaseIndex, Is.EqualTo(1));
            Assert.That(brain.State, Is.EqualTo(BossState.PhaseTransition));
            Assert.That(brain.CurrentMove, Is.Null, "the abandoned chain must not be resumed");
            Assert.That(brain.LinkIndex, Is.EqualTo(-1));
        }

        [Test]
        public void LosingTheTargetAbandonsAChain()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove
                    {
                        Id = "sweep",
                        MaxRange = 5f,
                        MoveCooldownSeconds = 0f,
                        LinkDelaySeconds = 0.3f,
                        Links = FakeBossMove.Chain(2),
                    },
                },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(83u));
            brain.Tick(0.05f, 2f, true, false, 1f);
            brain.NotifyLinkFinished();

            brain.Tick(0.05f, 2f, false, false, 1f);

            Assert.That(brain.State, Is.EqualTo(BossState.Idle));
            Assert.That(brain.CurrentMove, Is.Null);
        }

        // --- Repositioning -----------------------------------------------------------------

        [Test]
        public void TooFarFromEveryBandClosesOnTheTarget()
        {
            var definition = new FakeBossDefinition
            {
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MinRange = 5f, MaxRange = 9f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(89u));
            brain.Tick(0.05f, 20f, true, false, 1f);

            Assert.That(brain.State, Is.EqualTo(BossState.Reposition));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(1f));
        }

        [Test]
        public void TooCloseToEveryBandBacksAwayMoreSlowlyThanItCloses()
        {
            var definition = new FakeBossDefinition
            {
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MinRange = 5f, MaxRange = 9f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(97u));
            brain.Tick(0.05f, 1f, true, false, 1f);

            Assert.That(brain.State, Is.EqualTo(BossState.Reposition));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(-definition.Ranged.KiteSpeedFraction));
            Assert.That(System.Math.Abs(brain.MoveSpeedFraction), Is.LessThan(1f),
                "the player must always be able to corner it by committing");
        }

        [Test]
        public void ItClosesInsteadOfCampingAtTheEdgeOfItsLongestRange()
        {
            // Caught in a live play test: standing at 11 m the boss was technically inside its
            // 5-16 m volley band, so it held position and threw the same ranged attack over and
            // over. A player who simply kited would never see the melee half of the moveset.
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "cleave", MinRange = 0f, MaxRange = 3.2f, MoveCooldownSeconds = 1.5f },
                    new FakeBossMove { Id = "volley", MinRange = 5f, MaxRange = 16f, MoveCooldownSeconds = 2.5f },
                },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(1009u));

            // Throw the volley, then sit on its cooldown at range.
            brain.Tick(0.05f, 11f, true, false, 1f);
            Assert.That(brain.CurrentMove.Id, Is.EqualTo("volley"));
            brain.NotifyLinkFinished();

            brain.Tick(0.05f, 11f, true, false, 1f);

            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(1f),
                "with nothing available at range it must walk in, not camp");
            Assert.That(brain.State, Is.EqualTo(BossState.Reposition));
        }

        [Test]
        public void ItHoldsOnceInsideItsShortestBand()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "cleave", MinRange = 0f, MaxRange = 3.2f, MoveCooldownSeconds = 60f },
                    new FakeBossMove { Id = "volley", MinRange = 5f, MaxRange = 16f, MoveCooldownSeconds = 60f },
                },
            };

            var harness = new BossHarness(definition, seed: 1013u) { Distance = 2f };
            harness.Run(100);

            Assert.That(harness.Brain.MoveSpeedFraction, Is.EqualTo(0f),
                "already in melee reach; walking further would just shove the player");
        }

        [Test]
        public void RepositioningIgnoresBandsThisPhaseHasNotUnlocked()
        {
            var definition = new FakeBossDefinition
            {
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "opener", MinRange = 8f, MaxRange = 12f },
                    new FakeBossMove { Id = "late", MinRange = 0f, MaxRange = 2f, UnlockedAtPhase = 1 },
                },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(101u));
            brain.Tick(0.05f, 4f, true, false, 1f);

            // At 4 m the locked band [0,2] is only 2 m away while the opener's [8,12] is 4 m away.
            // Honouring the locked one would walk the boss onto the player to set up a move it
            // cannot use; ignoring it means backing off toward the band it can.
            Assert.That(brain.MoveSpeedFraction, Is.LessThan(0f),
                "it must retreat toward the unlocked band, not close on the locked one");
        }

        [Test]
        public void OutOfAggroRangeGoesIdleRatherThanRepositioning()
        {
            var definition = new FakeBossDefinition
            {
                AggroRange = 15f,
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MinRange = 0f, MaxRange = 3f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(103u));
            brain.Tick(0.05f, 40f, true, false, 1f);

            Assert.That(brain.State, Is.EqualTo(BossState.Idle));
            Assert.That(brain.MoveSpeedFraction, Is.EqualTo(0f));
        }

        // --- Greed punish ------------------------------------------------------------------

        static FakeBossDefinition WithRetaliation(int threshold = 3, float window = 2.5f)
        {
            return new FakeBossDefinition
            {
                AttackCooldownSeconds = 5f,     // long, so only a retaliation can bypass it
                RetaliationHitThreshold = threshold,
                RetaliationWindowSeconds = window,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "normal", MaxRange = 5f, MoveCooldownSeconds = 0f },
                    new FakeBossMove { Id = "nova", MaxRange = 5f, MoveCooldownSeconds = 30f, IsRetaliation = true },
                },
            };
        }

        [Test]
        public void EnoughHitsArmARetaliation()
        {
            var brain = new BossBrain(WithRetaliation(threshold: 3), new XorShiftRandom(211u));

            brain.NotifyDamaged();
            brain.NotifyDamaged();
            Assert.That(brain.RetaliationArmed, Is.False, "two hits is still a safe combo");

            brain.NotifyDamaged();
            Assert.That(brain.RetaliationArmed, Is.True, "the third hit is greed");
        }

        [Test]
        public void ARetaliationBypassesEveryCooldown()
        {
            var brain = new BossBrain(WithRetaliation(), new XorShiftRandom(223u));

            brain.Tick(0.05f, 2f, true, false, 1f);   // opens with something
            brain.NotifyLinkFinished();               // 5 s cooldown now running
            Assert.That(brain.CooldownRemaining, Is.GreaterThan(4f));

            brain.NotifyDamaged();
            brain.NotifyDamaged();
            brain.NotifyDamaged();

            brain.Tick(0.05f, 2f, true, false, 1f);

            Assert.That(brain.WantsToAttack, Is.True, "the answer must not wait out the cooldown");
            Assert.That(brain.CurrentMove.Id, Is.EqualTo("nova"));
            Assert.That(brain.RetaliationArmed, Is.False, "and it is spent once thrown");
        }

        [Test]
        public void ARetaliationStillKeepsItsFullWindup()
        {
            // Bypassing the cooldown is fair; bypassing the telegraph would just be an unfair hit,
            // and DESIGN.md forbids telegraph-free attacks outright.
            var definition = WithRetaliation();
            var brain = new BossBrain(definition, new XorShiftRandom(227u));

            brain.NotifyDamaged();
            brain.NotifyDamaged();
            brain.NotifyDamaged();
            brain.Tick(0.05f, 2f, true, false, 1f);

            Assert.That(brain.PendingAttack.WindupSeconds, Is.GreaterThan(0.4f));
        }

        [Test]
        public void ARetaliationNeverInterruptsAnAttackInProgress()
        {
            var brain = new BossBrain(WithRetaliation(), new XorShiftRandom(229u));

            brain.Tick(0.05f, 2f, true, false, 1f);
            brain.NotifyDamaged();
            brain.NotifyDamaged();
            brain.NotifyDamaged();

            for (int i = 0; i < 20; i++)
            {
                brain.Tick(0.05f, 2f, true, true, 1f);   // mid-attack the whole time
                Assert.That(brain.WantsToAttack, Is.False, $"tick {i}");
                Assert.That(brain.State, Is.EqualTo(BossState.Attacking));
            }

            Assert.That(brain.RetaliationArmed, Is.True, "the debt is still owed, just not yet paid");
        }

        [Test]
        public void TheHitTallyDecaysSoASlowFightNeverProvokesOne()
        {
            var brain = new BossBrain(WithRetaliation(threshold: 3, window: 1f), new XorShiftRandom(233u));

            brain.NotifyDamaged();
            brain.NotifyDamaged();

            for (int i = 0; i < 30; i++)
                brain.Tick(0.05f, 2f, true, false, 1f);   // 1.5 s of nothing

            brain.NotifyDamaged();

            Assert.That(brain.RetaliationArmed, Is.False, "chip damage spread out is not greed");
            Assert.That(brain.RecentHits, Is.EqualTo(1), "the tally restarted");
        }

        [Test]
        public void ARetaliationOutOfRangeStaysOwedRatherThanBeingForgiven()
        {
            var brain = new BossBrain(WithRetaliation(), new XorShiftRandom(239u));

            brain.NotifyDamaged();
            brain.NotifyDamaged();
            brain.NotifyDamaged();

            brain.Tick(0.05f, 40f, true, false, 1f);      // way outside the nova's band
            Assert.That(brain.RetaliationArmed, Is.True, "running away must not cancel the debt");

            brain.Tick(0.05f, 2f, true, false, 1f);
            Assert.That(brain.CurrentMove.Id, Is.EqualTo("nova"), "it is collected on return");
        }

        [Test]
        public void ARetaliationMoveNeverTurnsUpInTheOrdinaryRotation()
        {
            // The mechanic only teaches anything if seeing the move means "I got greedy".
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                RetaliationHitThreshold = 3,
                Moves = new IBossMove[]
                {
                    new FakeBossMove { Id = "normal", MaxRange = 5f, MoveCooldownSeconds = 0f },
                    new FakeBossMove { Id = "nova", MaxRange = 5f, MoveCooldownSeconds = 0f, IsRetaliation = true },
                },
            };

            var harness = new BossHarness(definition, seed: 241u);
            harness.Run(600);

            Assert.That(harness.ThrownMoves, Is.Not.Empty);
            CollectionAssert.DoesNotContain(harness.ThrownMoves, "nova");
        }

        [Test]
        public void AThresholdOfZeroDisablesTheMechanic()
        {
            var definition = WithRetaliation();
            definition.RetaliationHitThreshold = 0;
            var brain = new BossBrain(definition, new XorShiftRandom(251u));

            for (int i = 0; i < 20; i++)
                brain.NotifyDamaged();

            Assert.That(brain.RetaliationArmed, Is.False);
        }

        [Test]
        public void DeathClearsAnOwedRetaliation()
        {
            var brain = new BossBrain(WithRetaliation(), new XorShiftRandom(257u));

            brain.NotifyDamaged();
            brain.NotifyDamaged();
            brain.NotifyDamaged();
            Assert.That(brain.RetaliationArmed, Is.True);

            brain.NotifyDied();

            Assert.That(brain.RetaliationArmed, Is.False);
            brain.Tick(0.05f, 2f, true, false, 0f);
            Assert.That(brain.WantsToAttack, Is.False, "a corpse does not get the last word");
        }

        // --- Events and death --------------------------------------------------------------

        [Test]
        public void StateChangedFiresOnlyOnActualTransitions()
        {
            var definition = new FakeBossDefinition
            {
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MinRange = 5f, MaxRange = 9f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(107u));
            int changes = 0;
            brain.StateChanged += (from, to) =>
            {
                Assert.That(to, Is.Not.EqualTo(from));
                changes++;
            };

            for (int i = 0; i < 20; i++)
                brain.Tick(0.05f, 20f, true, false, 1f);

            Assert.That(changes, Is.EqualTo(1), "Idle to Reposition, then nothing");
        }

        [Test]
        public void PhaseChangedFiresOncePerPhase()
        {
            var definition = new FakeBossDefinition
            {
                AttackCooldownSeconds = 0f,
                PhaseTransitionSeconds = 0.2f,
                Phases = new IBossPhase[]
                {
                    new FakeBossPhase { HealthFractionThreshold = 0.7f },
                    new FakeBossPhase { HealthFractionThreshold = 0.4f },
                },
                Moves = new IBossMove[] { new FakeBossMove { Id = "a", MaxRange = 5f, MoveCooldownSeconds = 0f } },
            };

            var brain = new BossBrain(definition, new XorShiftRandom(109u));
            var seen = new List<int>();
            brain.PhaseChanged += seen.Add;

            for (int i = 0; i < 40; i++)
                brain.Tick(0.05f, 2f, true, false, 0.1f);

            CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
        }

        [Test]
        public void NotifyDiedStopsEverythingPermanently()
        {
            var harness = new BossHarness(TwoMovesInOneBand(), seed: 113u);
            harness.Run(20);
            Assert.That(harness.ThrownMoves, Is.Not.Empty);

            harness.Brain.NotifyDied();
            int thrownBefore = harness.ThrownMoves.Count;

            harness.Run(200);

            Assert.That(harness.Brain.State, Is.EqualTo(BossState.Dead));
            Assert.That(harness.Brain.WantsToAttack, Is.False);
            Assert.That(harness.Brain.MoveSpeedFraction, Is.EqualTo(0f));
            Assert.That(harness.ThrownMoves.Count, Is.EqualTo(thrownBefore));
        }

        [Test]
        public void NotifyLinkFinishedAfterDeathIsIgnored()
        {
            var harness = new BossHarness(TwoMovesInOneBand(), seed: 127u);
            harness.Run(10);

            harness.Brain.NotifyDied();
            Assert.DoesNotThrow(() => harness.Brain.NotifyLinkFinished());
            Assert.That(harness.Brain.State, Is.EqualTo(BossState.Dead));
        }

        [Test]
        public void MoveChosenReportsEveryCommittedMove()
        {
            var harness = new BossHarness(TwoMovesInOneBand(), seed: 131u);
            var announced = new List<string>();
            harness.Brain.MoveChosen += move => announced.Add(move.Id);

            harness.Run(300);

            CollectionAssert.AreEqual(harness.ThrownMoves, announced);
        }

        [Test]
        public void ANullDefinitionOrRandomSourceIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new BossBrain(null, new XorShiftRandom(1u)));
            Assert.Throws<System.ArgumentNullException>(
                () => new BossBrain(new FakeBossDefinition(), null));
        }
    }
}
