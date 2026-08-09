using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The adapter every Biome 1 archetype runs on: chase, choose a move, telegraph it, swing it,
    /// hand the attack token back.
    ///
    /// <para>Usable unchanged for an enemy whose behaviour is entirely "pick a move and throw it"
    /// — Swiftjaw is exactly that. Everything with a bespoke verb subclasses it and overrides one
    /// or two hooks: a charge that locks its line, a roll that ends in a wall, a fan that draws
    /// several telegraph lanes. That keeps the bespoke code to the part that is genuinely bespoke
    /// and leaves gravity, facing, hit queries and token bookkeeping written once.</para>
    ///
    /// <para>Deliberately <em>not</em> shared with <c>MeleeEnemyController</c> or
    /// <c>BossController</c>. Both are signed off and one is the boss fight; folding them in would
    /// put working content at risk for a tidiness win. The overlap is recorded in PROGRESS.md as
    /// something to collapse once the roster is proven, when it will be obvious which parts really
    /// are common.</para>
    ///
    /// <para>Decisions stay in <see cref="EnemyMovesetBrain"/>, which is engine-free and tested.
    /// This class owns only what needs a Transform (CLAUDE.md rule 1).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(CharacterController))]
    public class MovesetEnemyController : MonoBehaviour
    {
        [SerializeField] protected EnemyActor actor;

        [SerializeField, Tooltip("Left empty, the player is found by tag at startup.")]
        protected Transform target;

        [SerializeField, Tooltip("Layers this enemy's attacks can hit — the player, not other enemies. Cerashorn's charge deliberately widens this at runtime.")]
        protected LayerMask hittableLayers;

        [SerializeField, Tooltip("Height the hitbox is measured from. Matches the telegraph so the decal cannot promise a different volume than the query.")]
        protected float hitboxHeightOffset = 0.9f;

        [SerializeField] protected float gravity = -25f;
        [SerializeField] protected float groundedStickSpeed = 2f;

        [Header("Telegraph")]
        [SerializeField, Tooltip("Draws the wind-up. Without one the enemy still fights, but its attacks arrive unannounced — which DESIGN.md forbids, so its absence is logged loudly.")]
        protected TelegraphPresenter telegraph;

        protected readonly AttackStateMachine attacks = new AttackStateMachine();
        protected readonly HitResolver resolver = new HitResolver();
        readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();
        readonly Collider[] overlapResults = new Collider[12];

        BiomeEnemyDefinition definition;
        CharacterController controller;
        IRandomSource random;
        Vector3 lungeVelocity;
        float verticalSpeed;
        bool holdsToken;

        public EnemyMovesetBrain Brain { get; private set; }

        public AttackStateMachine Attacks => attacks;

        /// <summary>The hit pipeline for this enemy's attacks.</summary>
        public HitResolver Resolver => resolver;

        public BiomeEnemyDefinition Definition => definition;

        protected CharacterController Controller => controller;

        protected IRandomSource Random => random;

        /// <summary>The move currently being executed. Null between moves.</summary>
        protected IEnemyMove CurrentMove => Brain?.CurrentMove;

        /// <summary>The attacker's floor level, handed to the decal so it lies on the ground.</summary>
        protected float FeetY => transform.position.y - (controller != null ? controller.height * 0.5f : 0.9f);

        /// <summary>Where hitboxes and telegraphs are measured from.</summary>
        protected Vector3 HitboxOrigin => transform.position + Vector3.up * hitboxHeightOffset;

        /// <summary>
        /// Hands this enemy its seeded stream. Called by the spawner exactly as it is for a boss,
        /// because move selection draws a variable number of times — once per move actually thrown,
        /// which depends on the player. Drawing from the run stream directly would make the seed
        /// stop reproducing the run.
        /// </summary>
        public void Bind(IRandomSource source)
        {
            random = source;
            BuildBrain();
        }

        protected virtual void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();

            controller = GetComponent<CharacterController>();
            definition = actor.Definition as BiomeEnemyDefinition;

            if (definition == null)
            {
                Debug.LogError(
                    $"{GetType().Name} on '{name}' needs a {nameof(BiomeEnemyDefinition)}; " +
                    $"it has '{actor.Definition?.GetType().Name ?? "nothing"}'.", this);
                enabled = false;
                return;
            }

            if (telegraph == null)
                telegraph = GetComponent<TelegraphPresenter>();

            if (telegraph == null)
            {
                GameLog.Warn(LogCategory.Enemy,
                    $"{definition.Id} has no {nameof(TelegraphPresenter)} - its attacks will arrive with no tell at all");
            }

            attacks.ActiveStarted += OnActiveStarted;
            attacks.ActiveEnded += OnActiveEnded;
            attacks.AttackEnded += OnAttackEnded;
            actor.Staggered += OnStaggered;
            actor.DeathSequenceStarted += OnDeathStarted;

            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    target = player.transform;
            }

            // A lab spawn has no RunContext to derive from. Fall back to a stream keyed on the
            // archetype so the enemy still fights and still behaves the same way twice, rather
            // than reaching for UnityEngine.Random (CLAUDE.md rule 5).
            if (random == null)
                random = new XorShiftRandom((uint)definition.Id.GetHashCode());

            BuildBrain();
        }

        void BuildBrain()
        {
            if (definition == null)
                return;

            if (Brain != null)
                Brain.MoveChosen -= OnMoveChosen;

            Brain = new EnemyMovesetBrain(
                definition, definition.Moves, random, definition.RepeatWeightMultiplier);
            Brain.MoveChosen += OnMoveChosen;
        }

        protected virtual void OnDestroy()
        {
            attacks.ActiveStarted -= OnActiveStarted;
            attacks.ActiveEnded -= OnActiveEnded;
            attacks.AttackEnded -= OnAttackEnded;

            if (actor != null)
            {
                actor.Staggered -= OnStaggered;
                actor.DeathSequenceStarted -= OnDeathStarted;
            }

            if (Brain != null)
                Brain.MoveChosen -= OnMoveChosen;

            ReleaseToken();
        }

        protected virtual void OnDisable() => ReleaseToken();

        protected virtual void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || definition == null || !actor.IsAlive)
                return;

            bool hasTarget = target != null;
            float distance = hasTarget ? PlanarDistanceTo(target.position) : float.MaxValue;

            attacks.Tick(deltaTime);

            // Asked without taking, so the brain can treat "someone else is swinging" as an
            // ordinary reason to hold rather than committing to a move and being refused after
            // its cooldown has already been spent.
            bool permitted = holdsToken || AttackTokenBroker.CanAcquire(
                this, definition.TokenKind, definition.Id, definition.ConcurrentAttackerCap);

            Brain.Tick(deltaTime, distance, hasTarget, attacks.IsAttacking, actor.IsStaggered, permitted);

            if (Brain.WantsToAttack)
                TryStartAttack();

            Vector3 planarVelocity = ResolveMovement(deltaTime, hasTarget);

            verticalSpeed = controller.isGrounded ? -groundedStickSpeed : verticalSpeed + gravity * deltaTime;
            Vector3 motion = planarVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);

            if (attacks.Phase == AttackPhase.Active)
                QueryHitbox();

            UpdateTelegraph();
        }

        void TryStartAttack()
        {
            IAttackDefinition pending = Brain.PendingAttack;
            if (pending == null)
                return;

            // Mid-chain links do not re-ask: the enemy already holds the token for this move, and
            // making link two compete for one would leave a combo half-thrown.
            if (!holdsToken)
            {
                if (!AttackTokenBroker.TryAcquire(
                        this, definition.TokenKind, definition.Id, definition.ConcurrentAttackerCap))
                {
                    return;
                }

                holdsToken = true;
            }

            // Aim locks at commit time. The player reads the wind-up and steps out of it, which
            // only works if the telegraph cannot follow them.
            if (target != null && ShouldFaceTargetOnCommit)
            {
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0f)
                    transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            if (!attacks.TryStart(pending))
                return;

            alreadyHit.Clear();

            GameLog.Info(LogCategory.Enemy,
                $"telegraph {definition.Id}  {CurrentMove?.Id ?? "?"}[{Brain.LinkIndex}] {pending.Id}  " +
                $"windup {pending.WindupSeconds:0.000}s  active {pending.ActiveSeconds:0.000}s");

            OnAttackStarted(pending);
        }

        /// <summary>
        /// Planar velocity for this frame. Split out so a subclass can replace it wholesale — a
        /// committed charge ignores steering entirely, which is exactly what makes it baitable.
        /// </summary>
        protected virtual Vector3 ResolveMovement(float deltaTime, bool hasTarget)
        {
            if (actor.IsStaggered)
            {
                lungeVelocity = Vector3.zero;
                return Vector3.zero;
            }

            if (attacks.IsAttacking)
            {
                lungeVelocity = attacks.Phase == AttackPhase.Active
                    ? lungeVelocity
                    : Vector3.MoveTowards(lungeVelocity, Vector3.zero, definition.MoveSpeed * 6f * deltaTime);

                return lungeVelocity;
            }

            lungeVelocity = Vector3.zero;

            float fraction = Brain.MoveSpeedFraction;
            if (!hasTarget || Mathf.Approximately(fraction, 0f))
                return Vector3.zero;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return Vector3.zero;

            Vector3 direction = toTarget.normalized;

            // Facing follows the target even while backing away: a Sailspit that retreats
            // backwards-facing would spit over its shoulder, and the sail is the silhouette the
            // player is meant to read.
            FaceTowards(direction, deltaTime);

            return direction * (definition.MoveSpeed * fraction * actor.StatusMoveSpeedMultiplier);
        }

        /// <summary>
        /// Draws the wind-up. Overridden by anything whose footprint is not one shape — Sailspit's
        /// spine fan draws a lane per spine, because the gaps between them are the answer.
        /// </summary>
        protected virtual void UpdateTelegraph()
        {
            if (telegraph == null)
                return;

            bool telegraphing = attacks.Phase == AttackPhase.Windup;
            if (!telegraphing || !(attacks.Current is AttackDefinition asset))
            {
                telegraph.Hide();
                return;
            }

            telegraph.Show(asset, HitboxOrigin, transform.forward, attacks.WindupProgress, FeetY);
        }

        /// <summary>
        /// Resolves the live hitbox. Public shape so a subclass can trigger it off-schedule — a
        /// charge damages whatever it runs into for the whole of its travel, not for an active
        /// window measured in frames.
        /// </summary>
        protected void QueryHitbox() => QueryHitbox(attacks.Current, transform.forward, hittableLayers);

        protected void QueryHitbox(IAttackDefinition definitionToUse, Vector3 forward, LayerMask layers)
        {
            if (definitionToUse == null)
                return;

            HitboxShape shape = definitionToUse.Hitbox;
            Vector3 origin = HitboxOrigin;
            Vector3 center = shape.WorldCenter(origin, forward);

            int count = HitboxQuery.Overlap(shape, origin, forward, layers, overlapResults);

            for (int i = 0; i < count; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                if (!HitboxQuery.Contains(shape, origin, forward, collider.transform.position))
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || alreadyHit.Contains(damageable))
                    continue;

                alreadyHit.Add(damageable);

                Vector3 direction = collider.transform.position - transform.position;
                HitContext context = HitContext.FromAttack(
                    definitionToUse, damageable, direction, collider.ClosestPoint(center), HitZones.Resolve(collider));

                resolver.Resolve(ref context);
                OnHitLanded(collider, damageable);
            }
        }

        protected void FaceTowards(Vector3 direction, float deltaTime)
        {
            float maxRadians = definition.TurnSpeedDegPerSec * Mathf.Deg2Rad * deltaTime;
            Vector3 forward = Vector3.RotateTowards(transform.forward, direction, maxRadians, 0f);
            forward.y = 0f;
            if (forward.sqrMagnitude > 0f)
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        protected float PlanarDistanceTo(Vector3 position)
        {
            Vector3 delta = position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <summary>Clears the per-swing hit set, so a chain's second link can hit the same target.</summary>
        protected void ClearAlreadyHit() => alreadyHit.Clear();

        protected void SetLungeVelocity(Vector3 velocity) => lungeVelocity = velocity;

        // --- Hooks ---

        /// <summary>False for anything that must not snap onto the player when it commits.</summary>
        protected virtual bool ShouldFaceTargetOnCommit => true;

        protected virtual void OnMoveChosen(IEnemyMove move) =>
            GameLog.Debug(LogCategory.Enemy, $"{definition.Id} chose {move.Id}");

        protected virtual void OnAttackStarted(IAttackDefinition attack) { }

        protected virtual void OnHitLanded(Collider collider, IDamageable damageable) { }

        protected virtual void OnActiveStarted(IAttackDefinition attack)
        {
            ClearAlreadyHit();

            float active = Mathf.Max(0.0001f, attack.ActiveSeconds);
            float lunge = CurrentMove?.LungeDistance ?? definition.LungeDistance;
            lungeVelocity = transform.forward * (lunge / active);
        }

        protected virtual void OnActiveEnded(IAttackDefinition attack) => ClearAlreadyHit();

        protected virtual void OnAttackEnded(IAttackDefinition attack)
        {
            lungeVelocity = Vector3.zero;
            Brain.NotifyLinkFinished();

            // The token is held for the whole move, not for one link — otherwise the gap between
            // links would let another enemy in and a chain would be interleaved with someone
            // else's wind-up.
            if (!Brain.IsMidChain)
                ReleaseToken();
        }

        protected virtual void OnStaggered()
        {
            if (attacks.IsAttacking)
            {
                GameLog.Info(LogCategory.Enemy, $"interrupted {definition.Id} during {attacks.Phase}");
                attacks.Cancel();
            }

            lungeVelocity = Vector3.zero;
            Brain.NotifyInterrupted();
            ReleaseToken();

            if (telegraph != null)
                telegraph.Hide();
        }

        protected virtual void OnDeathStarted()
        {
            attacks.Cancel();
            Brain.NotifyDied();
            ReleaseToken();

            if (telegraph != null)
                telegraph.Hide();
        }

        /// <summary>
        /// Hands the attack token back. Called from every exit path there is — attack finished,
        /// staggered, died, disabled, destroyed — because a leaked token never recovers and ends
        /// with a room full of enemies that will not attack.
        /// </summary>
        protected void ReleaseToken()
        {
            if (!holdsToken)
                return;

            holdsToken = false;
            AttackTokenBroker.Release(this);
        }
    }
}
