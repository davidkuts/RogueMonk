using Game.Core.Locomotion;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class InputCurveTests
    {
        const float Tolerance = 1e-4f;

        [Test]
        public void InsideDeadzone_ReturnsZero()
        {
            Vector2 result = InputCurve.Condition(new Vector2(0.1f, 0f), 0.15f, 1f);
            Assert.That(result, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Deadzone_IsRadial_NotPerAxis()
        {
            // Each axis alone is inside the deadzone, but the vector magnitude (0.198) is outside.
            Vector2 result = InputCurve.Condition(new Vector2(0.14f, 0.14f), 0.15f, 1f);
            Assert.That(result, Is.Not.EqualTo(Vector2.zero));
        }

        [Test]
        public void FullDeflection_ReturnsUnitMagnitude()
        {
            Vector2 result = InputCurve.Condition(Vector2.right, 0.15f, 1.4f);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void OverDeflection_IsClampedToUnitMagnitude()
        {
            // Diagonal WASD input has magnitude 1.414 before conditioning.
            Vector2 result = InputCurve.Condition(new Vector2(1f, 1f), 0.15f, 1f);
            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Direction_IsPreserved()
        {
            Vector2 raw = new Vector2(0.6f, -0.8f);
            Vector2 result = InputCurve.Condition(raw, 0.15f, 1.4f);
            Assert.That(Vector2.Angle(raw, result), Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void JustOutsideDeadzone_IsNearZeroMagnitude()
        {
            Vector2 result = InputCurve.Condition(new Vector2(0.1501f, 0f), 0.15f, 1f);
            Assert.That(result.magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void ResponseExponent_AboveOne_SoftensMidRange()
        {
            const float halfway = 0.575f; // rescales to 0.5 with a 0.15 deadzone
            float linear = InputCurve.Condition(new Vector2(halfway, 0f), 0.15f, 1f).magnitude;
            float curved = InputCurve.Condition(new Vector2(halfway, 0f), 0.15f, 2f).magnitude;
            Assert.That(curved, Is.LessThan(linear));
        }

        [Test]
        public void ZeroInput_ReturnsZero()
        {
            Assert.That(InputCurve.Condition(Vector2.zero, 0.15f, 1.4f), Is.EqualTo(Vector2.zero));
        }
    }
}
