using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What every damage-over-time type shares. Per-type numbers live on
    /// <see cref="DotDefinition"/>; this is the handful that must be the same across all of them.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/DoT Settings", fileName = "DotSettings")]
    public sealed class DotSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Seconds between floating DoT numbers, per enemy per type. This is the confetti valve: an enemy carrying nine burn stacks would otherwise print a number every few frames, and a screen of numbers reads as no numbers at all. Damage is accumulated between flushes and shown as one figure.")]
        float numberFlushIntervalSeconds = 1f;

        [SerializeField, Tooltip("Size of a DoT number against a direct hit's. Deliberately smaller: a tick is not a hit, and it must never compete with the number that says the player landed something.")]
        float numberScale = 0.7f;

        public float NumberFlushIntervalSeconds => Mathf.Max(0.05f, numberFlushIntervalSeconds);
        public float NumberScale => Mathf.Max(0.05f, numberScale);
    }
}
