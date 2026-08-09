using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Engine-free decision layer for an enemy with more than one move.
    ///
    /// <para><c>MeleeEnemyBrain</c> knows one attack and asks only "am I in range yet". Every
    /// archetype in the Cretaceous roster has two or three moves with different reaches, so the
    /// question becomes <em>which</em>, and that is a legality gate followed by a seeded weighted
    /// draw — the same shape <c>BossBrain</c> settled on, minus phases and retaliation, which are
    /// genuinely a boss's.</para>
    ///
    /// <para>What this adds over the boss's version is the two things trash needs and a boss does
    /// not: a spawn grace, and an attack-token gate. A denied token is treated as an ordinary
    /// reason to hold — exactly like a cooldown — rather than as a refusal after the fact, so a
    /// move's cooldown is never spent on an attack that was not allowed to happen.</para>
    ///
    /// <para>Execution stays with the controller. This decides what to throw and when; how a
    /// charge locks its line or a roll ends in a wall is the adapter's business, because those are
    /// physics.</para>
    /// </summary>
    public sealed class EnemyMovesetBrain
    {
        readonly IEnemyDefinition definition;
        readonly IRandomSource random;
        readonly IReadOnlyList<IEnemyMove> moves;
        readonly float[] moveCooldowns;
        readonly float[] weights;
        readonly float repeatWeightMultiplier;

        float globalCooldownRemaining;
        float linkDelayRemaining;
        float graceRemaining;
        int lastMoveIndex = -1;
        int linkIndex = -1;
        bool dead;

        /// <param name="initialCooldownJitter">
        /// Upper bound on a random delay applied before this enemy's first attack, on top of the
        /// spawn grace.
        ///
        /// <para>This is what makes a pack read as a pack. ENEMIES_BIOME1.md § 2.1 wants Swiftjaws
        /// hunting in offset pairs — one attacking while the other circles — and without a jitter
        /// two raptors spawned in the same frame run identical timers forever: they commit
        /// together, recover together, and the player faces one doubled threat on a metronome
        /// instead of two alternating ones. The attack-token cap alone does not fix it; it only
        /// decides who goes first, not that they stay out of phase.</para>
        ///
        /// <para>Drawn once, at construction, from this enemy's own derived stream — a fixed point,
        /// so it stays deterministic.</para>
        /// </param>
        public EnemyMovesetBrain(
            IEnemyDefinition definition,
            IReadOnlyList<IEnemyMove> moves,
            IRandomSource random,
            float repeatWeightMultiplier = 0.45f,
            float initialCooldownJitter = 0f)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.moves = moves ?? Array.Empty<IEnemyMove>();
            this.repeatWeightMultiplier = repeatWeightMultiplier;

            moveCooldowns = new float[this.moves.Count];
            weights = new float[this.moves.Count];
            graceRemaining = Mathf.Max(0f, definition.SpawnGraceSeconds);

            if (initialCooldownJitter > 0f)
                globalCooldownRemaining = this.random.NextFloat(0f, initialCooldownJitter);
        }

        public EnemyState State { get; private set; } = EnemyState.Idle;

        /// <summary>Signed move intent: positive closes on the target, negative backs away.</summary>
        public float MoveSpeedFraction { get; private set; }

        /// <summary>True on the single frame the brain commits to a link.</summary>
        public bool WantsToAttack { get; private set; }

        /// <summary>The attack to start this frame. Only meaningful while <see cref="WantsToAttack"/>.</summary>
        public IAttackDefinition PendingAttack { get; private set; }

        /// <summary>The move being executed, for lunge distance and bespoke behaviour. Null between moves.</summary>
        public IEnemyMove CurrentMove { get; private set; }

        /// <summary>Zero-based index into <see cref="CurrentMove"/>'s links. -1 between moves.</summary>
        public int LinkIndex => linkIndex;

        /// <summary>True while this enemy is part-way through a scripted chain that must complete.</summary>
        public bool IsMidChain => CurrentMove != null && linkIndex >= 0 && linkIndex + 1 < CurrentMove.Links.Count;

        public float CooldownRemaining => globalCooldownRemaining;

        public float SpawnGraceRemaining => graceRemaining;

        public event Action<EnemyState, EnemyState> StateChanged;

        public event Action<IEnemyMove> MoveChosen;

        /// <param name="attackPermitted">
        /// Whether the attack-token pool would let this enemy start something. Passed in rather
        /// than queried, because the pool is scene state and this class is not allowed to know
        /// about scenes.
        /// </param>
        public void Tick(
            float deltaTime,
            float distanceToTarget,
            bool hasTarget,
            bool isAttacking,
            bool isStaggered,
            bool attackPermitted)
        {
            WantsToAttack = false;
            PendingAttack = null;

            if (dead)
            {
                MoveSpeedFraction = 0f;
                return;
            }

            // Timers stand still during an attack, so a long move never eats the punish window that
            // follows it. The rule MeleeEnemyBrain and BossBrain both already follow.
            if (deltaTime > 0f && !isAttacking)
            {
                globalCooldownRemaining = Mathf.Max(0f, globalCooldownRemaining - deltaTime);
                linkDelayRemaining = Mathf.Max(0f, linkDelayRemaining - deltaTime);

                for (int i = 0; i < moveCooldowns.Length; i++)
                    moveCooldowns[i] = Mathf.Max(0f, moveCooldowns[i] - deltaTime);
            }

            // The grace runs down in real time whatever the enemy is doing: it exists so a body
            // that materialised on top of the player cannot swing before it can be read, and that
            // clock should not pause because the enemy is walking.
            if (deltaTime > 0f && graceRemaining > 0f)
                graceRemaining = Mathf.Max(0f, graceRemaining - deltaTime);

            if (isStaggered)
            {
                // A stagger ends a chain. Resuming link two after being knocked down would deliver
                // the back half of a combo the player already interrupted.
                AbandonMove();
                MoveSpeedFraction = 0f;
                SetState(EnemyState.Staggered);
                return;
            }

            if (isAttacking)
            {
                MoveSpeedFraction = 0f;
                SetState(EnemyState.Attacking);
                return;
            }

            if (!hasTarget || distanceToTarget > definition.AggroRange)
            {
                AbandonMove();
                MoveSpeedFraction = 0f;
                SetState(EnemyState.Idle);
                return;
            }

            // Mid-chain: a scripted chain always completes, so this is not a choice and it does not
            // re-ask for a token — the enemy is already holding one.
            if (IsMidChain)
            {
                MoveSpeedFraction = 0f;
                SetState(EnemyState.Attacking);

                if (linkDelayRemaining <= 0f)
                {
                    linkIndex++;
                    PendingAttack = CurrentMove.Links[linkIndex];
                    WantsToAttack = true;
                }

                return;
            }

            if (globalCooldownRemaining > 0f || graceRemaining > 0f)
            {
                MoveSpeedFraction = RepositionFraction(distanceToTarget);
                SetState(EnemyState.Cooldown);
                return;
            }

            if (!attackPermitted)
            {
                // Someone else is swinging. Keep circling — a pack that freezes while it waits for
                // a token reads as broken, and the circling is what makes the flankers threatening.
                MoveSpeedFraction = RepositionFraction(distanceToTarget);
                SetState(EnemyState.Waiting);
                return;
            }

            int index = SelectMove(distanceToTarget);
            if (index < 0)
            {
                float fraction = RepositionFraction(distanceToTarget);
                MoveSpeedFraction = fraction;
                SetState(fraction > 0f ? EnemyState.Chase : fraction < 0f ? EnemyState.Reposition : EnemyState.Cooldown);
                return;
            }

            Commit(index);
        }

        void Commit(int index)
        {
            CurrentMove = moves[index];
            lastMoveIndex = index;
            linkIndex = 0;
            moveCooldowns[index] = Mathf.Max(0f, CurrentMove.MoveCooldownSeconds);
            PendingAttack = CurrentMove.Links[0];
            WantsToAttack = true;
            MoveSpeedFraction = 0f;

            SetState(EnemyState.Attacking);
            MoveChosen?.Invoke(CurrentMove);
        }

        /// <summary>Called by the controller when one link's attack finishes, recovery included.</summary>
        public void NotifyLinkFinished()
        {
            if (dead)
                return;

            if (IsMidChain)
            {
                linkDelayRemaining = Mathf.Max(0f, CurrentMove.LinkDelaySeconds);
                return;
            }

            AbandonMove();
            globalCooldownRemaining = Mathf.Max(0f, definition.AttackCooldownSeconds);
        }

        /// <summary>Called when a stagger interrupts an attack, so the enemy cannot instantly re-commit.</summary>
        public void NotifyInterrupted()
        {
            AbandonMove();
            globalCooldownRemaining = Mathf.Max(globalCooldownRemaining, Mathf.Max(0f, definition.AttackCooldownSeconds));
            WantsToAttack = false;
            PendingAttack = null;
        }

        public void NotifyDied()
        {
            dead = true;
            AbandonMove();
            WantsToAttack = false;
            PendingAttack = null;
            MoveSpeedFraction = 0f;
        }

        void AbandonMove()
        {
            CurrentMove = null;
            linkIndex = -1;
            linkDelayRemaining = 0f;
        }

        /// <summary>
        /// Returns the index of the move to throw, or -1 when nothing is legal from here.
        ///
        /// Draws from the RNG only when something is actually selectable — a frame with no legal
        /// move leaves <c>PickWeighted</c> short-circuiting on a zero total before it touches the
        /// stream. That keeps the draw count a function of moves thrown rather than of how long the
        /// player spent out of range, which is what stops player behaviour desynchronising a seed.
        /// </summary>
        int SelectMove(float distance)
        {
            if (moves.Count == 0)
                return -1;

            BuildWeights(distance, applyRepeatPenalty: true);
            int index = random.PickWeighted(weights);
            if (index >= 0)
                return index;

            // Everything legal was suppressed by the repeat penalty alone, which happens when the
            // last move is the only one in range. Repeating beats standing still, and the failed
            // pass consumed no draw, so this stays deterministic.
            BuildWeights(distance, applyRepeatPenalty: false);
            return random.PickWeighted(weights);
        }

        void BuildWeights(float distance, bool applyRepeatPenalty)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                IEnemyMove move = moves[i];

                bool legal = move.SelectionWeight > 0f
                    && move.Links != null
                    && move.Links.Count > 0
                    && moveCooldowns[i] <= 0f
                    && distance >= move.MinRange
                    && distance <= move.MaxRange;

                if (!legal)
                {
                    weights[i] = 0f;
                    continue;
                }

                weights[i] = applyRepeatPenalty && i == lastMoveIndex
                    ? move.SelectionWeight * Mathf.Max(0f, repeatWeightMultiplier)
                    : move.SelectionWeight;
            }
        }

        /// <summary>
        /// Signed intent toward the band of the shortest-reach move this enemy owns.
        ///
        /// <para>Closing to the <em>tightest</em> band rather than the nearest one is what keeps a
        /// mixed-range enemy pressing: parked at the outer edge of its longest reach it is
        /// technically "in a band", and would hold there throwing one attack forever.</para>
        ///
        /// <para>The negative case is Sailspit's whole personality — inside its minimum it backs
        /// away, which is what forces a committed dash instead of a walk-down. Backing off is
        /// slower than closing, so it can always be cornered.</para>
        /// </summary>
        float RepositionFraction(float distance)
        {
            IEnemyMove target = null;

            for (int i = 0; i < moves.Count; i++)
            {
                IEnemyMove move = moves[i];
                if (move.SelectionWeight <= 0f)
                    continue;

                if (target == null || move.MaxRange < target.MaxRange)
                    target = move;
            }

            if (target == null)
                return distance > definition.AttackRange ? 1f : 0f;

            if (distance > target.MaxRange)
                return 1f;

            if (distance < target.MinRange)
                return -Mathf.Clamp01(definition.Ranged.KiteSpeedFraction);

            return 0f;
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
