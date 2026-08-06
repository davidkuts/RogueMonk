using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// All player movement + camera-follow tuning. Nothing here may be hardcoded in
    /// behaviour code (CLAUDE.md hard rule 2).
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Player Movement Settings", fileName = "PlayerMovementSettings")]
    public sealed class PlayerMovementSettings : ScriptableObject, ILocomotionSettings, ICameraLookAheadSettings
    {
        [Header("Ground movement")]
        [SerializeField, Tooltip("Top planar speed in m/s.")]
        float maxSpeed = 6f;
        [SerializeField, Tooltip("m/s² applied while input is held. High = snappy start.")]
        float acceleration = 70f;
        [SerializeField, Tooltip("m/s² applied on input release. High = snappy stop.")]
        float deceleration = 90f;
        [SerializeField, Tooltip("How fast the capsule rotates toward the movement direction.")]
        float turnSpeedDegPerSec = 1080f;

        [Header("Input conditioning")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Radial deadzone on the left stick.")]
        float inputDeadzone = 0.15f;
        [SerializeField, Range(1f, 3f), Tooltip("1 = linear. >1 = finer control near centre.")]
        float inputResponseExponent = 1.4f;

        [Header("Gravity (CharacterController grounding only)")]
        [SerializeField, Tooltip("m/s² downward while airborne.")]
        float gravity = -25f;
        [SerializeField, Tooltip("Constant downward speed applied while grounded to keep isGrounded true.")]
        float groundedStickSpeed = 2f;

        [Header("Camera look-ahead")]
        [SerializeField, Tooltip("Metres the camera leads the player at full speed. Larger = more anticipation, more screen swing.")]
        float lookAheadDistance = 0.8f;
        [SerializeField, Tooltip("SmoothDamp time for the look-ahead offset. Larger = lazier lead, gentler on the eyes.")]
        float lookAheadSmoothTime = 0.55f;

        public float MaxSpeed => maxSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float TurnSpeedDegPerSec => turnSpeedDegPerSec;
        public float InputDeadzone => inputDeadzone;
        public float InputResponseExponent => inputResponseExponent;
        public float Gravity => gravity;
        public float GroundedStickSpeed => groundedStickSpeed;
        public float LookAheadDistance => lookAheadDistance;
        public float LookAheadSmoothTime => lookAheadSmoothTime;
    }
}
