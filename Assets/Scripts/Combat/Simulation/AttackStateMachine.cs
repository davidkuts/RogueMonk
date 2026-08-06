using System;
using UnityEngine;

namespace Game.Combat
{
    public enum AttackPhase
    {
        Idle = 0,
        Windup = 1,
        Active = 2,
        Recovery = 3,
    }

    /// <summary>
    /// Engine-free attack frame-timing. Drives windup → active → recovery off accumulated
    /// time rather than per-phase counters, so phase boundaries never drift.
    ///
    /// Wind-up and active are <em>committed</em> — nothing may interrupt them. Recovery is
    /// cancellable (DESIGN.md's skill-ceiling rule); the cost of cancelling is charged by
    /// whoever does the cancelling, not here.
    /// </summary>
    public sealed class AttackStateMachine
    {
        // 0 = windup, 1 = active, 2 = recovery, 3 = finished.
        int phaseIndex;
        float elapsed;

        public IAttackDefinition Current { get; private set; }

        public AttackPhase Phase { get; private set; } = AttackPhase.Idle;

        /// <summary>Seconds since the current attack began.</summary>
        public float Elapsed => elapsed;

        public bool IsAttacking => Phase != AttackPhase.Idle;

        /// <summary>Wind-up and active frames cannot be interrupted by anything.</summary>
        public bool IsCommitted => Phase == AttackPhase.Windup || Phase == AttackPhase.Active;

        /// <summary>True from the first frame of recovery, for attacks flagged cancellable.</summary>
        public bool IsCancellable => Phase == AttackPhase.Recovery && Current != null && Current.CancellableOnRecovery;

        /// <summary>Progress 0..1 through wind-up. Used to rotate facing onto an auto-aim target.</summary>
        public float WindupProgress
        {
            get
            {
                if (Current == null || Phase != AttackPhase.Windup)
                    return Phase == AttackPhase.Idle ? 0f : 1f;

                return Current.WindupSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / Current.WindupSeconds);
            }
        }

        public event Action<IAttackDefinition> AttackStarted;

        /// <summary>Hitbox goes live. Always raised, even if a long frame skips straight past the active window.</summary>
        public event Action<IAttackDefinition> ActiveStarted;

        /// <summary>Hitbox goes cold.</summary>
        public event Action<IAttackDefinition> ActiveEnded;

        /// <summary>Recovery completed naturally. Not raised by <see cref="Cancel"/>.</summary>
        public event Action<IAttackDefinition> AttackEnded;

        /// <summary>
        /// Begins <paramref name="definition"/>. Allowed from idle or from recovery — starting
        /// during recovery is exactly how a combo chains. Refused while committed.
        /// </summary>
        public bool TryStart(IAttackDefinition definition)
        {
            if (definition == null || IsCommitted)
                return false;

            Current = definition;
            elapsed = 0f;
            phaseIndex = 0;
            Phase = AttackPhase.Windup;
            AttackStarted?.Invoke(definition);

            // A zero-length wind-up should reach the active window on this same frame.
            Advance(PhaseIndexAt(0f));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!IsAttacking || deltaTime <= 0f)
                return;

            elapsed += deltaTime;
            Advance(PhaseIndexAt(elapsed));
        }

        /// <summary>
        /// Ends the attack immediately without raising <see cref="AttackEnded"/>. Closes a live
        /// hitbox first so a cancelled attack cannot keep hitting.
        /// </summary>
        public void Cancel()
        {
            if (!IsAttacking)
                return;

            IAttackDefinition cancelled = Current;
            bool hitboxWasLive = Phase == AttackPhase.Active;

            Phase = AttackPhase.Idle;
            Current = null;
            elapsed = 0f;
            phaseIndex = 3;

            if (hitboxWasLive)
                ActiveEnded?.Invoke(cancelled);
        }

        int PhaseIndexAt(float time)
        {
            IAttackDefinition definition = Current;
            if (definition == null)
                return 3;

            float windupEnd = Mathf.Max(0f, definition.WindupSeconds);
            float activeEnd = windupEnd + Mathf.Max(0f, definition.ActiveSeconds);
            float recoveryEnd = activeEnd + Mathf.Max(0f, definition.RecoverySeconds);

            if (time < windupEnd) return 0;
            if (time < activeEnd) return 1;
            if (time < recoveryEnd) return 2;
            return 3;
        }

        /// <summary>
        /// Walks every phase boundary between the current index and <paramref name="targetIndex"/>.
        /// Stepping rather than jumping is what guarantees a hitbox still opens and closes when a
        /// single long frame swallows the whole active window.
        /// </summary>
        void Advance(int targetIndex)
        {
            while (phaseIndex < targetIndex)
            {
                phaseIndex++;
                switch (phaseIndex)
                {
                    case 1:
                        Phase = AttackPhase.Active;
                        ActiveStarted?.Invoke(Current);
                        break;

                    case 2:
                        Phase = AttackPhase.Recovery;
                        ActiveEnded?.Invoke(Current);
                        break;

                    case 3:
                        IAttackDefinition finished = Current;
                        Phase = AttackPhase.Idle;
                        Current = null;
                        elapsed = 0f;
                        AttackEnded?.Invoke(finished);
                        break;
                }
            }
        }
    }
}
