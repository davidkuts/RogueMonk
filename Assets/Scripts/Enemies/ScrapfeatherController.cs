using System.Collections.Generic;
using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// One bird of the carrion swarm.
    ///
    /// <para>Deliberately <em>not</em> a <see cref="MovesetEnemyController"/>. Scrapfeathers have no
    /// telegraphed attacks at all — ENEMIES_BIOME1.md § 2.4 is explicit that "the swarm is the
    /// attack" — so a moveset brain, an attack state machine, a telegraph presenter and an attack
    /// token would all be machinery with nothing to do, twelve times over. What is left is small:
    /// flock, touch, die.</para>
    ///
    /// <para>Each bird is still a full <see cref="EnemyActor"/>, because that is what the room
    /// runner counts and what the run stats read. One HP means anything at all kills it, which is
    /// the point: a sweeping attack should feel gloriously efficient, and a swarm that took two
    /// hits each would be a chore rather than a flourish.</para>
    ///
    /// <para>They find each other through a static roster rather than a manager object, so a bird
    /// spawns through exactly the same path as every other enemy — the level spawner and the debug
    /// spawner both just instantiate a prefab with an <c>EnemyActor</c> on it, and the flock is
    /// emergent.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ScrapfeatherController : MonoBehaviour
    {
        [SerializeField] EnemyActor actor;

        [SerializeField, Tooltip("Left empty, the player is found by tag at startup.")]
        Transform target;

        [SerializeField, Tooltip("What a contact nibble does. An AttackDefinition so the chip damage is data like everything else.")]
        AttackDefinition nibble;

        [SerializeField, Tooltip("Layers a nibble can land on.")]
        LayerMask hittableLayers;

        [SerializeField, Tooltip("How close a body has to be to be nibbled. Contact, not reach.")]
        float contactRadius = 0.7f;

        [SerializeField, Tooltip("Gap between nibbles from THIS bird. Twelve birds on one cooldown would delete the player instantly.")]
        float nibbleCooldownSeconds = 1.1f;

        [SerializeField, Tooltip("How long a bird must stay in contact before it can bite. Passing through, or dashing past, must never cost health — there is no guaranteed damage in this game.")]
        float contactDwellSeconds = 0.35f;

        [Header("Flocking")]
        [SerializeField] float separation = 1.6f;
        [SerializeField] float cohesion = 0.5f;
        [SerializeField] float alignment = 0.35f;
        [SerializeField] float seek = 1.0f;
        [SerializeField] float neighbourRadius = 3.0f;
        [SerializeField] float separationRadius = 1.0f;

        [SerializeField, Tooltip("How fast the heading turns toward the steering direction. Low values make the carpet drift rather than snap.")]
        float steerResponse = 6f;

        [SerializeField, Tooltip("Environment layers to steer around.")]
        LayerMask obstacleLayers = 1;

        [SerializeField] float gravity = -25f;
        [SerializeField] float groundedStickSpeed = 2f;

        /// <summary>
        /// Every live bird. Static because the flock is emergent — no manager object owns it, so a
        /// bird can be spawned by the level generator, the debug spawner, or by hand, and still
        /// flock with whatever else is out there.
        /// </summary>
        static readonly List<ScrapfeatherController> Roster = new List<ScrapfeatherController>();

        static Vector3[] positionBuffer = new Vector3[32];
        static Vector3[] velocityBuffer = new Vector3[32];
        static int bufferCount;
        static int bufferFrame = -1;

        readonly Collider[] contactResults = new Collider[8];

        CharacterController controller;
        HitResolver resolver;
        Vector3 planarVelocity;
        float verticalSpeed;
        float nibbleCooldown;
        float contactDwell;

        public static int FlockSize => Roster.Count;

        /// <summary>The bird's current planar speed, for tests and diagnostics.</summary>
        public Vector3 PlanarVelocity => planarVelocity;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();

            controller = GetComponent<CharacterController>();
            resolver = new HitResolver();

            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    target = player.transform;
            }
        }

        void OnEnable() => Roster.Add(this);

        void OnDisable() => Roster.Remove(this);

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || !actor.IsAlive)
                return;

            RefreshBuffers();

            if (nibbleCooldown > 0f)
                nibbleCooldown -= deltaTime;

            if (!actor.IsStaggered)
            {
                Steer(deltaTime);
                TryNibble(deltaTime);
            }
            else
            {
                planarVelocity = Vector3.zero;
                contactDwell = 0f;
            }

            verticalSpeed = controller.isGrounded ? -groundedStickSpeed : verticalSpeed + gravity * deltaTime;
            Vector3 motion = planarVelocity * deltaTime;
            motion.y = verticalSpeed * deltaTime;
            controller.Move(motion);

            if (planarVelocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
        }

        /// <summary>
        /// Snapshots every bird's position and velocity once per frame, shared by the whole flock.
        ///
        /// <para>Without this each bird would walk the roster itself and read positions its
        /// neighbours had already changed this frame — the flock would behave differently depending
        /// on component order, and twelve birds would do twelve full passes instead of one. It also
        /// keeps the whole thing allocation-free, which is the part that matters at count.</para>
        /// </summary>
        static void RefreshBuffers()
        {
            if (bufferFrame == Time.frameCount)
                return;

            bufferFrame = Time.frameCount;
            bufferCount = Roster.Count;

            if (positionBuffer.Length < bufferCount)
            {
                positionBuffer = new Vector3[Mathf.NextPowerOfTwo(bufferCount)];
                velocityBuffer = new Vector3[positionBuffer.Length];
            }

            for (int i = 0; i < bufferCount; i++)
            {
                positionBuffer[i] = Roster[i].transform.position;
                velocityBuffer[i] = Roster[i].planarVelocity;
            }
        }

        void Steer(float deltaTime)
        {
            int index = Roster.IndexOf(this);
            if (index < 0 || target == null)
                return;

            var weights = new BoidsWeights
            {
                Separation = separation,
                Cohesion = cohesion,
                Alignment = alignment,
                Seek = seek,
                NeighbourRadius = neighbourRadius,
                SeparationRadius = separationRadius,
            };

            Vector3 desired = BoidsSteering.Steer(
                index, positionBuffer, velocityBuffer, bufferCount, target.position, weights);

            float speed = actor.Definition.MoveSpeed * actor.StatusMoveSpeedMultiplier;

            // Eased rather than snapped, so the carpet flows instead of twitching. Twelve bodies
            // all snapping to a new heading on the same frame reads as a glitch, not a flock.
            // Steered around obstacles like everything else: a carpet of birds funnelling into a
            // crate instead of around it is the same clumsiness a raptor showed, twelve times over.
            desired = ObstacleAvoidance.Deflect(
                transform.position, desired, controller.radius, 1.4f, obstacleLayers, 0.85f);

            planarVelocity = Vector3.Lerp(
                planarVelocity, desired * speed, 1f - Mathf.Exp(-steerResponse * deltaTime));
        }

        /// <summary>
        /// Chip damage on sustained contact. No telegraph, because there is no attack to telegraph —
        /// the threat is standing where they are, which is what makes them a space problem rather
        /// than a damage problem.
        ///
        /// <para><b>It requires a dwell, and that is a rule rather than a tuning value.</b> Touching
        /// a bird for a single frame — brushing past one, or landing a dash beside it — used to cost
        /// health unavoidably, and there is no guaranteed damage in this game: skill has to be
        /// rewarded, so a player who reads the swarm and moves through it cleanly must be able to
        /// take nothing. Standing in the swarm still costs, which is the whole point of the
        /// archetype; passing through it no longer does.</para>
        /// </summary>
        void TryNibble(float deltaTime)
        {
            if (nibble == null)
            {
                contactDwell = 0f;
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, contactRadius, contactResults, hittableLayers, QueryTriggerInteraction.Collide);

            IDamageable touching = null;
            Collider touchingCollider = null;

            for (int i = 0; i < count; i++)
            {
                Collider collider = contactResults[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                    continue;

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                    continue;

                touching = damageable;
                touchingCollider = collider;
                break;
            }

            if (touching == null)
            {
                // Contact broken. Reset rather than decay, so a player weaving in and out never
                // accumulates a bite across separate passes.
                contactDwell = 0f;
                return;
            }

            contactDwell += deltaTime;

            if (nibbleCooldown > 0f || contactDwell < contactDwellSeconds)
                return;

            Vector3 direction = touchingCollider.transform.position - transform.position;
            HitContext context = HitContext.FromAttack(
                nibble, touching, direction, transform.position, HitZones.Resolve(touchingCollider));

            resolver.Resolve(ref context);
            nibbleCooldown = nibbleCooldownSeconds;
            contactDwell = 0f;
        }

        void OnDestroy()
        {
            Roster.Remove(this);

            // The buffers are indexed by roster position, so a removal mid-frame would otherwise
            // leave every bird after this one steering from a stale neighbour's data.
            bufferFrame = -1;
        }

        /// <summary>Clears the roster. For teardown between rooms and between tests.</summary>
        public static void ResetRoster()
        {
            Roster.Clear();
            bufferFrame = -1;
            GameLog.Debug(LogCategory.Enemy, "scrapfeather roster cleared");
        }
    }
}
