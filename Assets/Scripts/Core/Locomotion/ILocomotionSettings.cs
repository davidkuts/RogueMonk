namespace Game.Core.Locomotion
{
    /// <summary>
    /// Tuning contract consumed by <see cref="PlayerLocomotion"/>. Implemented by a
    /// ScriptableObject at runtime and by fakes in tests, so the simulation never
    /// depends on an asset type.
    /// </summary>
    public interface ILocomotionSettings
    {
        float MaxSpeed { get; }
        float Acceleration { get; }
        float Deceleration { get; }
        float TurnSpeedDegPerSec { get; }
        float InputDeadzone { get; }
        float InputResponseExponent { get; }
    }

    /// <summary>Tuning contract consumed by <see cref="LookAheadTracker"/>.</summary>
    public interface ICameraLookAheadSettings
    {
        float LookAheadDistance { get; }
        float LookAheadSmoothTime { get; }
    }
}
