using Game.Core.Input;
using Game.Core.Locomotion;
using UnityEngine;

namespace Game.Core.Player
{
    /// <summary>
    /// MonoBehaviour adapter: forwards input + deltaTime into the locomotion and dash
    /// simulations and applies the resulting motion through the CharacterController, whose
    /// collide-and-slide gives wall sliding — and dash-into-wall stopping — for free.
    /// No movement logic lives here; this only arbitrates which simulation owns the frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] PlayerMovementSettings settings;
        [SerializeField] DashSettings dashSettings;
        [SerializeField] PlayerInputReader input;

        [SerializeField, Tooltip("Layers the dash passes straight through — enemies. Walls are deliberately not included: a dash that phased through geometry would break room containment.")]
        LayerMask dashPhasesThroughLayers;

        CharacterController controller;
        readonly InputBuffer dashBuffer = new InputBuffer();
        float verticalSpeed;

        /// <summary>Combat's veto on movement and dashing. Null when nothing restricts the player.</summary>
        IPlayerActionState actions;

        /// <summary>Whatever the controller excluded before the dash started, so it can be restored exactly.</summary>
        LayerMask baseExcludeLayers;

        /// <summary>
        /// Slowing fields the player is standing in — stall zones, and whatever later biomes add.
        /// The rule (slowest wins, never the product) lives in the accumulator so it is testable
        /// without a scene.
        /// </summary>
        SpeedFieldAccumulator speedFields;

        /// <summary>Control loss and throw from a heavy hit. Integrated like enemy knockback.</summary>
        float staggerRemaining;
        Vector3 staggerVelocity;

        [SerializeField, Tooltip("How fast a heavy-hit throw bleeds off. Higher stops the player sooner. Matches the enemy knockback damping so both read alike.")]
        float staggerDamping = 8f;

        /// <summary>The walking simulation — read by the camera rig and, later, combat.</summary>
        public PlayerLocomotion Locomotion { get; private set; }

        /// <summary>The dash simulation — owns charges, i-frames and the perfect-dodge refund.</summary>
        public PlayerDash Dash { get; private set; }

        /// <summary>
        /// True while the dash is protecting the player — i-frames or the trailing dodge grace.
        /// Combat asks this before applying damage, so it must be the full window: the grace has to
        /// actually stop the hit, or a "perfect dodge" would be credited for a blow that landed.
        /// </summary>
        public bool IsInvulnerable => Dash != null && Dash.IsProtected;

        /// <summary>Slowest field the player is standing in this frame. 1 when standing on clean ground.</summary>
        public float ExternalSpeedMultiplier => speedFields.Current;

        /// <summary>True while a heavy hit has taken control away. Input is ignored, momentum carries.</summary>
        public bool IsStaggered => staggerRemaining > 0f;

        public float StaggerRemaining => staggerRemaining;

        /// <summary>
        /// Takes control away briefly and throws the player, as the reaction to a heavy hit.
        ///
        /// <para>Some attacks need to land like they look. A thirty-metre committed charge that
        /// costs the same as a nibble reads as weightless, and the difference has to be something
        /// the player <em>feels</em> rather than reads off a number — DESIGN.md already learned this
        /// from the empowered strike, which was deleted precisely because it changed nothing
        /// perceptible.</para>
        ///
        /// <para>Deliberately shorter than it could be. The player's mandatory post-hit
        /// invulnerability is 0.5 s, so a stagger of about that length ends as the i-frames do —
        /// helpless and untouchable overlap, and the stagger never becomes the reason a second
        /// enemy lands a free hit. Stacking helplessness on top of vulnerability is how a heavy hit
        /// turns into a death sentence in a crowded room.</para>
        ///
        /// <para>A dash cannot be started while staggered, but one already running is not cancelled:
        /// the i-frames the player already earned are theirs.</para>
        /// </summary>
        public void ApplyHitStagger(float seconds, Vector3 knockback)
        {
            if (seconds > 0f)
                staggerRemaining = Mathf.Max(staggerRemaining, seconds);

            knockback.y = 0f;
            if (knockback.sqrMagnitude > 0.0001f)
                staggerVelocity += knockback;
        }

        /// <summary>
        /// Reports that the player is standing in something that slows them — a Sailspit stall
        /// zone, and whatever later biomes add.
        ///
        /// <para><b>Zones do not stack.</b> The slowest one wins rather than the product, which is
        /// why this takes a minimum instead of multiplying. Two overlapping puddles multiplying to
        /// a quarter speed would make a pair of Sailspits an accidental hard lock, and
        /// ENEMIES_BIOME1.md § 2.3 is explicit that overlapping zones must not stack their slow.</para>
        ///
        /// <para>It slows <em>movement only</em>. Attacks, the dash and the vortex all read their
        /// own timings from frame data and never touch this — a stall zone shapes the arena, it
        /// does not stun.</para>
        /// </summary>
        public void ApplyExternalSpeedMultiplier(float multiplier) => speedFields.Report(multiplier);

