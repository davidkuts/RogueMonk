using UnityEngine;

namespace Game.Core.Economy
{
    /// <summary>
    /// Tuning for the per-kill currency flow (REWARDS.md §1). Cache payouts per tier live on
    /// the reward definitions, not here — this covers only what falls out of ordinary combat.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Economy Settings", fileName = "EconomySettings")]
    public sealed class EconomySettings : ScriptableObject
    {
        [Header("Seconds (boon fuel, auto-collect fragments)")]
        [SerializeField, Tooltip("Seconds carried per fragment. A kill worth more sheds several fragments in a small ring, so a big kill visibly rains time. Per-kill AMOUNTS live on each EnemyDefinition — proportional to toughness, never flat per body.")]
        int secondsPerFragment = 3;
        [SerializeField, Tooltip("Distance at which a loose fragment starts drifting into the Second Hand.")]
        float secondsMagnetRadius = 6f;
        [SerializeField, Tooltip("Fragment drift speed once magnetised, m/s.")]
        float fragmentDriftSpeed = 14f;
        [SerializeField, Tooltip("Acceleration of the drift, so a fragment homes rather than gliding at one rate.")]
        float fragmentDriftAcceleration = 30f;
        [SerializeField, Tooltip("Distance at which the fragment counts as collected.")]
        float fragmentCollectDistance = 0.6f;
        [SerializeField, Tooltip("Fragments that outlive this are collected wherever they lie, so none is ever lost behind a door.")]
        float fragmentMaxLifetimeSeconds = 20f;

        [Header("Room-exit auto-collect")]
        [SerializeField, Tooltip("How much faster a fragment flies once the player leaves the room. The point is legibility: the player must SEE the loose time streak toward them and understand nothing was left behind, so this wants to be fast enough to finish inside the transition.")]
        float autoCollectSpeedMultiplier = 4f;
        [SerializeField, Tooltip("Longest a swept fragment may stay in the air before it is granted wherever it is. The visual is best-effort; the income is not. Keep it under the length of a room transition.")]
        float autoCollectHardGrantSeconds = 0.7f;

        public int SecondsPerFragment => Mathf.Max(1, secondsPerFragment);
        public float AutoCollectSpeedMultiplier => Mathf.Max(1f, autoCollectSpeedMultiplier);
        public float AutoCollectHardGrantSeconds => Mathf.Max(0.05f, autoCollectHardGrantSeconds);
        public float SecondsMagnetRadius => Mathf.Max(0f, secondsMagnetRadius);
        public float FragmentDriftSpeed => Mathf.Max(0.1f, fragmentDriftSpeed);
        public float FragmentDriftAcceleration => Mathf.Max(0f, fragmentDriftAcceleration);
        public float FragmentCollectDistance => Mathf.Max(0.05f, fragmentCollectDistance);
        public float FragmentMaxLifetimeSeconds => Mathf.Max(1f, fragmentMaxLifetimeSeconds);
    }
}
