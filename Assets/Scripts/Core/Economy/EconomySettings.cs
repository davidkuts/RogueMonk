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
        [SerializeField, Tooltip("Seconds granted per ordinary kill, delivered as a drifting fragment.")]
        int secondsPerKill = 3;
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

        [Header("Minutes (run currency)")]
        [SerializeField, Tooltip("Small Minutes trickle granted directly per kill (no pickup). Room caches are the real income.")]
        int minutesPerKillTrickle = 1;

        public int SecondsPerKill => Mathf.Max(0, secondsPerKill);
        public float SecondsMagnetRadius => Mathf.Max(0f, secondsMagnetRadius);
        public float FragmentDriftSpeed => Mathf.Max(0.1f, fragmentDriftSpeed);
        public float FragmentDriftAcceleration => Mathf.Max(0f, fragmentDriftAcceleration);
        public float FragmentCollectDistance => Mathf.Max(0.05f, fragmentCollectDistance);
        public float FragmentMaxLifetimeSeconds => Mathf.Max(1f, fragmentMaxLifetimeSeconds);
        public int MinutesPerKillTrickle => Mathf.Max(0, minutesPerKillTrickle);
    }
}
