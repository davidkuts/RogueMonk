using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// An ordered attack chain (punch → punch → kick) plus the input feel that goes with it.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Combo Definition", fileName = "ComboDefinition")]
    public sealed class ComboDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Attacks in chain order. The chain wraps back to the first after the last.")]
        AttackDefinition[] attacks = new AttackDefinition[0];

        [SerializeField, Tooltip("How long an attack press stays queued. DESIGN.md mandates ~150 ms.")]
        float inputBufferSeconds = 0.15f;

        public float InputBufferSeconds => inputBufferSeconds;

        public int Count => attacks != null ? attacks.Length : 0;

        /// <summary>The chain as simulation-facing definitions. Null entries are skipped.</summary>
        public IReadOnlyList<IAttackDefinition> BuildSequence()
        {
            var sequence = new List<IAttackDefinition>();
            if (attacks == null)
                return sequence;

            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] != null)
                    sequence.Add(attacks[i]);
            }

            return sequence;
        }
    }
}
