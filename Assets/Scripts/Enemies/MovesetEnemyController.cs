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

        [SerializeField, Tooltip("Layers that stop a projectile dead without being damaged (walls). Only read by moves that throw something.")]
        protected LayerMask projectileBlockingLayers;

        [Header("Obstacle avoidance")]
        [SerializeField, Tooltip("Environment layers to steer around. NavMesh is the eventual routing answer (DESIGN.md bakes one per room); this is the local layer that stops a body grinding along a crate.")]
        protected LayerMask obstacleLayers = 1;

        [SerializeField, Tooltip("How far ahead to look. Roughly a second of travel. 0 disables avoidance entirely.")]
        protected float avoidanceProbeDistance = 2.2f;

        [SerializeField, Range(0f, 1f), Tooltip("How hard it turns along an obstacle it is about to hit.")]
        protected float avoidanceStrength = 0.85f;

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
        float circleDirection = 1f;
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
                definition, definition.Moves, random,
                definition.RepeatWeightMultiplier, definition.InitialCooldownJitterSeconds,
                definition.PreferredRange);
            Brain.MoveChosen += OnMoveChosen;

            // Which way round this body circles, drawn once from its own stream. Two raptors that
            // both picked clockwise still separate, because they start at different bearings.
            circleDirection = random.NextBool() ? 1f : -1f;
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

            // In range, off cooldown-free: waiting on a token or on its own recharge. A pack
            // animal that stands still here reads as switched off, and the circling is most of
            // what makes a flanker threatening — it is the reason you cannot watch both of them.
            if (hasTarget && definition.CircleSpeedFraction > 0f &&
                (Brain.State == EnemyState.Waiting || Brain.State == EnemyState.Cooldown))
            {
                return ResolveCircling(deltaTime);
            }

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

            return Avoid(direction * (definition.MoveSpeed * fraction * actor.StatusMoveSpeedMultiplier));
        }

        /// <summary>
        /// Steers a walking velocity around obstacles.
        ///
        /// <para>Applied to ordinary movement only, never to a committed attack. A charge that
        /// swerved would stop being line-locked, and being able to bait it into geometry is the
        /// entire answer to it.</para>
        /// </summary>
        protected Vector3 Avoid(Vector3 velocity)
        {
            if (avoidanceProbeDistance <= 0f)
                return velocity;

            return ObstacleAvoidance.Deflect(
                transform.position, velocity, Controller != null ? Controller.radius : 0.4f,
                avoidanceProbeDistance, obstacleLayers, avoidanceStrength);
        }

        /// <summary>
        /// Orbits the target: a tangential component to circle with, plus a radial one that holds
        /// the ring. Facing stays on the target throughout — a raptor that turned to face the way
        /// it was running would show the player its flank, and the silhouette is the read.
        /// </summary>
        Vector3 ResolveCircling(float deltaTime)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            float distance = toTarget.magnitude;
            if (distance <= 0.0001f)
                return Vector3.zero;

            Vector3 direction = toTarget / distance;
            FaceTowards(direction, deltaTime);

            Vector3 tangent = Vector3.Cross(Vector3.up, direction) * circleDirection;

            // Clamped, so a raptor twenty metres out sprints in at full speed rather than orbiting
            // a ring it has not reached; and one already on the ring circles cleanly instead of
            // spiralling.
            float radialError = Mathf.Clamp(distance - definition.CircleRadius, -1f, 1f);
            Vector3 heading = (tangent + direction * radialError).normalized;
            Vector3 velocity = heading * (definition.MoveSpeed * definition.CircleSpeedFraction * actor.StatusMoveSpeedMultiplier);

            // Circling is where avoidance matters most: the orbit is a fixed geometric path that
            // takes no notice of the room, so without this a raptor working the player's flank
            // walks straight into whatever crate happens to be on the circle.
            return Avoid(velocity);
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

            // A fan draws one footprint per spine, because the gaps between them are the answer.
            // One wide wedge would say "there is no way through", which would be a lie.
            //
            // For a ranged move the shape being drawn is the lane the shot will sweep, authored on
            // the attack asset. A projectile takes its own collision radius from the RangedProfile
            // and never reads Hitbox, so that field is free to describe the warning instead — which
            // is the only description worth drawing on the floor.
            var move = CurrentMove as EnemyMove;
            if (move != null && move.IsRanged && move.ProjectileCount > 1)
            {
                telegraph.ShowFan(
                    asset, HitboxOrigin, transform.forward,
                    move.ProjectileSpreadDegrees, move.ProjectileCount, attacks.WindupProgress, FeetY);
                return;
            }

            telegraph.Show(asset, HitboxOrigin, transform.forward, attacks.WindupProgress, FeetY);
        }

        /// <summary>
        /// Resolves the live hitbox. Public shape so a subclass can trigger it off-schedule — a
        /// charge damages whatever it runs into for the whole of its travel, not for an active
        /// window measured in frames.
        /// </summary>
        protected void QueryHitbox() => QueryHitbox(attacks.Current, transform.forward, ActiveHitLayers);

        /// <summary>
        /// What this enemy's live attack can hit. Normally just the player.
        ///
        /// <para>Overridable because ENEMIES_BIOME1.md § 2.2 makes Cerashorn's charge damage and
        /// knock down <em>other enemies</em> — friendly fire is not a bug there, it is the reward
        /// for baiting a charge, and "players discovering this is a designed delight".</para>
        /// </summary>
        protected virtual LayerMask ActiveHitLayers => hittableLayers;

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

                // Same rule the player follows: a zoned body is only hittable on its zones, so a
                // Cerashorn charge cannot bypass an Ambershell's plating through the root capsule.
                if (HitZones.IsNonZoneColliderOfZonedBody(collider))
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

            FireProjectiles(attack);
        }

        /// <summary>
        /// Launches a move's projectiles at the instant its wind-up completes.
        ///
        /// <para>Fired from <c>ActiveStarted</c> rather than on a timer, which is what makes the
        /// telegraph honest: an interrupt landing during the wind-up means the shot is never
        /// created at all. Staggering a Sailspit mid-gulp eats the glob.</para>
        ///
        /// <para>The fan is spread evenly end to end, and the <em>gaps</em> are the point —
        /// ENEMIES_BIOME1.md § 2.3 makes them dash-through lanes, so the answer to a spine fan is
        /// to read a gap rather than to outrun the cone.</para>
        /// </summary>
        protected virtual void FireProjectiles(IAttackDefinition attack)
        {
            var move = CurrentMove as EnemyMove;
            if (move == null || !move.IsRanged || target == null)
                return;

            // Projectiles fly planar, so the launch height must be the target's centre or every
            // shot sails over its head. Taken from the target rather than a fixed muzzle offset,
            // so it stays correct whatever the two capsules' heights are.
            Vector3 origin = new Vector3(transform.position.x, target.position.y, transform.position.z);
            Vector3 toTarget = target.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                toTarget = transform.forward;

            float range = toTarget.magnitude;
            Vector3 aim = toTarget.normalized;
            int count = Mathf.Max(1, move.ProjectileCount);
            RangedProfile profile = move.ResolveProfile(definition.Ranged);

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float angle = Mathf.Lerp(-move.ProjectileSpreadDegrees * 0.5f, move.ProjectileSpreadDegrees * 0.5f, t);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aim;

                Projectile shot = Instantiate(move.ProjectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up));
                shot.Launch(
                    origin, direction, attack, resolver, transform,
                    profile, hittableLayers, projectileBlockingLayers, range);
            }

            GameLog.Info(LogCategory.Enemy,
                $"fire {definition.Id}  {move.Id}  {count} shot(s) over {move.ProjectileSpreadDegrees:0}deg  " +
                $"speed {profile.ProjectileSpeed:0.##}m/s");
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
