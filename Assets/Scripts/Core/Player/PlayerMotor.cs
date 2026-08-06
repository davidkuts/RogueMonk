using Game.Core.Locomotion;
using UnityEngine;

namespace Game.Core.Player
{
    /// <summary>
    /// MonoBehaviour adapter: forwards input + deltaTime into <see cref="PlayerLocomotion"/>
    /// and applies the resulting velocity through the CharacterController, whose
    /// collide-and-slide gives wall sliding for free. No movement logic lives here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] PlayerMovementSettings settings;
        [SerializeField] PlayerInputReader input;

        CharacterController controller;
        float verticalSpeed;

        /// <summary>The simulation this adapter drives — read by the camera rig and, later, combat.</summary>
        public PlayerLocomotion Locomotion { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (input == null)
                input = GetComponent<PlayerInputReader>();

            if (settings == null)
            {
                Debug.LogError($"{nameof(PlayerMotor)} on '{name}' has no {nameof(PlayerMovementSettings)} assigned.", this);
                enabled = false;
                return;
            }

            Locomotion = new PlayerLocomotion(settings);
            Locomotion.SetFacing(transform.forward);
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            Vector2 moveAxis = input != null ? input.MoveAxis : Vector2.zero;

            Locomotion.Tick(moveAxis, deltaTime);

            verticalSpeed = controller.isGrounded
                ? -settings.GroundedStickSpeed
                : verticalSpeed + settings.Gravity * deltaTime;

            Vector3 motion = Locomotion.Velocity;
            motion.y = verticalSpeed;
            controller.Move(motion * deltaTime);

            transform.rotation = Quaternion.LookRotation(Locomotion.Facing, Vector3.up);
        }
    }
}
