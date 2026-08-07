using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Drives the boss. A thin adapter over <see cref="BossBrain"/>: it forwards time and distance
    /// in, and turns the brain's answers into attacks, movement and telegraphs.
    ///
    /// Every attack still runs on the shared <see cref="AttackStateMachine"/>, so a boss wind-up is
    /// as committed and as readable as a grunt's. What differs is that the boss picks between
    /// several moves, and that its telegraph colour is read per <em>attack</em> rather than cached
    /// once per enemy — a four-move boss whose every wind-up looked the same would be unreadable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField] EnemyActor actor;
        [SerializeField, Tooltip("Left empty, the player is found by tag at startup.")]
        Transform target;
        [SerializeField, Tooltip("Layers this boss's attacks can hit — the player, not other enemies.")]
        LayerMask hittableLayers;
        [SerializeField, Tooltip("Layers that stop a projectile without being damaged (walls).")]
        LayerMask blockingLayers;
        [SerializeField] Projectile projectilePrefab;

        [SerializeField] float hitboxHeightOffset = 1.1f;
        [SerializeField, Tooltip("Turn rate while repositioning. Facing locks the moment a move commits.")]
        float turnSpeedDegPerSec = 360f;
        [SerializeField] float gravity = -25f;
        [SerializeField] float groundedStickSpeed = 2f;

        [Header("Telegraph")]
        [SerializeField, Tooltip("Ground footprint drawn during wind-up. The second readability channel: colour says an attack is coming, this says which one and how far it reaches.")]
        TelegraphDecal decal;

        [Header("Phase break")]
        [SerializeField, Tooltip("Colour held through the inert window after a phase threshold. This is the punish window an Immune enemy earns with damage instead of poise, so it must read as clearly as a stagger.")]
        Color phaseTransitionColor = new Color(0.55f, 0.85f, 1f);

        readonly AttackStateMachine attacks = new AttackStateMachine();
        readonly HitResolver resolver = new HitResolver();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly Collider[] overlapResults = new Collider[8];

        BossBrain brain;
        IBossDefinition definition;
        CharacterController controller;
        Vector3 lungeVelocity;
        float verticalSpeed;
        bool firedThisLink;
        bool unboundWarned;

        public BossBrain Brain => brain;

        public AttackStateMachine Attacks => attacks;

        /// <summary>The hit pipeline for this boss's attacks. Boons and debuffs hook in here.</summary>
        public HitResolver Resolver => resolver;

        public IBossDefinition Definition => definition;

        public bool IsBound => brain != null;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();
            controller = GetComponent<CharacterController>();

            definition = actor.Definition as IBossDefinition;
            if (definition == null)
            {
                Debug.LogError($"{nameof(BossController)} on '{name}' needs a {nameof(BossDefinition)}.", this);
                enabled = false;
                return;
            }

            attacks.ActiveStarted += OnActiveStarted;
            attacks.ActiveEnded += OnActiveEnded;
            attacks.AttackEnded += OnAttackEnded;
            actor.DeathSequenceStarted += OnDeathStarted;

            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    target = player.transform;
            }
        }

        /// <summary>
        /// Supplies the seeded stream the brain draws its move choices from.
        ///
        /// The brain is built here rather than in <c>Awake</c> on purpose: <c>Instantiate</c> runs
        /// <c>Awake</c> synchronously, before whoever spawned the boss has had a chance to hand
        /// over the stream, so a brain built there would be holding nothing.
        /// </summary>
        public void Bind(IRandomSource random)
        {
            if (definition == null)
                return;

            brain = new BossBrain(definition, random);
            brain.StateChanged += OnStateChanged;
            brain.PhaseChanged += OnPhaseChanged;
            brain.MoveChosen += OnMoveChosen;

            GameLog.Info(LogCategory.Enemy,
                $"BOSS {definition.DisplayName} bound  {definition.Moves.Count} move(s), " +
                $"{brain.PhaseCount} phase(s), seed {random.Seed}");
        }

        void OnDestroy()
        {
            if (brain != null)
            {
                brain.StateChanged -= OnStateChanged;
                brain.PhaseChanged -= OnPhaseChanged;
                brain.MoveChosen -= OnMoveChosen;
            }

            attacks.ActiveStarted -= OnActiveStarted;
            attacks.ActiveEnded -= OnActiveEnded;
            attacks.AttackEnded -= OnAttackEnded;
            if (actor != null) actor.DeathSequenceStarted -= OnDeathStarted;
        }

        void Update()
        {
            if (brain == null)
            {
                if (!unboundWarned)
                {
                    unboundWarned = true;
                    GameLog.Error(LogCategory.Enemy,
                        $"boss '{name}' is running unbound - whoever spawned it never called Bind(), so it will never act");
                }

                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || !actor.IsAlive)
                return;

            bool hasTarget = target != null;
            float distance = hasTarget ? PlanarDistanceTo(target.position) : float.MaxValue;

            attacks.Tick(deltaTime);
            brain.Tick(deltaTime, distance, hasTarget, attacks.IsAttacking, HealthFraction);

            if (brain.WantsToAttack)
                StartLink(brain.PendingAttack);

            Vector3 planarVelocity = ResolveMovement(deltaTime, hasTarget);

            verticalSpeed = controller.isGrounded ? -groundedStickSpeed : verticalSpeed + gravity * deltaTime;
            Vector3 motion = planarVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);

            if (attacks.Phase == AttackPhase.Active)
                QueryHitbox();

            UpdateTelegraph();
        }

        public float HealthFraction =>
            actor.Health != null && actor.Health.Max > 0f
                ? Mathf.Clamp01(actor.Health.Current / actor.Health.Max)
                : 0f;

        Vector3 ResolveMovement(float deltaTime, bool hasTarget)
        {
            if (attacks.IsAttacking)
            {
                // The lunge carries through the active frames and bleeds off after them. Facing is
                // committed from the wind-up onward, so the telegraph never tracks the player.
                lungeVelocity = attacks.Phase == AttackPhase.Active
                    ? lungeVelocity
                    : Vector3.MoveTowards(lungeVelocity, Vector3.zero, definition.MoveSpeed * 6f * deltaTime);

                return lungeVelocity;
            }

            lungeVelocity = Vector3.zero;

            if (!hasTarget || brain.State == BossState.PhaseTransition || brain.State == BossState.Dead)
                return Vector3.zero;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 direction = toTarget.normalized;
            FaceTowards(direction, deltaTime);

            // Signed: positive closes, negative backs away toward a band it can actually use.
            return direction * (definition.MoveSpeed * brain.MoveSpeedFraction);
        }

        void StartLink(IAttackDefinition attack)
        {
            if (attack == null)
                return;

            // Aim locks in at commit time: the player reads the wind-up and steps out of it.
            if (target != null)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0f)
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            if (!attacks.TryStart(attack))
                return;

            alreadyHit.Clear();
            firedThisLink = false;

            IBossMove move = brain.CurrentMove;
            GameLog.Info(LogCategory.Enemy,
                $"BOSS telegraph {move?.Id}[{brain.LinkIndex}] {attack.Id}  " +
                $"windup {attack.WindupSeconds:0.000}s  active {attack.ActiveSeconds:0.000}s  phase {brain.PhaseIndex}");
        }

        void OnActiveStarted(IAttackDefinition attack)
        {
            alreadyHit.Clear();

            IBossMove move = brain?.CurrentMove;
            if (move == null)
                return;

            float active = Mathf.Max(0.0001f, attack.ActiveSeconds);
            lungeVelocity = transform.forward * (move.LungeDistance / active);

            if (move.ProjectileCount > 0)
                FireFan(attack, move);
        }

        /// <summary>
        /// Fires the move's projectile fan once, the moment the wind-up completes. An interrupt
        /// before this point means no projectile is ever created — that is what makes the
        /// telegraph worth reading.
        /// </summary>
        void FireFan(IAttackDefinition attack, IBossMove move)
        {
            if (firedThisLink || target == null || projectilePrefab == null)
                return;

            firedThisLink = true;

            // Projectiles fly flat, so the launch height must be the target's centre or the shot
            // sails over its head — the M5 bug, and it only shows up in a build.
            float launchHeight = target.position.y;
            Vector3 origin = new Vector3(transform.position.x, launchHeight, transform.position.z);
            Vector3 toTarget = target.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                toTarget = transform.forward;

            int count = Mathf.Max(1, move.ProjectileCount);
            float spread = Mathf.Max(0f, move.ProjectileSpreadDegrees);
            float step = count > 1 ? spread / (count - 1) : 0f;
            float start = count > 1 ? -spread * 0.5f : 0f;

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(start + step * i, Vector3.up) * toTarget.normalized;

                Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
                projectile.Launch(
                    origin, direction, attack, resolver, transform,
                    definition.Ranged, hittableLayers, blockingLayers);
            }

            GameLog.Info(LogCategory.Enemy,
                $"BOSS fire {move.Id}  {count} projectile(s) over {spread:0.#}deg  " +
                $"speed {definition.Ranged.ProjectileSpeed:0.##}m/s");
        }

        void OnActiveEnded(IAttackDefinition attack) => alreadyHit.Clear();

        void OnAttackEnded(IAttackDefinition attack)
        {
            lungeVelocity = Vector3.zero;
            brain?.NotifyLinkFinished();
        }

        void OnDeathStarted()
        {
            if (attacks.IsAttacking)
                attacks.Cancel();

            lungeVelocity = Vector3.zero;
            actor.TelegraphOverride = null;
            actor.TelegraphProgress = 0f;
            if (decal != null)
                decal.Hide();
            brain?.NotifyDied();
        }

        void OnStateChanged(BossState previous, BossState next) =>
            GameLog.Debug(LogCategory.Enemy, $"BOSS {definition.Id} {previous} -> {next}");

        void OnPhaseChanged(int phaseIndex) =>
            GameLog.Info(LogCategory.Enemy,
                $"BOSS PHASE {phaseIndex + 1}/{brain.PhaseCount}  {definition.DisplayName} - " +
                $"vulnerable for {definition.PhaseTransitionSeconds:0.00}s");

        void OnMoveChosen(IBossMove move) =>
            GameLog.Debug(LogCategory.Enemy, $"BOSS chose {move.Id} at phase {brain.PhaseIndex}");

        /// <summary>
        /// Reads the telegraph colour live off the attack currently running, rather than caching
        /// one per enemy the way the trash controllers do. That is what gives each of the boss's
        /// moves its own tell.
        /// </summary>
        void UpdateTelegraph()
        {
            if (brain.State == BossState.PhaseTransition)
            {
                // The phase break is the boss's stagger. Hold a flat, distinct colour for it.
                actor.TelegraphOverride = phaseTransitionColor;
                actor.TelegraphProgress = 1f;
                if (decal != null)
                    decal.Hide();
                return;
            }

            bool telegraphing = attacks.Phase == AttackPhase.Windup;
            var current = attacks.Current as AttackDefinition;

            actor.TelegraphOverride = telegraphing && current != null
                ? current.TelegraphColor
                : (Color?)null;
            actor.TelegraphProgress = telegraphing ? attacks.WindupProgress : 0f;

            if (decal == null)
                return;

            // Projectile moves have no ground footprint to draw — their hitbox travels.
            bool hasFootprint = telegraphing
                && current != null
                && (brain.CurrentMove == null || brain.CurrentMove.ProjectileCount <= 0);

            if (hasFootprint)
            {
                decal.Show(
                    current.Hitbox,
                    transform.position + Vector3.up * hitboxHeightOffset,
                    transform.forward,
                    current.TelegraphColor,
                    attacks.WindupProgress);
            }
            else
            {
                decal.Hide();
            }
        }

        void QueryHitbox()
        {
            IAttackDefinition attack = attacks.Current;
            if (attack == null || (brain.CurrentMove != null && brain.CurrentMove.ProjectileCount > 0))
                return;

            HitboxShape shape = attack.Hitbox;
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
                    attack, damageable, direction, collider.ClosestPoint(center));
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
