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

        CharacterController controller;
        readonly InputBuffer dashBuffer = new InputBuffer();
        float verticalSpeed;

        /// <summary>The walking simulation — read by the camera rig and, later, combat.</summary>
        public PlayerLocomotion Locomotion { get; private set; }

        /// <summary>The dash simulation — owns charges, i-frames and the perfect-dodge refund.</summary>
        public PlayerDash Dash { get; private set; }

        /// <summary>True while dash i-frames are live. Combat asks this before applying damage.</summary>
        public bool IsInvulnerable => Dash != null && Dash.IsInvulnerable;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (input == null)
                input = GetComponent<PlayerInputReader>();

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

            if (dashBuffer.HasInput && Dash.CanStart && Dash.TryStart(ResolveDashDirection(moveAxis)))
            {
                dashBuffer.Clear();
                Locomotion.SetFacing(Dash.Direction);
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
                Locomotion.Tick(moveAxis, deltaTime);
                planarMotion = Locomotion.Velocity * deltaTime;
            }

            verticalSpeed = controller.isGrounded
                ? -settings.GroundedStickSpeed
                : verticalSpeed + settings.Gravity * deltaTime;

            planarMotion.y = verticalSpeed * deltaTime;
            controller.Move(planarMotion);

            transform.rotation = Quaternion.LookRotation(Locomotion.Facing, Vector3.up);
        }

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
