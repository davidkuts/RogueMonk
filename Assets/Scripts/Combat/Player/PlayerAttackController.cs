using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Input;
using Game.Core.Player;
using Game.Core.Timing;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// MonoBehaviour adapter for the player's melee combo. Owns the frame-timing state
    /// machine, the combo cursor, the hit resolver and hitstop, and translates the
    /// simulation's hitbox description into physics queries. Runs before
    /// <see cref="PlayerMotor"/> so the motor sees this frame's action state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerAttackController : MonoBehaviour, IPlayerActionState
    {
        [SerializeField] ComboDefinition combo;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;

        [Header("Targeting")]
        [SerializeField, Tooltip("Layers an attack can hit.")]
        LayerMask hittableLayers = ~0;
        [SerializeField, Tooltip("Origin height offset for hitbox queries, so ground-level boxes still overlap capsules.")]
        float hitboxHeightOffset = 0.9f;
        [SerializeField, Tooltip("How far the stick must be pushed to take aim away from auto-aim during a wind-up. Low, because a deliberate push should always win.")]
        float aimSteerDeadzone = 0.25f;

        [Header("Feedback")]
        [SerializeField, Tooltip("Optional. Fires a Cinemachine impulse on every connecting hit.")]
        CinemachineImpulseSource impulseSource;
        [SerializeField, Tooltip("Impulse strength scaled by the attack's hitstop, so heavier hits shake harder.")]
        float impulseScale = 1f;

        readonly AttackStateMachine attacks = new AttackStateMachine();
        readonly HitResolver resolver = new HitResolver();
        readonly InputBuffer attackBuffer = new InputBuffer();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly List<Vector3> candidatePositions = new List<Vector3>();
        readonly List<IDamageable> candidateTargets = new List<IDamageable>();
        readonly List<Transform> candidateTransforms = new List<Transform>();

        readonly Collider[] overlapResults = new Collider[16];

        ComboTracker comboTracker;
        Transform aimTarget;

        /// <summary>How a single step of the current chain turned out — what the combo meter draws.</summary>
        public enum ComboStepState
        {
            /// <summary>Not thrown yet in this chain.</summary>
            Pending = 0,

            /// <summary>Thrown and missed. Advances the chain but earns nothing.</summary>
            Whiffed = 1,

            /// <summary>Thrown and connected.</summary>
            Connected = 2,
        }

        ComboStepState[] stepStates = new ComboStepState[0];
        int activeStepIndex = -1;

        /// <summary>The hit pipeline. Boons register modifiers here later.</summary>
        public HitResolver Resolver => resolver;

        /// <summary>
        /// Hands the active window's hit detection to someone else for the current attack.
        ///
        /// <para>The shared pass deliberately strikes each target at most once per attack, which is
        /// exactly wrong for the Undertow: its whole payload is several ticks on the same enemy
        /// across one active window. Rather than teach the shared pass a second mode, the vortex
        /// owns its own querying and switches this on while it runs. Hits still go through the same
        /// <see cref="Resolver"/>, so boons and feedback are unaffected.</para>
        /// </summary>
        public bool SuppressDefaultHitbox { get; set; }

        public AttackStateMachine Attacks => attacks;

        /// <summary>
        /// The shared freeze timer. Owned by <see cref="GameClock"/>, not by combat — the
        /// pause menu has to be able to outrank it.
        /// </summary>
        public HitstopController Hitstop => GameClock.Instance != null ? GameClock.Instance.Freeze : null;

        public ComboTracker Combo => comboTracker;

        /// <summary>Per-step outcome of the chain in progress. Index 0 is the first attack.</summary>
        public IReadOnlyList<ComboStepState> ComboSteps => stepStates;

        /// <summary>Fires when the last attack of the chain connects — a fully landed combo.</summary>
        public event Action<int> ComboLanded;

        /// <summary>Fires when the chain lapses. Argument is how many steps had connected.</summary>
        public event Action<int> ComboDropped;

        /// <summary>Fires for every hit that survives the pipeline.</summary>
        public event Action<HitContext> Hit;

        /// <summary>Fires when an attack's active window closed without touching anything.</summary>
        public event Action<IAttackDefinition> Whiffed;

        /// <summary>Fires when a one-off attack outside the combo chain is thrown.</summary>
        public event Action<IAttackDefinition> SpecialStarted;

        /// <summary>
        /// Throws a one-off attack that is not part of the combo chain — the riposte.
        ///
        /// It goes through the same state machine, hitbox query and resolver as everything else, so
        /// it is an ordinary attack that happens to be spent from a different resource. It also
        /// leaves the combo cursor alone: landing a counter should not silently reset the chain the
        /// player was building.
        /// </summary>
        public bool TryStartSpecial(IAttackDefinition definition)
        {
            if (definition == null || attacks.IsCommitted)
                return false;

            if (motor != null && motor.Dash != null && motor.Dash.IsDashing)
                return false;

            if (TryGetStickDirection(out Vector3 aimHint))
                motor.Locomotion.SetFacing(aimHint);

            if (!attacks.TryStart(definition))
                return false;

            attackBuffer.Clear();
            activeStepIndex = -1;
            alreadyHit.Clear();
            aimTarget = AcquireAimTarget(definition);

            GameLog.Info(LogCategory.Combat,
                $"SPECIAL       {definition.Id}  frames {definition.WindupSeconds:F3}/" +
                $"{definition.ActiveSeconds:F3}/{definition.RecoverySeconds:F3}  damage {definition.Damage:0.#}");

            SpecialStarted?.Invoke(definition);
            return true;
        }

        // --- IPlayerActionState ---

        public float MoveSpeedMultiplier =>
            attacks.IsAttacking && attacks.Current != null ? attacks.Current.MoveSpeedMultiplier : 1f;

        /// <summary>Wind-up and active frames are committed; recovery is dash-cancellable.</summary>
        public bool AllowsDash => !attacks.IsAttacking || attacks.IsCancellable;

        /// <summary>
        /// The attack owns facing while committed, so auto-aim survives the player strafing.
        /// Turning frees up again in recovery, for repositioning between combo hits.
        /// </summary>
        public bool AllowsTurning => !attacks.IsCommitted;

        public void CancelForDash()
        {
            if (!attacks.IsAttacking)
                return;

            GameLog.Debug(LogCategory.Combat,
                $"dash-cancel   {attacks.Current.Id} out of {attacks.Phase} (costs a dash charge)");
            attacks.Cancel();
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
            if (input == null)
                input = GetComponent<PlayerInputReader>();

            IReadOnlyList<IAttackDefinition> sequence = combo != null ? combo.BuildSequence() : null;
            if (sequence == null || sequence.Count == 0)
            {
                Debug.LogError($"{nameof(PlayerAttackController)} on '{name}' has no combo attacks assigned.", this);
                enabled = false;
                return;
            }

            comboTracker = new ComboTracker(sequence);
            stepStates = new ComboStepState[sequence.Count];
            attacks.ActiveStarted += OnActiveStarted;
            attacks.ActiveEnded += OnActiveEnded;
            resolver.HitApplied += OnHitApplied;
            comboTracker.ChainDropped += OnChainDropped;
        }

        void OnDestroy()
        {
            attacks.ActiveStarted -= OnActiveStarted;
            attacks.ActiveEnded -= OnActiveEnded;
            resolver.HitApplied -= OnHitApplied;
            if (comboTracker != null)
                comboTracker.ChainDropped -= OnChainDropped;
        }

        int ConnectedStepCount()
        {
            int count = 0;
            for (int i = 0; i < stepStates.Length; i++)
            {
                if (stepStates[i] == ComboStepState.Connected)
                    count++;
            }

            return count;
        }

        void ClearStepStates()
        {
            for (int i = 0; i < stepStates.Length; i++)
                stepStates[i] = ComboStepState.Pending;
        }

        void OnChainDropped()
        {
            int connected = ConnectedStepCount();
            ClearStepStates();
            activeStepIndex = -1;
            ComboDropped?.Invoke(connected);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (input != null && input.AttackPressedThisFrame)
                attackBuffer.Press();
            attackBuffer.Tick(deltaTime, combo.InputBufferSeconds);

            TryConsumeBufferedAttack();

            attacks.Tick(deltaTime);
            comboTracker.Tick(deltaTime, attacks.IsAttacking);

            SteerAimDuringWindup(deltaTime);

            if (attacks.Phase == AttackPhase.Active && !SuppressDefaultHitbox)
                QueryHitbox();
        }

        void TryConsumeBufferedAttack()
        {
            if (!attackBuffer.HasInput || attacks.IsCommitted)
                return;

            // Dashing owns the character outright; an attack press waits in the buffer.
            if (motor != null && motor.Dash != null && motor.Dash.IsDashing)
                return;

            IAttackDefinition next = comboTracker.Next;
            int stepIndex = comboTracker.Index;
            if (!attacks.TryStart(next))
                return;

            // Point the punch where the player is pointing, before anything else looks at facing.
            //
            // Steering across the wind-up alone is not enough to fix this: Punch1 winds up in 100 ms,
            // which at 540 deg/s buys 54 degrees of correction — nowhere near a reversal. Someone
            // who dashed past an enemy and is now holding the stick back toward it has already said
            // where they want to hit, so the attack should simply start there. The dash sets facing
            // instantly from the stick for exactly the same reason.
            if (TryGetStickDirection(out Vector3 aimHint))
                motor.Locomotion.SetFacing(aimHint);

            // Starting the first step of a fresh chain wipes the previous chain's outcome.
            if (stepIndex == 0)
                ClearStepStates();

            attackBuffer.Clear();
            comboTracker.Consume();
            activeStepIndex = stepIndex;
            if (stepIndex < stepStates.Length)
                stepStates[stepIndex] = ComboStepState.Whiffed; // upgraded to Connected if it lands

            alreadyHit.Clear();
            aimTarget = AcquireAimTarget(next);

            GameLog.Debug(LogCategory.Combat,
                $"attack start  {next.Id}  step {stepIndex + 1}/{stepStates.Length}  " +
                $"frames {next.WindupSeconds:F3}/{next.ActiveSeconds:F3}/{next.RecoverySeconds:F3}");
        }

        /// <summary>
        /// The direction the player is actively pointing, or false when the stick is near neutral.
        /// Screen axes are world axes on a fixed top-down camera, so no conversion is needed.
        /// </summary>
        bool TryGetStickDirection(out Vector3 direction) =>
            AimAssist.TryResolveAimDirection(
                input != null ? input.MoveAxis : Vector2.zero,
                aimSteerDeadzone,
                Vector3.zero,
                hasTarget: false,
                out direction);

        /// <summary>Locks a target at attack start; facing then rotates onto it across the wind-up.</summary>
        Transform AcquireAimTarget(IAttackDefinition definition)
        {
            CollectCandidates(definition.AutoAimRangeMeters);
            if (candidatePositions.Count == 0)
                return null;

            int index;
            bool found = AimAssist.TrySelectTarget(
                transform.position,
                motor.Locomotion.Facing,
                candidatePositions,
                definition.AutoAimConeDegrees,
                definition.AutoAimRangeMeters,
                out index);

            return found ? candidateTransforms[index] : null;
        }

        void CollectCandidates(float radius)
        {
            candidatePositions.Clear();
            candidateTargets.Clear();
            candidateTransforms.Clear();

            Vector3 origin = transform.position + Vector3.up * hitboxHeightOffset;
            int count = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(0f, radius), overlapResults, hittableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapResults[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                    continue;

                candidateTargets.Add(damageable);
                candidateTransforms.Add(hit.transform);
                candidatePositions.Add(hit.transform.position);
            }
        }

        /// <summary>
        /// Aims the attack across its wind-up.
        ///
        /// The stick outranks auto-aim. Auto-aim is an <em>assist</em>: it should snap onto a target
        /// the player has not bothered to point at, never override one they have. Previously this
        /// returned immediately when no target was acquired at attack start — and because attacks
        /// also switch off <c>AllowsTurning</c>, that left facing frozen for the whole attack. Dash
        /// past an enemy and every following punch went into empty air with no way to correct it,
        /// while the stick was read for movement and ignored for aim.
        ///
        /// Turning is not cancelling, so the wind-up stays as committed as CLAUDE.md rule 6 requires:
        /// the attack still lands, on its own frame data. Only its direction is negotiable.
        /// </summary>
        void SteerAimDuringWindup(float deltaTime)
        {
            if (attacks.Phase != AttackPhase.Windup || attacks.Current == null)
                return;

            Vector3 toTarget = aimTarget != null ? aimTarget.position - transform.position : Vector3.zero;

            if (!AimAssist.TryResolveAimDirection(
                    input != null ? input.MoveAxis : Vector2.zero,
                    aimSteerDeadzone,
                    toTarget,
                    aimTarget != null,
                    out Vector3 desired))
                return;

            Vector3 steered = AimAssist.RotateFacing(
                motor.Locomotion.Facing, desired, attacks.Current.AimSnapSpeedDegPerSec, deltaTime);

            motor.Locomotion.SetFacing(steered);
        }

        void QueryHitbox()
        {
            IAttackDefinition definition = attacks.Current;
            if (definition == null)
                return;

            HitboxShape shape = definition.Hitbox;
            Vector3 facing = motor.Locomotion.Facing;
            Vector3 origin = transform.position + Vector3.up * hitboxHeightOffset;
            Vector3 center = shape.WorldCenter(origin, facing);

            int count = HitboxQuery.Overlap(shape, origin, facing, hittableLayers, overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                if (!HitboxQuery.Contains(shape, origin, facing, collider.transform.position))
                    continue;

                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || alreadyHit.Contains(target))
                    continue;

                // A zoned body may only be struck on a plate or a soft spot. Its root capsule is
                // skipped, or the hit would resolve unzoned at full damage and the armour would
                // apply only when the query happened to return the right collider first.
                if (HitZones.IsNonZoneColliderOfZonedBody(collider))
                    continue;

                alreadyHit.Add(target);

                Vector3 point = collider.ClosestPoint(center);
                Vector3 direction = collider.transform.position - transform.position;
                // The collider is what says whether this landed on amber or on the seam beside it.
                // Resolved here because this is the last moment anything knows it.
                HitContext context = HitContext.FromAttack(
                    definition, target, direction, point, HitZones.Resolve(collider));
                resolver.Resolve(ref context);
            }
        }

        void OnActiveStarted(IAttackDefinition definition) => alreadyHit.Clear();

        void OnActiveEnded(IAttackDefinition definition)
        {
            // An attack whose hits are owned elsewhere leaves this set empty by design, so reading
            // it as a whiff would report every vortex as a miss and play the whiff sound over it.
            if (alreadyHit.Count == 0 && !SuppressDefaultHitbox)
            {
                GameLog.Debug(LogCategory.Combat, $"whiff         {definition.Id}  (active window closed, nothing in range)");
                Whiffed?.Invoke(definition);
            }

            alreadyHit.Clear();
        }

        void OnHitApplied(HitContext context)
        {
            if (GameClock.Instance != null)
                GameClock.Instance.RequestFreeze(context.HitstopSeconds);

            if (impulseSource != null && context.HitstopSeconds > 0f)
                impulseSource.GenerateImpulse(context.Direction * (context.HitstopSeconds * impulseScale));

            if (activeStepIndex >= 0 && activeStepIndex < stepStates.Length)
                stepStates[activeStepIndex] = ComboStepState.Connected;

            // Base values come from the asset; the context values are post-pipeline. Logging both
            // is what makes a misbehaving hit modifier visible instead of mysterious.
            IAttackDefinition attack = context.Attack;
            string damageNote = Mathf.Approximately(attack.Damage, context.Damage)
                ? $"{context.Damage:0.##}"
                : $"{context.Damage:0.##} (base {attack.Damage:0.##})";

            GameLog.Info(LogCategory.Combat,
                $"HIT           {attack.Id}  step {activeStepIndex + 1}/{stepStates.Length}  " +
                $"dmg {damageNote} {context.DamageType}  poise {context.PoiseDamage:0.##}  " +
                $"knock {context.Knockback:0.##}  hitstop {context.HitstopSeconds:F3}s");

            Hit?.Invoke(context);

            bool chainComplete = activeStepIndex == stepStates.Length - 1;
            if (chainComplete)
            {
                int connected = ConnectedStepCount();
                GameLog.Info(LogCategory.Combat,
                    $"COMBO LANDED  {connected}/{stepStates.Length} steps connected");
                ComboLanded?.Invoke(connected);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || attacks.Phase != AttackPhase.Active || attacks.Current == null)
                return;

            HitboxShape shape = attacks.Current.Hitbox;
            Vector3 facing = motor != null && motor.Locomotion != null ? motor.Locomotion.Facing : transform.forward;
            Vector3 origin = transform.position + Vector3.up * hitboxHeightOffset;
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);

            if (shape.Kind == HitboxKind.Box)
            {
                Gizmos.matrix = Matrix4x4.TRS(shape.WorldCenter(origin, facing), Quaternion.LookRotation(facing, Vector3.up), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, shape.Size);
            }
            else
            {
                Gizmos.DrawWireSphere(shape.WorldCenter(origin, facing), shape.Radius);
            }
        }
#endif
    }
}
