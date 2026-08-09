using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The juvenile ceratopsian: a charge you beat by standing somewhere else.
    ///
    /// <para>Almost all of it is already data. "Cannot steer once committed" is the base
    /// controller's existing rule that facing locks at commit and an attacking body travels on its
    /// lunge velocity — a long lunge over a long active window <em>is</em> a line-locked charge, so
    /// no bespoke movement mode was needed. What genuinely is bespoke is what happens when the
    /// charge meets something: a wall, or another enemy.</para>
    ///
    /// <para>Both of those are the design. ENEMIES_BIOME1.md § 2.2 gives the player three answers —
    /// sidestep and it slams a wall and self-stuns, dash through it for the Split Second, or bait
    /// it into the rest of the room — and the third is explicitly "intended, encouraged, fun".</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CerashornController : MovesetEnemyController
    {
        [Header("Charge")]
        [SerializeField, Tooltip("Move id that counts as the charge. Anything else runs as an ordinary attack.")]
        string chargeMoveId = "LineCharge";

        [SerializeField, Tooltip("Self-stun after running into a wall. ENEMIES_BIOME1.md 2.2 asks for 1.5s — this is the punish window a sidestep earns.")]
        float wallSlamStunSeconds = 1.5f;

        [SerializeField, Tooltip("Layers that count as a wall worth slamming into. Environment only — hitting an enemy must never end the charge.")]
        LayerMask wallLayers = 1;

        [SerializeField, Tooltip("How long anything the charge hits is knocked down for. 0 leaves it merely damaged.")]
        float knockdownSeconds = 1.2f;

        [SerializeField, Tooltip("Layers the CHARGE can hit — deliberately wider than the enemy's usual mask, so it ploughs through its own allies.")]
        LayerMask chargeHitLayers;

        [SerializeField, Tooltip("Fraction of charge speed below which a moving charge counts as having hit a wall. Guards against a glancing scrape ending it.")]
        [Range(0.05f, 0.9f)]
        float wallSlamSpeedFraction = 0.35f;

        Vector3 positionBeforeMove;
        bool slammedThisCharge;

        /// <summary>Whatever the controller excluded before a charge, so it can be restored exactly.</summary>
        LayerMask baseExcludeLayers;
        bool capturedBaseExcludes;

        /// <summary>True while the committed charge is actually travelling.</summary>
        public bool IsCharging =>
            attacks.Phase == AttackPhase.Active && CurrentMove != null && CurrentMove.Id == chargeMoveId;

        /// <summary>True on the frame after a charge ended against a wall. For tests and feedback.</summary>
        public bool SlammedThisCharge => slammedThisCharge;

        // A charge ploughs through everything; anything else this body does hits only the player.
        protected override LayerMask ActiveHitLayers =>
            IsCharging && chargeHitLayers.value != 0 ? chargeHitLayers : base.ActiveHitLayers;

        protected override void Update()
        {
            positionBeforeMove = transform.position;

            // Recomputed from the charge state every frame rather than toggled on start and end
            // events, exactly as PlayerMotor does for the dash. It is self-healing, so a charge cut
            // short by a stagger, a death or a disable can never strand this body permanently
            // phased through everything.
            UpdateChargePhasing();

            base.Update();

            if (IsCharging)
                CheckWallSlam();
        }

        /// <summary>
        /// Lets a committed charge pass through bodies.
        ///
        /// <para>Without this the charge is simply <em>stopped</em> by the first CharacterController
        /// it touches — the player's, or one of its own allies' — because two controllers collide.
        /// That quietly deletes both of § 2.2's payoffs at once: the charge can never reach a wall
        /// to slam into, so sidestepping earns nothing, and it can never plough through the room,
        /// so baiting it into a Swiftjaw pack does nothing either. Measured: a charge with 12.5m of
        /// runway stopped after 4.5m, dead against the player's capsule.</para>
        ///
        /// <para>Walls stay solid, because they are on the environment layer and only bodies are
        /// excluded — which is what leaves the wall slam as the one thing that can end a charge.</para>
        /// </summary>
        void UpdateChargePhasing()
        {
            if (Controller == null)
                return;

            if (!capturedBaseExcludes)
            {
                baseExcludeLayers = Controller.excludeLayers;
                capturedBaseExcludes = true;
            }

            Controller.excludeLayers = IsCharging && chargeHitLayers.value != 0
                ? baseExcludeLayers | chargeHitLayers
                : baseExcludeLayers;
        }

        protected override void OnAttackStarted(IAttackDefinition attack)
        {
            base.OnAttackStarted(attack);

            if (CurrentMove != null && CurrentMove.Id == chargeMoveId)
            {
                slammedThisCharge = false;
                GameLog.Info(LogCategory.Enemy,
                    $"{Definition.Id} commits to a charge - line locked for {attack.ActiveSeconds:0.00}s of travel");
            }
        }

        /// <summary>
        /// Ends the charge against a wall and hands the player the punish window they earned by
        /// stepping aside.
        ///
        /// <para>Detected by <em>displacement</em> rather than by <c>collisionFlags</c>. A
        /// <c>CharacterController</c> reports a side collision for any graze — including brushing
        /// another enemy, which must never end a charge, since ploughing through the room is the
        /// whole point. Measuring how far the body actually moved asks the only question that
        /// matters: did it stop?</para>
        /// </summary>
        void CheckWallSlam()
        {
            if (slammedThisCharge)
                return;

            float expected = ChargeSpeed * Time.deltaTime;
            if (expected <= 0.0001f)
                return;

            float actual = Vector3.Distance(
                new Vector3(positionBeforeMove.x, 0f, positionBeforeMove.z),
                new Vector3(transform.position.x, 0f, transform.position.z));

            if (actual >= expected * wallSlamSpeedFraction)
                return;

            // Confirm there is genuinely something solid ahead before crediting a slam, so a body
            // wedged on a slope or shoved by a knockback does not self-stun for free.
            if (!Physics.Raycast(
                    transform.position, transform.forward, Controller.radius + 0.45f,
                    wallLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            slammedThisCharge = true;

            GameLog.Info(LogCategory.Enemy,
                $"WALL SLAM {Definition.Id} - self-stunned {wallSlamStunSeconds:0.0}s (punish window open)");

            // Goes through the ordinary stagger path, so the interrupt, the colour change, the
            // status and the brain's recovery are the same ones a poise break produces. A bespoke
            // "dazed" state would be a second thing that means the same thing.
            actor.ApplyStagger(wallSlamStunSeconds);
        }

        /// <summary>
        /// Knocks down whatever the charge ran into — including other enemies.
        ///
        /// <para>The friendly fire is the point, not an oversight: § 2.2 wants a baited charge to
        /// carve through the room, and a Cerashorn one-shotting a Scrapfeather or flattening a
        /// Swiftjaw is a reward for positioning. It is also why the charge does not stop on
        /// contact — only a wall ends it.</para>
        /// </summary>
        protected override void OnHitLanded(Collider collider, IDamageable damageable)
        {
            base.OnHitLanded(collider, damageable);

            if (!IsCharging || knockdownSeconds <= 0f)
                return;

            damageable.ApplyStagger(knockdownSeconds);
        }

        /// <summary>Metres per second the current charge is travelling, from its own frame data.</summary>
        float ChargeSpeed
        {
            get
            {
                IAttackDefinition current = attacks.Current;
                if (current == null || CurrentMove == null)
                    return 0f;

                return CurrentMove.LungeDistance / Mathf.Max(0.0001f, current.ActiveSeconds);
            }
        }
    }
}
