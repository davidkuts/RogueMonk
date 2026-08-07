using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Engine-free decision layer for the ranged enemy. It holds a preferred distance band:
    /// too far and it closes, too close and it backs off, in the band and it shoots. The
    /// hysteresis of a band rather than a single distance is what stops it oscillating on the
    /// spot, which reads as jitter.
    /// </summary>
    public sealed class RangedEnemyBrain
    {
        readonly IEnemyDefinition definition;

        float cooldownRemaining;
        float graceRemaining;

        public RangedEnemyBrain(IEnemyDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            graceRemaining = Mathf.Max(0f, definition.SpawnGraceSeconds);
        }

        /// <summary>Time left before this enemy may attack for the first time.</summary>
        public float SpawnGraceRemaining => graceRemaining;

        public EnemyState State { get; private set; } = EnemyState.Idle;

        /// <summary>Signed move intent: positive closes on the target, negative backs away.</summary>
        public float MoveSpeedFraction { get; private set; }

        public bool WantsToAttack { get; private set; }

        public float CooldownRemaining => cooldownRemaining;

        public event Action<EnemyState, EnemyState> StateChanged;

        public void Tick(float deltaTime, float distanceToTarget, bool hasTarget, bool isAttacking, bool isStaggered)
        {
            WantsToAttack = false;

            if (deltaTime > 0f && cooldownRemaining > 0f && !isAttacking)
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);

            if (deltaTime > 0f && graceRemaining > 0f)
                graceRemaining = Mathf.Max(0f, graceRemaining - deltaTime);

            RangedProfile profile = definition.Ranged;
            EnemyState next;
            float moveFraction = 0f;

            if (isStaggered)
            {
                next = EnemyState.Staggered;
            }
            else if (isAttacking)
            {
                // Rooted while shooting: the wind-up is the tell, and it must not slide away
                // from the player mid-telegraph.
                next = EnemyState.Attacking;
            }
            else if (!hasTarget || distanceToTarget > definition.AggroRange)
            {
                next = EnemyState.Idle;
            }
            else if (distanceToTarget > profile.PreferredMaxRange)
            {
                next = EnemyState.Chase;
                moveFraction = 1f;
            }
            else if (distanceToTarget < profile.PreferredMinRange)
            {
                // Backing away is deliberately slower than closing, so the player can always
                // corner a ranged enemy by committing to the chase.
                next = EnemyState.Chase;
                moveFraction = -Mathf.Max(0f, profile.KiteSpeedFraction);
            }
            else if (cooldownRemaining > 0f || graceRemaining > 0f)
            {
                // Recovering, or freshly spawned and not yet allowed to shoot.
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

        public void NotifyAttackFinished() =>
            cooldownRemaining = Mathf.Max(0f, definition.AttackCooldownSeconds);

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
