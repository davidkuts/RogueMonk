using Game.Core.Locomotion;

namespace Game.Core.Tests
{
    /// <summary>Mutable stand-in for the settings ScriptableObject so tests stay asset-free.</summary>
    internal sealed class FakeMovementSettings : ILocomotionSettings, ICameraLookAheadSettings
    {
        public float MaxSpeed { get; set; } = 6f;
        public float Acceleration { get; set; } = 60f;
        public float Deceleration { get; set; } = 60f;
        public float TurnSpeedDegPerSec { get; set; } = 720f;
        public float InputDeadzone { get; set; } = 0.15f;
        public float InputResponseExponent { get; set; } = 1f;
        public float LookAheadDistance { get; set; } = 1.25f;
        public float LookAheadSmoothTime { get; set; } = 0.2f;
    }
}
