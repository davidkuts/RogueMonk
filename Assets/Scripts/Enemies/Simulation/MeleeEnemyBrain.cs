using System;
using UnityEngine;

namespace Game.Enemies
{
    public enum EnemyState
    {
        /// <summary>No target, or the target is outside aggro range.</summary>
        Idle = 0,

        /// <summary>Closing on the target.</summary>
        Chase = 1,

        /// <summary>Committed to an attack. The controller owns the frame data.</summary>
        Attacking = 2,

        /// <summary>Enforced gap after an attack, so the player always gets a gap to punish.</summary>
        Cooldown = 3,

        /// <summary>Poise broken. Helpless.</summary>
        Staggered = 4,
    }

    /// <summary>
    /// Engine-free decision layer for the melee humanoid. It decides <em>whether</em> to
    /// chase and <em>when</em> to commit; the frame data of the attack itself is owned by the
    /// shared <c>AttackStateMachine</c>, so enemy and player attacks are timed by the same code.
    /// </summary>
    public sealed class MeleeEnemyBrain
    {
        readonly IEnemyDefinition definition;

        float cooldownRemaining;
        float graceRemaining;

        public MeleeEnemyBrain(IEnemyDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            graceRemaining = Mathf.Max(0f, definition.SpawnGraceSeconds);
        }

        /// <summary>Time left before this enemy may attack for the first time.</summary>
        public float SpawnGraceRemaining => graceRemaining;

        public EnemyState State { get; private set; } = EnemyState.Idle;

        /// <summary>Fraction of MoveSpeed to travel toward the target this frame, 0..1.</summary>
        public float MoveSpeedFraction { get; private set; }

        /// <summary>True on the single frame the brain decides to commit to an attack.</summary>
        public bool WantsToAttack { get; private set; }

        public float CooldownRemaining => cooldownRemaining;

        /// <summary>Raised when the state changes, for telegraph visuals and logging.</summary>
        public event Action<EnemyState, EnemyState> StateChanged;

        public void Tick(float deltaTime, float distanceToTarget, bool hasTarget, bool isAttacking, bool isStaggered)
        {
            WantsToAttack = false;

            if (deltaTime > 0f && cooldownRemaining > 0f && !isAttacking)
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);

            if (deltaTime > 0f && graceRemaining > 0f)
                graceRemaining = Mathf.Max(0f, graceRemaining - deltaTime);

            EnemyState next;
            float moveFraction = 0f;

            if (isStaggered)
            {
                next = EnemyState.Staggered;
            }
            else if (isAttacking)
            {
                next = EnemyState.Attacking;
            }
            else if (!hasTarget || distanceToTarget > definition.AggroRange)
            {
                next = EnemyState.Idle;
            }
            else if (distanceToTarget > definition.AttackRange)
            {
                next = EnemyState.Chase;
                moveFraction = 1f;
            }
            else if (cooldownRemaining > 0f || graceRemaining > 0f)
            {
                // In range but not allowed to swing yet — either recovering from the last attack,
                // or freshly spawned. Hold position rather than jitter in and out.
                next = EnemyState.Cooldown;
            }
            else
            {
                next = EnemyState.Attacking;
                WantsToAttack = true;
            }

            MoveSpeedFraction = moveFraction;
            SetState(next);
        }

        /// <summary>Called by the controller when an attack finishes, starting the enforced gap.</summary>
        public void NotifyAttackFinished()
        {
            cooldownRemaining = Mathf.Max(0f, definition.AttackCooldownSeconds);
        }

        /// <summary>Called when a stagger interrupts an attack, so the enemy cannot instantly re-commit.</summary>
        public void NotifyInterrupted()
        {
            cooldownRemaining = Mathf.Max(cooldownRemaining, Mathf.Max(0f, definition.AttackCooldownSeconds));
            WantsToAttack = false;
        }

        void SetState(EnemyState next)
        {
            if (next == State)
                return;

            EnemyState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
