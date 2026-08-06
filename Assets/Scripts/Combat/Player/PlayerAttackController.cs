using System.Collections.Generic;
using Game.Core.Input;
using Game.Core.Player;
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

        [Header("Feedback")]
        [SerializeField, Tooltip("Optional. Fires a Cinemachine impulse on every connecting hit.")]
        CinemachineImpulseSource impulseSource;
        [SerializeField, Tooltip("Impulse strength scaled by the attack's hitstop, so heavier hits shake harder.")]
        float impulseScale = 1f;

        readonly AttackStateMachine attacks = new AttackStateMachine();
        readonly HitResolver resolver = new HitResolver();
        readonly HitstopController hitstop = new HitstopController();
        readonly InputBuffer attackBuffer = new InputBuffer();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly List<Vector3> candidatePositions = new List<Vector3>();
        readonly List<IDamageable> candidateTargets = new List<IDamageable>();
        readonly List<Transform> candidateTransforms = new List<Transform>();

        readonly Collider[] overlapResults = new Collider[16];

        ComboTracker comboTracker;
        Transform aimTarget;
        bool timeScaleHeld;
        float cachedTimeScale = 1f;

        /// <summary>The hit pipeline. Boons register modifiers here later.</summary>
        public HitResolver Resolver => resolver;

        public AttackStateMachine Attacks => attacks;

        public HitstopController Hitstop => hitstop;

        // --- IPlayerActionState ---

        public float MoveSpeedMultiplier =>
            attacks.IsAttacking && attacks.Current != null ? attacks.Current.MoveSpeedMultiplier : 1f;

        /// <summary>Wind-up and active frames are committed; recovery is dash-cancellable.</summary>
        public bool AllowsDash => !attacks.IsAttacking || attacks.IsCancellable;

        public void CancelForDash()
        {
            if (attacks.IsAttacking)
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
            attacks.ActiveStarted += OnActiveStarted;
            attacks.ActiveEnded += OnActiveEnded;
            resolver.HitApplied += OnHitApplied;
        }

        void OnDestroy()
        {
            attacks.ActiveStarted -= OnActiveStarted;
            attacks.ActiveEnded -= OnActiveEnded;
            resolver.HitApplied -= OnHitApplied;
            ReleaseTimeScale();
        }

        void Update()
        {
            // Hitstop runs on the unscaled clock — it is the thing holding the scaled one at zero.
            hitstop.Tick(Time.unscaledDeltaTime);
            ApplyTimeScale();

            float deltaTime = Time.deltaTime;

            if (input != null && input.AttackPressedThisFrame)
                attackBuffer.Press();
            attackBuffer.Tick(deltaTime, combo.InputBufferSeconds);

            TryConsumeBufferedAttack();

            attacks.Tick(deltaTime);
            comboTracker.Tick(deltaTime, attacks.IsAttacking);

            SteerAimDuringWindup(deltaTime);

            if (attacks.Phase == AttackPhase.Active)
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
            if (!attacks.TryStart(next))
                return;

            attackBuffer.Clear();
            comboTracker.Consume();
            alreadyHit.Clear();
            aimTarget = AcquireAimTarget(next);
        }

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

        void SteerAimDuringWindup(float deltaTime)
        {
            if (aimTarget == null || attacks.Phase != AttackPhase.Windup || attacks.Current == null)
                return;

            Vector3 toTarget = aimTarget.position - transform.position;
            Vector3 steered = AimAssist.RotateFacing(
                motor.Locomotion.Facing, toTarget, attacks.Current.AimSnapSpeedDegPerSec, deltaTime);

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

            int count;
            if (shape.Kind == HitboxKind.Box)
            {
                Quaternion rotation = Quaternion.LookRotation(facing, Vector3.up);
                count = Physics.OverlapBoxNonAlloc(
                    center, shape.Size * 0.5f, overlapResults, rotation, hittableLayers, QueryTriggerInteraction.Collide);
            }
            else
            {
                count = Physics.OverlapSphereNonAlloc(
                    center, Mathf.Max(0f, shape.Radius), overlapResults, hittableLayers, QueryTriggerInteraction.Collide);
            }

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                var target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || alreadyHit.Contains(target))
                    continue;

                alreadyHit.Add(target);

                Vector3 point = collider.ClosestPoint(center);
                Vector3 direction = collider.transform.position - transform.position;
                HitContext context = HitContext.FromAttack(definition, target, direction, point);
                resolver.Resolve(ref context);
            }
        }

        void OnActiveStarted(IAttackDefinition definition) => alreadyHit.Clear();

        void OnActiveEnded(IAttackDefinition definition) => alreadyHit.Clear();

        void OnHitApplied(HitContext context)
        {
            hitstop.Request(context.HitstopSeconds);

            if (impulseSource != null && context.HitstopSeconds > 0f)
                impulseSource.GenerateImpulse(context.Direction * (context.HitstopSeconds * impulseScale));
        }

        void ApplyTimeScale()
        {
            if (hitstop.IsActive && !timeScaleHeld)
            {
                cachedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                timeScaleHeld = true;
            }
            else if (!hitstop.IsActive && timeScaleHeld)
            {
                ReleaseTimeScale();
            }
        }

        void ReleaseTimeScale()
        {
            if (!timeScaleHeld)
                return;

            Time.timeScale = cachedTimeScale;
            timeScaleHeld = false;
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