        /// <summary>
        /// Moves the body somewhere instantly, for the Stored Rewind Stopgap.
        ///
        /// <para>The CharacterController caches its own position and overwrites the transform on
        /// its next Move, so writing <c>transform.position</c> alone gets silently undone within a
        /// frame. Disabling it across the write is the documented way to relocate one, and it is
        /// why this is a method here rather than a line at the call site.</para>
        ///
        /// <para>Vertical speed is cleared too: arriving with the accumulated fall of wherever the
        /// player was would slam them into the floor on landing.</para>
        /// </summary>
        public void Teleport(Vector3 position)
        {
            if (controller != null)
            {
                bool wasEnabled = controller.enabled;
                controller.enabled = false;
                transform.position = position;
                controller.enabled = wasEnabled;
            }
            else
            {
                transform.position = position;
            }

            verticalSpeed = 0f;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            baseExcludeLayers = controller.excludeLayers;

            if (input == null)
                input = GetComponent<PlayerInputReader>();

            // Resolved by interface so Game.Core never has to reference Game.Combat.
            actions = GetComponent<IPlayerActionState>();

            if (settings == null || dashSettings == null)
            {
                Debug.LogError($"{nameof(PlayerMotor)} on '{name}' is missing movement or dash settings.", this);
                enabled = false;
                return;
            }

            Locomotion = new PlayerLocomotion(settings);
            Locomotion.SetFacing(transform.forward);
            Dash = new PlayerDash(dashSettings);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            Vector2 moveAxis = input != null ? input.MoveAxis : Vector2.zero;

            if (input != null && input.DashPressedThisFrame)
                dashBuffer.Press();
            dashBuffer.Tick(deltaTime, dashSettings.BufferSeconds);

            if (staggerRemaining > 0f)
                staggerRemaining = Mathf.Max(0f, staggerRemaining - deltaTime);

            // An attack's wind-up and active frames are committed; recovery is dash-cancellable.
            // A stagger also blocks *starting* a dash — but never cancels one already running,
            // because those i-frames were earned before the hit landed.
            bool dashAllowed = (actions == null || actions.AllowsDash) && !IsStaggered;
            if (dashAllowed && dashBuffer.HasInput && Dash.CanStart && Dash.TryStart(ResolveDashDirection(moveAxis)))
            {
                dashBuffer.Clear();
                Locomotion.SetFacing(Dash.Direction);
                if (actions != null)
                    actions.CancelForDash();
            }

            // Attacks slow the player and may take ownership of facing.
            float speedMultiplier = actions != null ? actions.MoveSpeedMultiplier : 1f;
            bool allowTurning = actions == null || actions.AllowsTurning;

            // Ground that slows, on top of whatever the current action imposes. Kept separate
            // from the action multiplier so the two compose without either owning the other.
            speedMultiplier *= speedFields.Current;

            // A staggered player has no steering at all. The throw still carries them, which is
            // what makes it read as being hit rather than as the game freezing.
            if (IsStaggered)
            {
                moveAxis = Vector2.zero;
                speedMultiplier = 0f;
            }

            // Dash.Tick always runs — it owns charge recharge whether or not a dash is live.
            bool wasDashing = Dash.IsDashing;
            Vector3 dashStep = Dash.Tick(deltaTime);
            Vector3 planarMotion;

            if (wasDashing)
            {
                planarMotion = dashStep;
                if (!Dash.IsDashing)
                    Locomotion.SetVelocity(Dash.Direction * (settings.MaxSpeed * dashSettings.ExitSpeedFraction));
            }
            else
            {
                Locomotion.Tick(moveAxis, deltaTime, speedMultiplier, allowTurning);
                planarMotion = Locomotion.Velocity * deltaTime;
            }

            // Recomputed every frame from the dash state rather than toggled on the start and
            // end events: it is self-healing, so a cancelled or interrupted dash can never
            // strand the player permanently phased through enemies.
            controller.excludeLayers = Dash.IsDashing
                ? baseExcludeLayers | dashPhasesThroughLayers
                : baseExcludeLayers;

            verticalSpeed = controller.isGrounded
                ? -settings.GroundedStickSpeed
                : verticalSpeed + settings.Gravity * deltaTime;

            // The heavy-hit throw, integrated on top of whatever movement resolved. Move first and
            // decay exponentially — the same fix EnemyActor's knockback needed, for the same
            // reason: Lerp-then-move clamps at 1 and a long frame silently eats the whole impulse.
            if (staggerVelocity.sqrMagnitude > 0.0001f)
            {
                planarMotion += staggerVelocity * deltaTime;
                staggerVelocity *= Mathf.Exp(-staggerDamping * deltaTime);
            }
            else
            {
                staggerVelocity = Vector3.zero;
            }

            planarMotion.y = verticalSpeed * deltaTime;
            controller.Move(planarMotion);

            transform.rotation = Quaternion.LookRotation(Locomotion.Facing, Vector3.up);
        }

        /// <summary>
        /// Closes the frame's slowing-field accumulator. Runs after every Update, so a zone that
        /// reported itself this frame is honoured next frame and a zone the player has left stops
        /// applying immediately rather than sticking.
        /// </summary>
        void LateUpdate() => speedFields.EndFrame();

        /// <summary>Dash goes where the stick points, or straight ahead when the stick is neutral.</summary>
        Vector3 ResolveDashDirection(Vector2 moveAxis)
        {
            Vector2 conditioned = InputCurve.Condition(
                moveAxis, settings.InputDeadzone, settings.InputResponseExponent);

            return conditioned.sqrMagnitude > 0f
                ? new Vector3(conditioned.x, 0f, conditioned.y).normalized
                : Locomotion.Facing;
        }
    }
}
