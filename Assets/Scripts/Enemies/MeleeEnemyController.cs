using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Drives the melee humanoid: chase, telegraphed lunge, punish window.
    ///
    /// The attack itself runs on the <em>same</em> <see cref="AttackStateMachine"/> the player
    /// uses, so enemy frame data means exactly what player frame data means, and the wind-up is
    /// as readable and as committed. Movement is direct steering — NavMesh arrives with the
    /// room prefabs in M6 (DESIGN.md bakes it per room, and the gray-box arena has none).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class MeleeEnemyController : MonoBehaviour
    {
        [SerializeField] EnemyActor actor;
        [SerializeField, Tooltip("Left empty, the player is found by tag at startup.")]
        Transform target;
        [SerializeField, Tooltip("Layers this enemy's attack can hit — the player, not other enemies.")]
        LayerMask hittableLayers;
        [SerializeField] float hitboxHeightOffset = 0.9f;
        [SerializeField, Tooltip("Turn rate while chasing.")]
        float turnSpeedDegPerSec = 540f;
        [SerializeField] float gravity = -25f;
        [SerializeField] float groundedStickSpeed = 2f;

        [Header("Telegraph")]
        [SerializeField, Tooltip("Ground footprint drawn during wind-up. Without it a 450 ms tell can only be answered by frame-perfect timing; with it the attack is answered by standing somewhere else.")]
        TelegraphDecal decal;

        readonly AttackStateMachine attacks = new AttackStateMachine();
        readonly HitResolver resolver = new HitResolver();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly Collider[] overlapResults = new Collider[8];

        MeleeEnemyBrain brain;
        CharacterController controller;
        AttackDefinition attackAsset;
        Vector3 lungeVelocity;
        float verticalSpeed;

        public MeleeEnemyBrain Brain => brain;

        public AttackStateMachine Attacks => attacks;

        /// <summary>The hit pipeline for this enemy's attacks. Boons and debuffs hook in here.</summary>
        public HitResolver Resolver => resolver;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();
            controller = GetComponent<CharacterController>();

            IEnemyDefinition definition = actor.Definition;
            if (definition == null || definition.Attack == null)
            {
                Debug.LogError($"{nameof(MeleeEnemyController)} on '{name}' has no definition or attack.", this);
                enabled = false;
                return;
            }

            attackAsset = definition.Attack as AttackDefinition;
            brain = new MeleeEnemyBrain(definition);
            brain.StateChanged += OnStateChanged;
            attacks.ActiveStarted += OnActiveStarted;
            attacks.ActiveEnded += OnActiveEnded;
            attacks.AttackEnded += OnAttackEnded;
            actor.Staggered += OnStaggered;

            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    target = player.transform;
            }
        }

        void OnDestroy()
        {
            if (brain != null) brain.StateChanged -= OnStateChanged;
            attacks.ActiveStarted -= OnActiveStarted;
            attacks.ActiveEnded -= OnActiveEnded;
            attacks.AttackEnded -= OnAttackEnded;
            if (actor != null) actor.Staggered -= OnStaggered;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || !actor.IsAlive)
                return;

            IEnemyDefinition definition = actor.Definition;
            bool hasTarget = target != null;
            float distance = hasTarget ? PlanarDistanceTo(target.position) : float.MaxValue;

            attacks.Tick(deltaTime);
            brain.Tick(deltaTime, distance, hasTarget, attacks.IsAttacking, actor.IsStaggered);

            if (brain.WantsToAttack)
                StartAttack();

            Vector3 planarVelocity = ResolveMovement(deltaTime, definition, hasTarget);

            verticalSpeed = controller.isGrounded ? -groundedStickSpeed : verticalSpeed + gravity * deltaTime;
            Vector3 motion = planarVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);

            if (attacks.Phase == AttackPhase.Active)
                QueryHitbox();

            UpdateTelegraph();
        }

        Vector3 ResolveMovement(float deltaTime, IEnemyDefinition definition, bool hasTarget)
        {
            // A staggered enemy is inert; that is the whole point of the punish window.
            if (actor.IsStaggered)
            {
                lungeVelocity = Vector3.zero;
                return Vector3.zero;
            }

            if (attacks.IsAttacking)
            {
                // The lunge carries through the active frames and bleeds off after them.
                lungeVelocity = attacks.Phase == AttackPhase.Active
                    ? lungeVelocity
                    : Vector3.MoveTowards(lungeVelocity, Vector3.zero, definition.MoveSpeed * 6f * deltaTime);

                // Facing is committed from the wind-up onward — the telegraph must not track.
                return lungeVelocity;
            }

            lungeVelocity = Vector3.zero;
            if (!hasTarget || brain.MoveSpeedFraction <= 0f)
                return Vector3.zero;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 direction = toTarget.normalized;
            FaceTowards(direction, deltaTime);
            return direction * (definition.MoveSpeed * brain.MoveSpeedFraction);
        }

        void StartAttack()
        {
            IEnemyDefinition definition = actor.Definition;

            // Aim is locked in at commit time: the player reads the wind-up and steps out of it.
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0f)
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            if (!attacks.TryStart(definition.Attack))
                return;

            alreadyHit.Clear();
            GameLog.Info(LogCategory.Enemy,
                $"telegraph {definition.Id}  {definition.Attack.Id}  " +
                $"windup {definition.Attack.WindupSeconds:0.000}s  active {definition.Attack.ActiveSeconds:0.000}s");
        }

        void OnActiveStarted(IAttackDefinition definition)
        {
            alreadyHit.Clear();

            float active = Mathf.Max(0.0001f, definition.ActiveSeconds);
            lungeVelocity = transform.forward * (actor.Definition.LungeDistance / active);
        }

        void OnActiveEnded(IAttackDefinition definition) => alreadyHit.Clear();

        void OnAttackEnded(IAttackDefinition definition)
        {
            lungeVelocity = Vector3.zero;
            brain.NotifyAttackFinished();
        }

        void OnStaggered()
        {
            if (attacks.IsAttacking)
            {
                GameLog.Info(LogCategory.Enemy, $"interrupted {actor.Definition.Id} during {attacks.Phase}");
                attacks.Cancel();
            }

            lungeVelocity = Vector3.zero;
            brain.NotifyInterrupted();
        }

        void OnStateChanged(EnemyState previous, EnemyState next) =>
            GameLog.Debug(LogCategory.Enemy, $"{actor.Definition.Id} {previous} -> {next}");

        void UpdateTelegraph()
        {
            bool telegraphing = attacks.Phase == AttackPhase.Windup;
            actor.TelegraphOverride = telegraphing && attackAsset != null
                ? attackAsset.TelegraphColor
                : (Color?)null;
            actor.TelegraphProgress = telegraphing ? attacks.WindupProgress : 0f;

            if (decal == null)
                return;

            // Drawn from the attack's real hitbox, so the warning cannot lie about its reach. The
            // grunt's is a sphere offset forward — a circle in front of it rather than around it,
            // which is what makes stepping around the side an answer.
            if (telegraphing && attackAsset != null)
            {
                decal.Show(
                    attackAsset.Hitbox,
                    transform.position + Vector3.up * hitboxHeightOffset,
                    transform.forward,
                    attackAsset.TelegraphColor,
                    attacks.WindupProgress,
                    FeetY);
            }
            else
            {
                decal.Hide();
            }
        }

        float FeetY => transform.position.y - (controller != null ? controller.height * 0.5f : 0.9f);

        void QueryHitbox()
        {
            IAttackDefinition definition = attacks.Current;
            if (definition == null)
                return;

            HitboxShape shape = definition.Hitbox;
            Vector3 origin = transform.position + Vector3.up * hitboxHeightOffset;
            Vector3 center = shape.WorldCenter(origin, transform.forward);

            int count = shape.Kind == HitboxKind.Box
                ? Physics.OverlapBoxNonAlloc(center, shape.Size * 0.5f, overlapResults,
                    Quaternion.LookRotation(transform.forward, Vector3.up), hittableLayers, QueryTriggerInteraction.Collide)
                : Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, shape.Radius), overlapResults,
                    hittableLayers, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || alreadyHit.Contains(damageable))
                    continue;

                alreadyHit.Add(damageable);

                Vector3 direction = collider.transform.position - transform.position;
                HitContext context = HitContext.FromAttack(
                    definition, damageable, direction, collider.ClosestPoint(center));
                resolver.Resolve(ref context);
            }
        }

        void FaceTowards(Vector3 direction, float deltaTime)
        {
            float maxRadians = turnSpeedDegPerSec * Mathf.Deg2Rad * deltaTime;
            Vector3 forward = Vector3.RotateTowards(transform.forward, direction, maxRadians, 0f);
            forward.y = 0f;
            if (forward.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        float PlanarDistanceTo(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }
    }
}
