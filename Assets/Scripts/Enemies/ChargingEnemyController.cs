using Game.Combat;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Shared behaviour for anything that commits to a straight line and runs.
    ///
    /// <para>Two enemies in this biome charge, and they charge identically — Cerashorn's line charge
    /// and Ambershell's rolling charge are the same physics with the same three answers (sidestep,
    /// dash through, bait it into the room). What differs is only <em>what happens when it hits a
    /// wall</em>: one self-stuns, the other cracks its own plating open. That is the one hook.</para>
    ///
    /// <para>Extracted rather than copied because the charge is where the two real bugs of this
    /// milestone lived — body-blocking and a hitbox too thin to catch anything short — and a second
    /// copy would be a second place for them to come back.</para>
    /// </summary>
    public abstract class ChargingEnemyController : MovesetEnemyController
    {
        [Header("Charge")]
        [SerializeField, Tooltip("Move id that counts as the charge. Anything else runs as an ordinary attack.")]
        protected string chargeMoveId = "Charge";

        [SerializeField, Tooltip("Layers that count as a wall worth slamming into. Environment only — hitting an enemy must never end a charge.")]
        protected LayerMask wallLayers = 1;

        [SerializeField, Tooltip("How long anything the charge hits is knocked down for. 0 leaves it merely damaged.")]
        protected float knockdownSeconds = 1.2f;

        [SerializeField, Tooltip("Layers the CHARGE can hit — deliberately wider than the usual mask, so it ploughs through its own allies.")]
        protected LayerMask chargeHitLayers;

        [SerializeField, Range(0.05f, 0.9f), Tooltip("Fraction of charge speed below which a moving charge counts as stopped. Guards against a glancing scrape ending it.")]
        protected float wallSlamSpeedFraction = 0.35f;

        [SerializeField, Tooltip("Degrees per second the charge may steer toward the target while running. 0 is a pure straight line. A small value tracks a walking player but loses a committed sidestep — the Minotaur rule.")]
        protected float chargeTurnDegPerSec;

        Vector3 positionBeforeMove;
        Vector3 lockedChargeDirection;
        LayerMask baseExcludeLayers;
        bool capturedBaseExcludes;
        bool slammedThisCharge;

        /// <summary>True while the committed charge is actually travelling.</summary>
        public bool IsCharging =>
            attacks.Phase == AttackPhase.Active && CurrentMove != null && CurrentMove.Id == chargeMoveId;

        /// <summary>True once the current charge has ended against a wall. For tests and feedback.</summary>
        public bool SlammedThisCharge => slammedThisCharge;

        // A charge ploughs through everything; anything else this body does hits only its usual mask.
        protected override LayerMask ActiveHitLayers =>
            IsCharging && chargeHitLayers.value != 0 ? chargeHitLayers : base.ActiveHitLayers;

        protected override void Update()
        {
            positionBeforeMove = transform.position;
            UpdateChargePhasing();
            base.Update();

            if (IsCharging)
            {
                SteerCharge();
                CheckWallSlam();
            }
        }

        /// <summary>
        /// Lets a running charge bend slowly toward the target.
        ///
        /// <para>The Minotaur rule: it locks on and commits, and it keeps coming until something
        /// stops it — but the lock is not perfect. A slow turn rate tracks a player who is merely
        /// walking, and loses one who commits to a hard cut across it. That is what makes the
        /// answer a <em>decision</em> ("break hard, or dash through it") rather than a reflex, and
        /// it is why the turn rate is small rather than zero.</para>
        ///
        /// <para>The locked direction is updated as it turns, so the wall-slam test keeps measuring
        /// progress along the line the body is <em>currently</em> committed to.</para>
        /// </summary>
        void SteerCharge()
        {
            if (chargeTurnDegPerSec <= 0f || target == null)
                return;

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return;

            float maxRadians = chargeTurnDegPerSec * Mathf.Deg2Rad * Time.deltaTime;
            Vector3 steered = Vector3.RotateTowards(lockedChargeDirection, toTarget.normalized, maxRadians, 0f);
            steered.y = 0f;
            if (steered.sqrMagnitude <= 0.0001f)
                return;

            lockedChargeDirection = steered.normalized;
            transform.rotation = Quaternion.LookRotation(lockedChargeDirection, Vector3.up);

            // Redirect the travel itself, or the body would face a new way while still sliding
            // along the old one.
            SetLungeVelocity(lockedChargeDirection * ChargeSpeed);
        }

        protected override void OnAttackStarted(IAttackDefinition attack)
        {
            base.OnAttackStarted(attack);

            if (CurrentMove != null && CurrentMove.Id == chargeMoveId)
            {
                slammedThisCharge = false;

                // The line, captured once. Progress is measured against this rather than against
                // facing, so a body being deflected by a wall cannot quietly redefine "forward".
                lockedChargeDirection = transform.forward;
                lockedChargeDirection.y = 0f;
                lockedChargeDirection = lockedChargeDirection.sqrMagnitude > 0.0001f
                    ? lockedChargeDirection.normalized
                    : Vector3.forward;

                GameLog.Info(LogCategory.Enemy,
                    $"{Definition.Id} commits to a charge - line locked for {attack.ActiveSeconds:0.00}s of travel");
            }
        }

        /// <summary>
        /// Lets a committed charge pass through bodies.
        ///
        /// <para>Without this the charge is simply <em>stopped</em> by the first CharacterController
        /// it touches, because two controllers collide. Measured on Cerashorn: a charge with 12.5 m
        /// of runway stopped after 4.5 m against the player's capsule, which silently deleted both
        /// of the design's payoffs — it could never reach a wall, and it could never plough the
        /// room.</para>
        ///
        /// <para>Recomputed from the charge state every frame rather than toggled on events, exactly
        /// as <c>PlayerMotor</c> does for the dash. It is self-healing, so a charge cut short by a
        /// stagger, a death or a disable can never strand the body permanently phased.</para>
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

        /// <summary>
        /// Ends the charge against a wall.
        ///
        /// <para>Measured as <em>progress along the locked line</em>, not as total displacement.
        /// That distinction is the whole fix: a <c>CharacterController</c> meeting a wall at a
        /// shallow angle <em>slides</em> along it, so total movement stays high and a displacement
        /// test never fires — the charger just scrapes down the wall looking confused, which is
        /// exactly what a playtest reported. Projecting onto the committed direction collapses to
        /// near zero the moment it stops going the way it promised, whatever angle it hit at.</para>
        ///
        /// <para>Also deliberately not <c>collisionFlags</c>: a controller reports a side collision
        /// for any graze, including brushing another enemy, and ploughing through the room is the
        /// point of the move.</para>
        /// </summary>
        void CheckWallSlam()
        {
            if (slammedThisCharge)
                return;

            float expected = ChargeSpeed * Time.deltaTime;
            if (expected <= 0.0001f)
                return;

            Vector3 travelled = transform.position - positionBeforeMove;
            travelled.y = 0f;

            float progress = Vector3.Dot(travelled, lockedChargeDirection);

            if (progress >= expected * wallSlamSpeedFraction)
                return;

            // Confirm something solid is genuinely ahead before crediting a slam, so a body wedged
            // on a slope or shoved by a knockback does not trigger it for free.
            //
            // A SPHERE cast, along the locked line: a thin ray from the centre misses a wall the
            // body is already shouldered against at an angle, which is the case that matters most.
            if (!Physics.SphereCast(
                    transform.position, Controller.radius * 0.9f, lockedChargeDirection,
                    out RaycastHit _, 0.6f, wallLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            slammedThisCharge = true;
            OnWallSlam();
        }

        /// <summary>
        /// What this creature does when its charge meets a wall. The one thing that differs between
        /// a Cerashorn and an Ambershell.
        /// </summary>
        protected abstract void OnWallSlam();

        /// <summary>
        /// Knocks down whatever the charge ran into — including other enemies, which is the design
        /// rather than an oversight, and why the charge does not stop on contact.
        /// </summary>
        protected override void OnHitLanded(Collider collider, IDamageable damageable)
        {
            base.OnHitLanded(collider, damageable);

            if (!IsCharging || knockdownSeconds <= 0f)
                return;

            damageable.ApplyStagger(knockdownSeconds);
        }

        /// <summary>Metres per second the current charge is travelling, from its own frame data.</summary>
        protected float ChargeSpeed
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
