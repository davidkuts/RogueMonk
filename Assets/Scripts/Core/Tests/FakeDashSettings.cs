using System;
using Game.Core.Locomotion;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>Mutable stand-in for the dash settings ScriptableObject so tests stay asset-free.</summary>
    internal sealed class FakeDashSettings : IDashSettings
    {
        public float DistanceMeters { get; set; } = 4f;
        public float DurationSeconds { get; set; } = 0.18f;
        public float IFrameFraction { get; set; } = 0.85f;
        public int MaxCharges { get; set; } = 2;
        public float RechargeSeconds { get; set; } = 2.5f;
        public float BufferSeconds { get; set; } = 0.15f;
        public float ExitSpeedFraction { get; set; } = 1f;

        /// <summary>Defaults to linear travel; override to test curve shapes.</summary>
        public Func<float, float> TravelCurve { get; set; } = t => t;

        public float EvaluateTravel(float normalizedTime) => TravelCurve(Mathf.Clamp01(normalizedTime));
    }
}
