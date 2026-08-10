using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Stopgap system tuning: the carry cap and the pool a Stopgap reward draws from.
    /// Panic buttons, not a hoardable resource — hence the cap of 2.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Stopgap Settings", fileName = "StopgapSettings")]
    public sealed class StopgapSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Most Stopgaps carried at once (REWARDS.md §5: 2). The Cracked Hourglass Stray will add one when its logic lands.")]
        int carryCap = 2;
        [SerializeField, Tooltip("What a Stopgap reward can hand out. Drawn uniformly.")]
        List<StopgapDefinition> pool = new List<StopgapDefinition>();

        public int CarryCap => Mathf.Max(1, carryCap);

        public IReadOnlyList<StopgapDefinition> Pool => pool;
    }
}
