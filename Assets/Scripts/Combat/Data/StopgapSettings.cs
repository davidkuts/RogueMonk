using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What a Stopgap reward can hand out.
    ///
    /// <para>The carry cap that used to live here is gone: Stopgaps now hold one per D-pad
    /// direction rather than sharing a pool of two, so the ceiling is the number of directions and
    /// there is no number left to tune.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Stopgap Settings", fileName = "StopgapSettings")]
    public sealed class StopgapSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Everything a Stopgap reward could hand out. Entries whose asset is switched off are skipped, so disabling one is a change on the asset rather than a silent deletion from this list.")]
        List<StopgapDefinition> pool = new List<StopgapDefinition>();

        readonly List<StopgapDefinition> grantableScratch = new List<StopgapDefinition>();

        /// <summary>Everything in the pool, enabled or not. For tooling and tests.</summary>
        public IReadOnlyList<StopgapDefinition> Pool => pool;

        /// <summary>
        /// The pool a reward may actually draw from: enabled entries only.
        ///
        /// <para>Filtering here rather than pruning the list keeps a disabled Stopgap's intent
        /// visible — Wound Spring is off because the vortex cooldown is 0 and it has nothing to
        /// refund, which is a fact worth reading off the asset rather than inferring from an
        /// absence.</para>
        /// </summary>
        public IReadOnlyList<StopgapDefinition> Grantable
        {
            get
            {
                grantableScratch.Clear();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].Enabled)
                        grantableScratch.Add(pool[i]);
                }

                return grantableScratch;
            }
        }
    }
}
