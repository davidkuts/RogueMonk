using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// What the boss is doing. Deliberately not <see cref="EnemyState"/>: a boss is Immune tier so
    /// it has no Staggered state, and it gains two of its own.
    /// </summary>
    public enum BossState
    {
        /// <summary>No target, or the target is outside aggro range.</summary>
        Idle = 0,

        /// <summary>Nothing is legal from here. Moving toward a distance where something is.</summary>
        Reposition = 1,

        /// <summary>Committed to a move. The controller owns the frame data.</summary>
        Attacking = 2,

        /// <summary>Enforced gap after a move — the punish window the player is owed.</summary>
        Cooldown = 3,

        /// <summary>Inert and vulnerable after a health threshold. The Immune tier's stagger.</summary>
        PhaseTransition = 4,

        Dead = 5,
    }

    /// <summary>
    /// Engine-free decision layer for the boss. It decides <em>which</em> move to throw and
    /// <em>when</em>; the frame data of each attack is still owned by the shared
    /// <c>AttackStateMachine</c>, so boss timings mean exactly what player timings mean.
    ///
    /// Selection is a deterministic legality gate followed by a seeded weighted draw. Pure random
    /// would pick a melee cleave at twelve metres and read-and-react would die; pure highest-score
    /// would be memorised in half a minute.
    ///
    /// Note there is no <c>isStaggered</c> input. <c>PoiseSystem</c> answers Immune before touching
    /// anything, so a boss can never be staggered, and taking the argument would imply it could.
    /// </summary>
    public sealed class BossBrain
    {
        readonly IBossDefinition definition;
        readonly IRandomSource random;
        readonly IReadOnlyList<IBossMove> moves;
        readonly float[] moveCooldowns;
        readonly float[] weights;

        float globalCooldownRemaining;
        float linkDelayRemaining;
        float phaseTransitionRemaining;
        bool phaseBreakPending;
        int lastMoveIndex = -1;
        int linkIndex = -1;
        bool dead;

        int recentHits;
        float retaliationWindowRemaining;
        bool retaliationArmed;

        public BossBrain(IBossDefinition definition, IRandomSource random)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.random = random ?? throw new ArgumentNullException(nameof(random));

            moves = definition.Moves ?? Array.Empty<IBossMove>();
            moveCooldowns = new float[moves.Count];
            weights = new float[moves.Count];

            RollNextThreshold();
        }

        public BossState State { get; private set; } = BossState.Idle;

        /// <summary>Zero-based. Phase 0 is the opening phase and is always present.</summary>
        public int PhaseIndex { get; private set; }

        public int PhaseCount => (definition.Phases?.Count ?? 0) + 1;

        /// <summary>Signed move intent: positive closes on the target, negative backs away.</summary>
        public float MoveSpeedFraction { get; private set; }

        /// <summary>True on the single frame the brain commits to a link. Same contract as the other brains.</summary>
        public bool WantsToAttack { get; private set; }

        /// <summary>The attack to start this frame. Only meaningful while <see cref="WantsToAttack"/>.</summary>
        public IAttackDefinition PendingAttack { get; private set; }

        /// <summary>The move being executed, for lunge distance and projectile count. Null between moves.</summary>
        public IBossMove CurrentMove { get; private set; }

        /// <summary>Zero-based index into <see cref="CurrentMove"/>'s links. -1 between moves.</summary>
        public int LinkIndex => linkIndex;

        public float CooldownRemaining => globalCooldownRemaining;

        public float PhaseTransitionRemaining => phaseTransitionRemaining;

        /// <summary>True once the boss has been hit enough to owe the player an answer.</summary>
        public bool RetaliationArmed => retaliationArmed;

        /// <summary>Hits counted so far toward the retaliation threshold.</summary>
        public int RecentHits => recentHits;

        /// <summary>How many hits the next counter costs. Re-drawn every time one is spent.</summary>
        public int NextThreshold { get; private set; }

        public event Action<BossState, BossState> StateChanged;

        /// <summary>Raised with the new phase index the frame a transition begins.</summary>
        public event Action<int> PhaseChanged;

        public event Action<IBossMove> MoveChosen;

        /// <summary>Raised when the hit tally arms a retaliation, for feedback and logging.</summary>
        public event Action RetaliationArmedChanged;

        public void Tick(float deltaTime, float distanceToTarget, bool hasTarget, bool isAttacking, float healthFraction)
        {
            WantsToAttack = false;
            PendingAttack = null;

            if (dead)
            {
                MoveSpeedFraction = 0f;
                SetState(BossState.Dead);
                return;
            }

            // Timers only run while the boss is not mid-attack. Otherwise a long attack would eat
            // its own punish window — the rule MeleeEnemyBrain already follows.
            if (deltaTime > 0f && !isAttacking)
            {
                globalCooldownRemaining = Mathf.Max(0f, globalCooldownRemaining - deltaTime);
                linkDelayRemaining = Mathf.Max(0f, linkDelayRemaining - deltaTime);
                phaseTransitionRemaining = Mathf.Max(0f, phaseTransitionRemaining - deltaTime);

                for (int i = 0; i < moveCooldowns.Length; i++)
                    moveCooldowns[i] = Mathf.Max(0f, moveCooldowns[i] - deltaTime);

                // The tally is a burst counter, not a running total: hits spread out over a long
                // fight should never accumulate into a retaliation the player did not provoke.
                if (retaliationWindowRemaining > 0f)
                {
                    retaliationWindowRemaining = Mathf.Max(0f, retaliationWindowRemaining - deltaTime);
                    if (retaliationWindowRemaining <= 0f)
                        recentHits = 0;
                }
            }

            // Latch the threshold, but do not act on it yet. Acting mid-attack would erase a swing
            // the player has already committed to dodging (CLAUDE.md rule 6).
            IReadOnlyList<IBossPhase> phases = definition.Phases;
            if (phases != null && PhaseIndex < phases.Count &&
                healthFraction <= phases[PhaseIndex].HealthFractionThreshold)
            {
                phaseBreakPending = true;
            }

            if (isAttacking)
            {
                MoveSpeedFraction = 0f;
                SetState(BossState.Attacking);
                return;
            }

            if (phaseTransitionRemaining > 0f)
            {
                MoveSpeedFraction = 0f;
                SetState(BossState.PhaseTransition);
                return;
            }

            if (phaseBreakPending)
            {
                BeginPhaseTransition();
                return;
            }

            if (!hasTarget || distanceToTarget > definition.AggroRange)
            {
                AbandonMove();
                MoveSpeedFraction = 0f;
                SetState(BossState.Idle);
                return;
            }

            // Mid-chain: a scripted chain always completes, so this is not a choice.
            if (CurrentMove != null && linkIndex + 1 < CurrentMove.Links.Count)
            {
                MoveSpeedFraction = 0f;
                SetState(BossState.Attacking);

                if (linkDelayRemaining <= 0f)
                {
                    linkIndex++;
                    PendingAttack = CurrentMove.Links[linkIndex];
                    WantsToAttack = true;
                }

                return;
            }

            // The answer to being combo'd, checked before the cooldown gate so it can arrive the
            // instant the previous attack ends. It is never checked before the isAttacking guard
            // above, so it still cannot cut a wind-up short (CLAUDE.md rule 6).
            if (retaliationArmed)
            {
                int counter = SelectRetaliation(distanceToTarget);
                if (counter >= 0)
                {
                    Commit(counter);
                    ClearRetaliation();
                    return;
                }

                // Out of range: stay armed, because backing off should delay the answer rather
                // than cancel it. Still recharging: drop the debt and attack normally — the player
                // got away with that one, and holding it would fire the counter long after the
                // greed that earned it, which reads as arbitrary.
                if (!AnyRetaliationCouldStillArrive(distanceToTarget))
                    ClearRetaliation();
            }

            if (globalCooldownRemaining > 0f)
            {
                MoveSpeedFraction = RepositionFraction(distanceToTarget);
                SetState(BossState.Cooldown);
                return;
            }

            int index = SelectMove(distanceToTarget);
            if (index < 0)
            {
                float fraction = RepositionFraction(distanceToTarget);
                MoveSpeedFraction = fraction;

                // Standing in a usable band with everything on cooldown is a wait, not a
                // reposition. Saying so keeps the state readable for animation and logging.
                SetState(fraction == 0f ? BossState.Cooldown : BossState.Reposition);
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

            SetState(BossState.Attacking);
            MoveChosen?.Invoke(CurrentMove);
        }

        /// <summary>
        /// Counts a hit toward the retaliation threshold. Called by the adapter whenever the boss
        /// takes damage.
        /// </summary>
        public void NotifyDamaged()
        {
            if (dead || definition.RetaliationHitThreshold <= 0 || retaliationArmed)
                return;

            recentHits++;
            retaliationWindowRemaining = Mathf.Max(0f, definition.RetaliationWindowSeconds);

            if (recentHits < NextThreshold)
                return;

            recentHits = 0;
            retaliationWindowRemaining = 0f;
            retaliationArmed = true;
            RollNextThreshold();
            RetaliationArmedChanged?.Invoke();
        }

        void ClearRetaliation()
        {
            retaliationArmed = false;
            RetaliationArmedChanged?.Invoke();
        }

        /// <summary>
        /// How many hits the next counter costs. Drawn fresh each time rather than fixed, so the
        /// player cannot simply count to three and stop — they have to read the boss instead of
        /// the arithmetic. Still bounded, so "lots of hits is dangerous" stays learnable.
        /// </summary>
        void RollNextThreshold()
        {
            int min = Mathf.Max(1, definition.RetaliationHitThreshold);
            int max = Mathf.Max(min, definition.RetaliationHitThresholdMax);

            NextThreshold = max > min ? random.NextInt(min, max + 1) : min;
        }

        /// <summary>Called by the controller when one link's attack finishes, recovery included.</summary>
        public void NotifyLinkFinished()
        {
            if (dead)
                return;

            if (CurrentMove != null && linkIndex + 1 < CurrentMove.Links.Count)
            {
                linkDelayRemaining = Mathf.Max(0f, CurrentMove.LinkDelaySeconds);
                return;
            }

            AbandonMove();
            globalCooldownRemaining = ScaledCooldown();
        }

        public void NotifyDied()
        {
            dead = true;
            AbandonMove();
            WantsToAttack = false;
            PendingAttack = null;
            MoveSpeedFraction = 0f;
            retaliationArmed = false;
            recentHits = 0;
            SetState(BossState.Dead);
        }

        void BeginPhaseTransition()
        {
            phaseBreakPending = false;
            PhaseIndex++;

            // A chain does not survive a phase change, and the new phase starts with a clean slate
            // so its freshly unlocked moves are immediately available.
            AbandonMove();
            globalCooldownRemaining = 0f;
            for (int i = 0; i < moveCooldowns.Length; i++)
                moveCooldowns[i] = 0f;

            phaseTransitionRemaining = Mathf.Max(0f, definition.PhaseTransitionSeconds);
            MoveSpeedFraction = 0f;

            SetState(BossState.PhaseTransition);
            PhaseChanged?.Invoke(PhaseIndex);
        }

        void AbandonMove()
        {
            CurrentMove = null;
            linkIndex = -1;
            linkDelayRemaining = 0f;
        }

        float ScaledCooldown()
        {
            float baseCooldown = Mathf.Max(0f, definition.AttackCooldownSeconds);
            IReadOnlyList<IBossPhase> phases = definition.Phases;

            // Phase 0 is the implicit opening phase, so Phases[0] describes phase 1.
            if (phases != null && PhaseIndex >= 1 && PhaseIndex - 1 < phases.Count)
                baseCooldown *= Mathf.Max(0f, phases[PhaseIndex - 1].CooldownMultiplier);

            return baseCooldown;
        }

        /// <summary>
        /// Returns the index of the move to throw, or -1 when nothing is legal from here.
        ///
        /// Draws from the RNG only when a move is actually selected: an unselectable frame leaves
        /// <c>PickWeighted</c> short-circuiting on a zero total before it touches the stream. That
        /// keeps the draw count a function of moves thrown rather than of how long the player kites.
        /// </summary>
        int SelectMove(float distance)
        {
            if (moves.Count == 0)
                return -1;

            BuildWeights(distance, applyRepeatPenalty: true);
            int index = random.PickWeighted(weights);
            if (index >= 0)
                return index;

            // Everything legal was suppressed by the repeat penalty alone — which happens when the
            // last move is the only one in range. Repeating beats standing still, so drop the
            // penalty and pick again. The failed pass consumed no draw, so this stays deterministic.
            BuildWeights(distance, applyRepeatPenalty: false);
            return random.PickWeighted(weights);
        }

        /// <summary>
        /// Picks a retaliation move. It bypasses the <em>global</em> cooldown — arriving straight
        /// after the previous attack is the whole point — but honours its <em>own</em> cooldown.
        ///
        /// Ignoring that too was a mistake: with a zero cooldown the counter fired on every third
        /// hit forever, so it stopped being a punish and became most of the fight. Its own recharge
        /// is what keeps it a threat rather than a rhythm.
        /// </summary>
        int SelectRetaliation(float distance)
        {
            if (moves.Count == 0)
                return -1;

            for (int i = 0; i < moves.Count; i++)
            {
                IBossMove move = moves[i];

                weights[i] = move.IsRetaliation
                    && move.SelectionWeight > 0f
                    && move.UnlockedAtPhase <= PhaseIndex
                    && moveCooldowns[i] <= 0f
                    && distance >= move.MinRange
                    && distance <= move.MaxRange
                    ? move.SelectionWeight
                    : 0f;
            }

            return random.PickWeighted(weights);
        }

        /// <summary>
        /// True when some retaliation could still become available from here — i.e. the debt is
        /// worth holding on to. False when every one of them is on cooldown, in which case the
        /// player simply got away with it.
        /// </summary>
        bool AnyRetaliationCouldStillArrive(float distance)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                IBossMove move = moves[i];
                if (!move.IsRetaliation || move.SelectionWeight <= 0f || move.UnlockedAtPhase > PhaseIndex)
                    continue;

                // On cooldown is a hard no; merely out of range is worth waiting for, since the
                // player will come back to keep attacking.
                if (moveCooldowns[i] <= 0f)
                    return true;
            }

            return false;
        }

        void BuildWeights(float distance, bool applyRepeatPenalty)
        {
            for (int i = 0; i < moves.Count; i++)
            {
                IBossMove move = moves[i];

                // Retaliations are counter-only. Letting one turn up in the ordinary rotation would
                // destroy the signal: the player should be able to learn that seeing it means
                // "I got greedy", not "it rolled a four".
                bool legal = !move.IsRetaliation
                    && move.SelectionWeight > 0f
                    && move.UnlockedAtPhase <= PhaseIndex
                    && moveCooldowns[i] <= 0f
                    && distance >= move.MinRange
                    && distance <= move.MaxRange;

                if (!legal)
                {
                    weights[i] = 0f;
                    continue;
                }

                weights[i] = applyRepeatPenalty && i == lastMoveIndex
                    ? move.SelectionWeight * Mathf.Max(0f, definition.RepeatWeightMultiplier)
                    : move.SelectionWeight;
            }
        }

        /// <summary>
        /// Steers toward the band of the <em>most demanding</em> move this phase has unlocked —
        /// the one with the shortest reach.
        ///
        /// Targeting the nearest band instead would let the boss park at the edge of its longest
        /// range and never close: standing at 11 m with a 16 m volley, it is technically "in a
        /// band", so it would hold there and throw the same ranged attack forever while the player
        /// kited it. Closing to the shortest band keeps every move in play and keeps the boss
        /// pressing. Backing off stays slower than closing, so the player can always corner it.
        /// </summary>
        float RepositionFraction(float distance)
        {
            IBossMove target = null;

            for (int i = 0; i < moves.Count; i++)
            {
                IBossMove move = moves[i];
                if (move.SelectionWeight <= 0f || move.UnlockedAtPhase > PhaseIndex)
                    continue;

                if (target == null || move.MaxRange < target.MaxRange)
                    target = move;
            }

            if (target == null)
                return 0f;

            if (distance > target.MaxRange)
                return 1f;

            if (distance < target.MinRange)
                return -Mathf.Max(0f, definition.Ranged.KiteSpeedFraction);

            return 0f; // inside the tightest band it has; holding here beats jittering
        }

        void SetState(BossState next)
        {
            if (next == State)
                return;

            BossState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
