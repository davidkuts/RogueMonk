using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Drives the ranged enemy: hold a distance band, telegraph, then fire a straight
    /// projectile at the end of the wind-up. Like the melee grunt it runs its attack on the
    /// shared <see cref="AttackStateMachine"/>, so the telegraph is committed and readable
    /// and an interrupt cancels the shot before it is ever spawned.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class RangedEnemyController : MonoBehaviour
    {
        [SerializeField] EnemyActor actor;
        [SerializeField] Transform target;
        [SerializeField] Projectile projectilePrefab;
        [SerializeField, Tooltip("Layers the projectile can damage.")]
        LayerMask hittableLayers;
        [SerializeField, Tooltip("Layers that stop a projectile dead (walls, obstacles).")]
        LayerMask blockingLayers;
        [SerializeField] float turnSpeedDegPerSec = 360f;
        [SerializeField] float gravity = -25f;
        [SerializeField] float groundedStickSpeed = 2f;

        readonly AttackStateMachine attacks = new AttackStateMachine();
        readonly HitResolver resolver = new HitResolver();

        RangedEnemyBrain brain;
        CharacterController controller;
        AttackDefinition attackAsset;
        float verticalSpeed;
        bool shotFired;

        public RangedEnemyBrain Brain => brain;

        public AttackStateMachine Attacks => attacks;

        public HitResolver Resolver => resolver;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();
            controller = GetComponent<CharacterController>();

            IEnemyDefinition definition = actor.Definition;
            if (definition == null || definition.Attack == null || projectilePrefab == null)
            {
                Debug.LogError($"{nameof(RangedEnemyController)} on '{name}' needs a definition, an attack and a projectile prefab.", this);
                enabled = false;
                return;
            }

            attackAsset = definition.Attack as AttackDefinition;
            brain = new RangedEnemyBrain(definition);
            brain.StateChanged += OnStateChanged;
            attacks.ActiveStarted += OnActiveStarted;
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

            // A ranged enemy keeps facing the player even while repositioning — its threat is
            // where it is looking, and the player needs that read.
            if (hasTarget && !actor.IsStaggered)
                FaceTowards(target.position - transform.position, deltaTime);

            Vector3 planarVelocity = Vector3.zero;
            if (!actor.IsStaggered && !attacks.IsAttacking && hasTarget && !Mathf.Approximately(brain.MoveSpeedFraction, 0f))
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                    planarVelocity = toTarget.normalized * (definition.MoveSpeed * brain.MoveSpeedFraction);
            }

            verticalSpeed = controller.isGrounded ? -groundedStickSpeed : verticalSpeed + gravity * deltaTime;
            Vector3 motion = planarVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);

            UpdateTelegraph();
        }

        void StartAttack()
        {
            IEnemyDefinition definition = actor.Definition;
            if (!attacks.TryStart(definition.Attack))
                return;

            shotFired = false;
            GameLog.Info(LogCategory.Enemy,
                $"telegraph {definition.Id}  {definition.Attack.Id}  windup {definition.Attack.WindupSeconds:0.000}s (ranged)");
        }

        void OnActiveStarted(IAttackDefinition definition)
        {
            // Fire once, at the moment the wind-up completes. An interrupt before this point
            // means no projectile is ever created, which is what makes the telegraph punishable.
            if (shotFired || target == null)
                return;

            shotFired = true;

            // Projectiles fly flat (ProjectileMotion is planar), so the launch height must be
            // the target's centre height or the shot sails over its head. Deriving it from the
            // target rather than from a fixed muzzle offset keeps it correct whatever the two
            // capsules' sizes are.
            float launchHeight = target.position.y;
            Vector3 origin = new Vector3(transform.position.x, launchHeight, transform.position.z);
            Vector3 direction = target.position - origin;
            direction.y = 0f;

            Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
            projectile.Launch(
                origin, direction, definition, resolver, transform,
                actor.Definition.Ranged, hittableLayers, blockingLayers);

            GameLog.Info(LogCategory.Enemy,
                $"fire {actor.Definition.Id}  speed {actor.Definition.Ranged.ProjectileSpeed:0.##}m/s  " +
                $"lifetime {actor.Definition.Ranged.ProjectileLifetime:0.##}s");
        }

        void OnAttackEnded(IAttackDefinition definition) => brain.NotifyAttackFinished();

        void OnStaggered()
        {
            if (attacks.IsAttacking)
            {
                GameLog.Info(LogCategory.Enemy,
                    $"interrupted {actor.Definition.Id} during {attacks.Phase}" +
                    (shotFired ? string.Empty : " - shot never left"));
                attacks.Cancel();
            }

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
        }

        void FaceTowards(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            float maxRadians = turnSpeedDegPerSec * Mathf.Deg2Rad * deltaTime;
            Vector3 forward = Vector3.RotateTowards(transform.forward, direction.normalized, maxRadians, 0f);
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
