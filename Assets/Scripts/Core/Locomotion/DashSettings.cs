using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// All dash tuning. Starting values come from DESIGN.md § Movement &amp; dash.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Dash Settings", fileName = "DashSettings")]
    public sealed class DashSettings : ScriptableObject, IDashSettings
    {
        [Header("Travel")]
        [SerializeField, Tooltip("Total ground distance of one dash, in metres.")]
        float distanceMeters = 4f;
        [SerializeField, Tooltip("Wall-clock length of the dash.")]
        float durationSeconds = 0.18f;
        [SerializeField, Tooltip("Distance covered (0..1) by normalized dash time. Must run from (0,0) to (1,1). Front-loaded = explosive launch.")]
        AnimationCurve travelCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 2.2f, 2.2f),
            new Keyframe(1f, 1f, 0.15f, 0.15f));
        [SerializeField, Range(0f, 1.5f), Tooltip("Speed carried out of the dash, as a fraction of walking top speed.")]
        float exitSpeedFraction = 1f;

        [Header("Invulnerability")]
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the dash covered by i-frames. The uncovered tail is the punish window.")]
        float iFrameFraction = 0.85f;
        [SerializeField, Tooltip("Extra protection after the i-frames end, still counting as a perfect dodge. Exists because a melee swing is only live for a tenth of a second, so without it a melee perfect dodge is frame-perfect while a projectile one is comfortable.")]
        float perfectDodgeGraceSeconds = 0.09f;
        [SerializeField, Tooltip("The same protection against a hitbox that TRAVELS to you. Shorter on purpose: a projectile is caught by any instant of the window, where a melee swing also demands you be standing in the arc. One number for both made projectiles trivial.")]
        float projectileDodgeGraceSeconds = 0.05f;

        [Header("Charges")]
        [SerializeField, Tooltip("Charges held at once. Two pips on the HUD.")]
        int maxCharges = 2;
        [SerializeField, Tooltip("Recharge time for one charge. Recharge is sequential — spending both means waiting this long twice.")]
        float rechargeSeconds = 1.5f;

        [Header("Input")]
        [SerializeField, Tooltip("How long a dash press stays queued while the player cannot act on it.")]
        float bufferSeconds = 0.15f;

        public float DistanceMeters => distanceMeters;
        public float DurationSeconds => durationSeconds;
        public float IFrameFraction => iFrameFraction;
        public float PerfectDodgeGraceSeconds => perfectDodgeGraceSeconds;
        public float ProjectileDodgeGraceSeconds => projectileDodgeGraceSeconds;
        public int MaxCharges => maxCharges;
        public float RechargeSeconds => rechargeSeconds;
        public float BufferSeconds => bufferSeconds;
        public float ExitSpeedFraction => exitSpeedFraction;

        public float EvaluateTravel(float normalizedTime) => travelCurve.Evaluate(normalizedTime);
    }
}
