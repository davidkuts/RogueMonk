using Game.Core.Locomotion;
using UnityEngine;

namespace Game.Core.Player
{
    /// <summary>
    /// The transform the gameplay vcam follows: the player position plus a damped
    /// look-ahead offset. Kept separate from the player so Cinemachine damping and
    /// look-ahead damping stay independently tunable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollowTarget : MonoBehaviour
    {
        [SerializeField] PlayerMovementSettings settings;
        [SerializeField] PlayerMotor motor;

        LookAheadTracker tracker;

        void Awake()
        {
            if (motor == null)
                motor = GetComponentInParent<PlayerMotor>();

            if (settings == null || motor == null)
            {
                Debug.LogError($"{nameof(CameraFollowTarget)} on '{name}' is missing settings or a {nameof(PlayerMotor)}.", this);
                enabled = false;
                return;
            }

            tracker = new LookAheadTracker(settings);
        }

        void LateUpdate()
        {
            PlayerLocomotion locomotion = motor.Locomotion;
            if (locomotion == null)
                return;

            tracker.Tick(locomotion.NormalizedVelocity, Time.deltaTime);
            transform.position = motor.transform.position + tracker.Offset;
        }
    }
}
