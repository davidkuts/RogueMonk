using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public class AttackStateMachineTests
    {
        const float Step = 1f / 60f;

        static AttackStateMachine Make(out FakeAttack attack)
        {
            attack = new FakeAttack { WindupSeconds = 0.10f, ActiveSeconds = 0.06f, RecoverySeconds = 0.18f };
            return new AttackStateMachine();
        }

        static void Run(AttackStateMachine machine, float seconds, float step = Step)
        {
            int steps = UnityEngine.Mathf.CeilToInt(seconds / step);
            for (int i = 0; i < steps; i++)
                machine.Tick(step);
        }

        [Test]
        public void StartsIdle()
        {
            var machine = new AttackStateMachine();
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Idle));
            Assert.That(machine.IsAttacking, Is.False);
            Assert.That(machine.Current, Is.Null);
        }

        [Test]
        public void TryStart_EntersWindup()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            Assert.That(machine.TryStart(attack), Is.True);
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Windup));
            Assert.That(machine.Current, Is.SameAs(attack));
        }

        [Test]
        public void TryStart_RejectsNull()
        {
            var machine = new AttackStateMachine();
            Assert.That(machine.TryStart(null), Is.False);
        }

        [Test]
        public void PhasesRunInOrderWithTheRightDurations()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            machine.TryStart(attack);

            machine.Tick(0.09f);
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Windup));

            machine.Tick(0.02f); // 0.11 -> active
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Active));

            machine.Tick(0.06f); // 0.17 -> recovery
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Recovery));

            machine.Tick(0.20f); // past 0.34 -> idle
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Idle));
            Assert.That(machine.Current, Is.Null);
        }

        [Test]
        public void WindupAndActiveAreCommitted_RecoveryIsNot()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            machine.TryStart(attack);
            Assert.That(machine.IsCommitted, Is.True, "windup");

            machine.Tick(0.11f);
            Assert.That(machine.IsCommitted, Is.True, "active");

            machine.Tick(0.06f);
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Recovery));
            Assert.That(machine.IsCommitted, Is.False);
            Assert.That(machine.IsCancellable, Is.True);
        }

        [Test]
        public void RecoveryIsNotCancellableWhenTheAttackForbidsIt()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            attack.CancellableOnRecovery = false;
            machine.TryStart(attack);
            machine.Tick(0.17f);

            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Recovery));
            Assert.That(machine.IsCancellable, Is.False);
        }

        [Test]
        public void TryStart_IsRefusedWhileCommitted()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            var other = new FakeAttack { Id = "other" };
            machine.TryStart(attack);

            Assert.That(machine.TryStart(other), Is.False, "windup is committed");
            machine.Tick(0.11f);
            Assert.That(machine.TryStart(other), Is.False, "active is committed");
            Assert.That(machine.Current, Is.SameAs(attack));
        }

        [Test]
        public void TryStart_IsAllowedDuringRecovery_ThatIsHowCombosChain()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            var followUp = new FakeAttack { Id = "followUp" };
            machine.TryStart(attack);
            machine.Tick(0.17f);

            Assert.That(machine.TryStart(followUp), Is.True);
            Assert.That(machine.Current, Is.SameAs(followUp));
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Windup));
            Assert.That(machine.Elapsed, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void ActiveEventsFireExactlyOnce()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            int started = 0, ended = 0;
            machine.ActiveStarted += _ => started++;
            machine.ActiveEnded += _ => ended++;

            machine.TryStart(attack);
            Run(machine, 0.5f);

            Assert.That(started, Is.EqualTo(1));
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void ALongFrameStillOpensAndClosesTheHitbox()
        {
            // At a bad frame rate a single tick can swallow the whole active window.
            // The hit must still register, or damage silently vanishes on slow machines.
            AttackStateMachine machine = Make(out FakeAttack attack);
            var order = new List<string>();
            machine.ActiveStarted += _ => order.Add("start");
            machine.ActiveEnded += _ => order.Add("end");
            machine.AttackEnded += _ => order.Add("done");

            machine.TryStart(attack);
            machine.Tick(5f);

            Assert.That(order, Is.EqualTo(new[] { "start", "end", "done" }));
        }

        [Test]
        public void ZeroLengthWindupReachesActiveOnTheStartingFrame()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            attack.WindupSeconds = 0f;
            bool activeStarted = false;
            machine.ActiveStarted += _ => activeStarted = true;

            machine.TryStart(attack);

            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Active));
            Assert.That(activeStarted, Is.True);
        }

        [Test]
        public void AttackEndedFiresOnNaturalCompletion()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            IAttackDefinition finished = null;
            machine.AttackEnded += a => finished = a;

            machine.TryStart(attack);
            Run(machine, 0.5f);

            Assert.That(finished, Is.SameAs(attack));
        }

        [Test]
        public void Cancel_EndsTheAttackWithoutRaisingAttackEnded()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            int ended = 0;
            machine.AttackEnded += _ => ended++;

            machine.TryStart(attack);
            machine.Tick(0.17f);
            machine.Cancel();

            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Idle));
            Assert.That(machine.Current, Is.Null);
            Assert.That(ended, Is.EqualTo(0));
        }

        [Test]
        public void Cancel_ClosesALiveHitbox()
        {
            // Otherwise a cancelled attack leaves the hitbox open and keeps hitting.
            AttackStateMachine machine = Make(out FakeAttack attack);
            int ended = 0;
            machine.ActiveEnded += _ => ended++;

            machine.TryStart(attack);
            machine.Tick(0.11f);
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Active));

            machine.Cancel();
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_WhileIdle_IsANoOp()
        {
            var machine = new AttackStateMachine();
            Assert.DoesNotThrow(() => machine.Cancel());
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Idle));
        }

        [Test]
        public void WindupProgress_RunsZeroToOne()
        {
            AttackStateMachine machine = Make(out FakeAttack attack);
            machine.TryStart(attack);
            Assert.That(machine.WindupProgress, Is.EqualTo(0f).Within(1e-4f));

            machine.Tick(0.05f);
            Assert.That(machine.WindupProgress, Is.EqualTo(0.5f).Within(0.01f));

            machine.Tick(0.06f); // into active
            Assert.That(machine.WindupProgress, Is.EqualTo(1f));
        }

        [Test]
        public void ElapsedTracksTheSumOfTicksWithoutDrift()
        {
            // Per-phase counters would accumulate error across hundreds of frames;
            // timing every boundary off total elapsed does not.
            AttackStateMachine machine = Make(out FakeAttack attack);
            attack.WindupSeconds = 1f;
            attack.ActiveSeconds = 1f;
            attack.RecoverySeconds = 1f;
            machine.TryStart(attack);

            for (int i = 0; i < 60; i++)
                machine.Tick(1f / 60f);

            // Accumulated float error after 60 additions must stay negligible. The phase at an
            // exact boundary is deliberately not asserted — it lands either side by an epsilon.
            Assert.That(machine.Elapsed, Is.EqualTo(1f).Within(1e-3f));

            machine.Tick(0.5f); // 1.5 s — unambiguously inside the active window
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Active));

            machine.Tick(1f); // 2.5 s — unambiguously inside recovery
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Recovery));

            machine.Tick(1f); // past 3 s
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Idle));
        }

        [Test]
        public void TickWhileIdle_IsANoOp()
        {
            var machine = new AttackStateMachine();
            Assert.DoesNotThrow(() => machine.Tick(1f));
            Assert.That(machine.Phase, Is.EqualTo(AttackPhase.Idle));
        }
    }
}
