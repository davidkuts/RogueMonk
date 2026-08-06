using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Stick/keyboard input conditioning: radial deadzone + response curve.
    /// Pure math — no engine state, no frame dependency.
    /// </summary>
    public static class InputCurve
    {
        /// <summary>
        /// Applies a radial deadzone and a response exponent to a raw 2D input vector.
        /// Output magnitude is 0 at the deadzone edge and 1 at full stick deflection.
        /// </summary>
        public static Vector2 Condition(Vector2 raw, float deadzone, float responseExponent)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= 0f)
                return Vector2.zero;

            deadzone = Mathf.Clamp(deadzone, 0f, 0.99f);
            if (magnitude <= deadzone)
                return Vector2.zero;

            Vector2 direction = raw / magnitude;
            float clamped = Mathf.Min(magnitude, 1f);
            float rescaled = (clamped - deadzone) / (1f - deadzone);
            float shaped = responseExponent == 1f ? rescaled : Mathf.Pow(rescaled, responseExponent);
            return direction * shaped;
        }
    }
}
