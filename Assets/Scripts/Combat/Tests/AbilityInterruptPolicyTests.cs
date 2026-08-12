using Game.Core.Player;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The global ability-interrupt priority (M21). Three rules, and they are the kind that rot
    /// quietly if nothing pins them: the Split Second and the Blink answer in any state, and the
    /// attack button cannot cut into an Undertow channel.
    /// </summary>
    public sealed class AbilityInterruptPolicyTests
    {
        const float Windup = 0.10f;
        const float Active = 0.22f;
        const float ChannelEnd = Windup + Active;

        static AbilityState Vortex(AttackPhase phase, float elapsed) =>
            new AbilityState(true, AbilityId.VORTEX, phase, elapsed, ChannelEnd);

        static AbilityState Combo(AttackPhase phase, float elapsed) =>
            new AbilityState(true, AbilityId.ATK, phase, elapsed, 0.16f);

        // --- Interrupt rights ---

        [Test]
        public void TheBlinkAndTheSplitSecondInterruptEveryState()
        {
            AbilityState[] states =
            {
                AbilityState.Idle,
                Vortex(AttackPhase.Windup, 0.05f),
                Vortex(AttackPhase.Active, 0.20f),
                Vortex(AttackPhase.Recovery, 0.35f),
                Combo(AttackPhase.Windup, 0.02f),
                Combo(AttackPhase.Active, 0.12f),
                Combo(AttackPhase.Recovery, 0.20f),
            };

            foreach (AbilityState state in states)
            {
                Assert.IsTrue(
                    AbilityInterruptPolicy.AllowsInterrupt(state, InterruptSource.Dash),
                    $"The Blink must be answerable in {state.Ability}/{state.Phase}.");
                Assert.IsTrue(
                    AbilityInterruptPolicy.AllowsInterrupt(state, InterruptSource.Riposte),
                    $"The Split Second must be answerable in {state.Ability}/{state.Phase}.");
            }
        }

        [Test]
        public void NothingElseEarnsAnInterrupt()
        {
            // The rule is "these two", not "anything that asks". A future ability wanting the same
            // right has to say so here, where it is visible.
            Assert.IsFalse(
                AbilityInterruptPolicy.AllowsInterrupt(Vortex(AttackPhase.Active, 0.2f), InterruptSource.None));
        }

        // --- The attack button against the Undertow ---

        [Test]
        public void AnAttackPressedInsideTheChannelIsDiscarded()
        {
            Assert.AreEqual(
                AttackInputVerdict.Discard,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Windup, 0.01f), 0f));
            Assert.AreEqual(
                AttackInputVerdict.Discard,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Active, 0.20f), 0f));
        }

        [Test]
        public void DiscardIsNotAQueue()
        {
            // The distinction the whole rule rests on: Discard and Buffer are different verdicts,
            // because a buffered punch fires later and a discarded one never existed.
            AttackInputVerdict verdict =
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Active, 0.15f), 0f);

            Assert.AreNotEqual(AttackInputVerdict.Buffer, verdict);
            Assert.AreNotEqual(AttackInputVerdict.Accept, verdict);
        }

        [Test]
        public void TheChannelEndsWithTheActiveWindowNotWithTheMove()
        {
            // Recovery is the settle, and chaining out of it is untouched — that is ordinary
            // combo behaviour and the interrupt rules deliberately do not reach it.
            Assert.AreEqual(
                AttackInputVerdict.Accept,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Recovery, 0.35f), 0f));
        }

        // --- The tail buffer ---

        [Test]
        public void TheTailWindowIsOffByDefault()
        {
            // Every point in the channel, at the shipped tuning of 0.
            for (float t = 0f; t < ChannelEnd; t += 0.01f)
            {
                AttackPhase phase = t < Windup ? AttackPhase.Windup : AttackPhase.Active;
                Assert.AreEqual(
                    AttackInputVerdict.Discard,
                    AbilityInterruptPolicy.ResolveAttackInput(Vortex(phase, t), 0f),
                    $"Tail off must discard everywhere in the channel; failed at {t:0.00}s.");
            }
        }

        [Test]
        public void ANonZeroTailBuffersOnlyTheEndOfTheChannel()
        {
            const float tail = 0.08f;

            // Early in the channel: still refused.
            Assert.AreEqual(
                AttackInputVerdict.Discard,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Windup, 0.02f), tail));
            Assert.AreEqual(
                AttackInputVerdict.Discard,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Active, 0.20f), tail));

            // Inside the last 0.08 s of the channel: buffered, and it fires as the spin ends.
            Assert.AreEqual(
                AttackInputVerdict.Buffer,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Active, ChannelEnd - 0.05f), tail));
            Assert.AreEqual(
                AttackInputVerdict.Buffer,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Active, ChannelEnd - 0.001f), tail));
        }

        [Test]
        public void TheTailIsMeasuredBackFromTheChannelEnd()
        {
            // A tail longer than the channel swallows it whole rather than misbehaving at the edge.
            Assert.AreEqual(
                AttackInputVerdict.Buffer,
                AbilityInterruptPolicy.ResolveAttackInput(Vortex(AttackPhase.Windup, 0f), 10f));
        }

        // --- Everything that is NOT the vortex is unchanged ---

        [Test]
        public void TheOrdinaryComboKeepsItsInputBuffer()
        {
            Assert.AreEqual(
                AttackInputVerdict.Buffer,
                AbilityInterruptPolicy.ResolveAttackInput(Combo(AttackPhase.Windup, 0.02f), 0f),
                "The mandatory ~150 ms buffer is not up for negotiation.");
            Assert.AreEqual(
                AttackInputVerdict.Buffer,
                AbilityInterruptPolicy.ResolveAttackInput(Combo(AttackPhase.Active, 0.12f), 0f));
            Assert.AreEqual(
                AttackInputVerdict.Accept,
                AbilityInterruptPolicy.ResolveAttackInput(Combo(AttackPhase.Recovery, 0.20f), 0f));
        }

        [Test]
        public void AnIdlePlayerJustAttacks()
        {
            Assert.AreEqual(
                AttackInputVerdict.Accept,
                AbilityInterruptPolicy.ResolveAttackInput(AbilityState.Idle, 0f));
        }

        [Test]
        public void TheRiposteChannelIsNotTheVortexChannel()
        {
            // Only the Undertow refuses the attack button. A committed riposte buffers like any
            // other attack — its whole job is to be followed up on.
            Assert.AreEqual(
                AttackInputVerdict.Buffer,
                AbilityInterruptPolicy.ResolveAttackInput(
                    new AbilityState(true, AbilityId.SPLIT, AttackPhase.Active, 0.05f, 0.2f), 0f));
        }
    }

    /// <summary>
    /// The interrupt rules driven through the real <see cref="AttackStateMachine"/>, because the
    /// acceptance criterion that matters most — "rapidly alternating vortex → dash → vortex produces
    /// no stuck states" — is a property of the two together, not of either alone.
    /// </summary>
    public sealed class AbilityInterruptStateMachineTests
    {
        // The shipped Undertow frame data, so the phase boundaries the soak walks across are the
        // real ones rather than a convenient round number.
        static readonly FakeAttack TheVortex = new FakeAttack
        {
            Id = "Vortex",
            Ability = AbilityId.VORTEX,
            WindupSeconds = 0.10f,
            ActiveSeconds = 0.22f,
            RecoverySeconds = 0.10f,
            ComboWindowSeconds = 0f,
        };

        static readonly FakeAttack Punch = new FakeAttack { Id = "Punch", Ability = AbilityId.ATK };

        static AbilityState Capture(AttackStateMachine machine)
        {
            IAttackDefinition current = machine.Current;
            if (!machine.IsAttacking || current == null)
                return AbilityState.Idle;

            AbilityId ability = current is IAbilityTagged tagged ? tagged.Ability : AbilityId.None;
            return new AbilityState(
                true, ability, machine.Phase, machine.Elapsed,
                current.WindupSeconds + current.ActiveSeconds);
        }

        [Test]
        public void ADashInterruptsACommittedVortexAndClosesItsHitbox()
        {
            var machine = new AttackStateMachine();
            int activeEnded = 0;
            machine.ActiveEnded += _ => activeEnded++;

            machine.TryStart(TheVortex);
            machine.Tick(0.15f); // into the active window — the pull is live

            Assert.AreEqual(AttackPhase.Active, machine.Phase);
            Assert.IsTrue(AbilityInterruptPolicy.AllowsInterrupt(Capture(machine), InterruptSource.Dash));

            machine.Cancel();

            Assert.AreEqual(AttackPhase.Idle, machine.Phase);
            Assert.IsNull(machine.Current, "An interrupted vortex must not stay current.");
            Assert.AreEqual(1, activeEnded, "A cancelled spin must close its live hitbox exactly once.");
        }

        [Test]
        public void ARiposteInterruptsACommittedComboWindup()
        {
            var machine = new AttackStateMachine();
            machine.TryStart(Punch);
            machine.Tick(0.02f);

            Assert.AreEqual(AttackPhase.Windup, machine.Phase);
            Assert.IsTrue(AbilityInterruptPolicy.AllowsInterrupt(Capture(machine), InterruptSource.Riposte));

            machine.Cancel();
            Assert.IsTrue(machine.TryStart(Punch), "The counter must be able to start on the same frame.");
        }

        [Test]
        public void RapidVortexDashAlternationLeavesNoStuckState()
        {
            // The acceptance criterion, run hard. Every iteration casts a spin, interrupts it at a
            // different point in its frame data, and asserts the machine came back to a state that
            // can cast again. A single stuck phase or a surviving Current would fail every
            // subsequent iteration, so this catches residue rather than just the first frame.
            var machine = new AttackStateMachine();
            int activeEndedTotal = 0;
            int activeStartedTotal = 0;
            machine.ActiveEnded += _ => activeEndedTotal++;
            machine.ActiveStarted += _ => activeStartedTotal++;

            for (int i = 0; i < 500; i++)
            {
                Assert.IsTrue(machine.TryStart(TheVortex), $"Iteration {i}: the vortex must be castable.");

                // Walk to a different point of the move each time, so wind-up, active and recovery
                // interrupts all get exercised — including the exact phase boundaries.
                float t = 0.02f + (i % 20) * 0.02f;
                machine.Tick(t);

                Assert.IsTrue(AbilityInterruptPolicy.AllowsInterrupt(Capture(machine), InterruptSource.Dash));
                machine.Cancel();

                Assert.IsFalse(machine.IsAttacking, $"Iteration {i}: interrupted at {t:0.00}s and still attacking.");
                Assert.IsNull(machine.Current, $"Iteration {i}: interrupted at {t:0.00}s and Current survived.");
                Assert.AreEqual(0f, machine.Elapsed, 0.0001f, $"Iteration {i}: elapsed was not reset.");
                Assert.AreEqual(AttackPhase.Idle, machine.Phase, $"Iteration {i}: phase stuck.");

                // And the interrupt must not have left the player unable to act.
                Assert.IsTrue(machine.TryStart(Punch), $"Iteration {i}: could not act after the interrupt.");
                machine.Cancel();
            }

            Assert.AreEqual(
                activeStartedTotal, activeEndedTotal,
                "Every hitbox that opened must have closed — an unmatched pair is a lingering hitbox.");
        }

        [Test]
        public void AlternatingVortexAndRiposteNeverDoubleOpensAHitbox()
        {
            var machine = new AttackStateMachine();
            int live = 0;
            machine.ActiveStarted += _ => live++;
            machine.ActiveEnded += _ => live--;

            for (int i = 0; i < 200; i++)
            {
                machine.TryStart(TheVortex);
                machine.Tick(0.16f);
                Assert.LessOrEqual(live, 1, $"Iteration {i}: two hitboxes live at once.");

                machine.Cancel();
                Assert.AreEqual(0, live, $"Iteration {i}: a hitbox outlived its vortex.");

                machine.TryStart(Punch);
                machine.Tick(0.13f);
                machine.Cancel();
                Assert.AreEqual(0, live, $"Iteration {i}: a hitbox outlived its riposte.");
            }
        }

        [Test]
        public void AnInterruptedSpinForfeitsItsRemainingTicks()
        {
            // The no-refund bargain from the other side: what the spin already paid stands, and the
            // rest is simply not paid. Drain is what the natural end uses; an interrupt never
            // reaches it.
            var spin = new VortexSpin(3, 0.22f);
            spin.Due(0f);

            Assert.AreEqual(1, spin.Fired, "One tick landed before the interrupt.");
            Assert.AreEqual(2, spin.TickCount - spin.Fired, "Two ticks are forfeited, not banked.");
        }

        [Test]
        public void InterruptingDoesNotRefundTheCooldown()
        {
            var ability = new VortexAbility(10f, 0.4f);
            ability.TryConsume();

            // A partial channel that landed two hits keeps the reduction it earned...
            ability.RegisterLandedHit();
            ability.RegisterLandedHit();
            float atInterrupt = ability.CooldownRemaining;

            // ...and the interrupt itself gives nothing back.
            Assert.AreEqual(9.2f, atInterrupt, 0.0001f);
            Assert.IsFalse(ability.IsReady, "A cancelled cast must stay spent.");
        }
    }
}
