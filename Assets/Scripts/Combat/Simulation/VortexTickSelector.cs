using System.Collections.Generic;

namespace Game.Combat
{
    /// <summary>
    /// One collider offered to a vortex tick. Engine-free: the adapter resolves the physics and
    /// hands over identities and distances, so the selection rule itself stays testable.
    /// </summary>
    public readonly struct VortexTickCandidate
    {
        /// <summary>Identity of the body this collider belongs to. Colliders sharing a key are one enemy.</summary>
        public readonly int TargetKey;

        /// <summary>Index back into the caller's collider array.</summary>
        public readonly int ColliderIndex;

        /// <summary>Squared planar distance from the vortex centre. Squared because nothing here needs the root.</summary>
        public readonly float SqrDistance;

        public VortexTickCandidate(int targetKey, int colliderIndex, float sqrDistance)
        {
            TargetKey = targetKey;
            ColliderIndex = colliderIndex;
            SqrDistance = sqrDistance;
        }
    }

    /// <summary>
    /// Picks exactly one collider per body for a vortex tick.
    ///
    /// <para><b>Why this exists.</b> The Undertow used to resolve a hit per <em>collider</em>, which
    /// made its damage a function of how many colliders an enemy happened to have. A five-collider
    /// boss took five times the damage, five times the poise, five sparks and five hitstop requests
    /// per tick — so spamming the spin at a large single target out-damaged the combo, which is the
    /// exact inversion of "its job is space and setup, not damage". Every other attacker in the game
    /// already deduplicates per <see cref="IDamageable"/>; the vortex was the sole exception.</para>
    ///
    /// <para><b>The rule is nearest-zone-wins.</b> A swung attack lands where it was aimed, so which
    /// plate it strikes is answered by the player's positioning. A radial pull has no aim, so the
    /// question "which plate did the spin hit" has no natural answer — and taking whatever the
    /// physics query happened to return first would make armour apply intermittently, which reads as
    /// bad luck rather than as a rule. Nearest to the centre is deterministic, reproducible, and
    /// says something true: the drag bites on the part closest to the drain.</para>
    ///
    /// <para>Ties break toward the lower collider index purely so the outcome is stable across
    /// frames — two colliders at identical distance must not alternate.</para>
    /// </summary>
    public sealed class VortexTickSelector
    {
        readonly Dictionary<int, int> bestCandidateByTarget = new Dictionary<int, int>();
        readonly List<int> chosen = new List<int>();

        /// <summary>Collider indices to resolve this tick — one per body, nearest zone first.</summary>
        public IReadOnlyList<int> Chosen => chosen;

        /// <summary>
        /// Reduces <paramref name="candidates"/> to one collider per target. Reuses its buffers, so
        /// a spin ticking three times a cast in a crowded room allocates nothing after the first.
        /// </summary>
        public void Select(IReadOnlyList<VortexTickCandidate> candidates)
        {
            bestCandidateByTarget.Clear();
            chosen.Clear();

            if (candidates == null || candidates.Count == 0)
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                VortexTickCandidate candidate = candidates[i];

                if (!bestCandidateByTarget.TryGetValue(candidate.TargetKey, out int bestIndex))
                {
                    bestCandidateByTarget[candidate.TargetKey] = i;
                    continue;
                }

                VortexTickCandidate best = candidates[bestIndex];
                bool closer = candidate.SqrDistance < best.SqrDistance;
                bool tiedButEarlier =
                    candidate.SqrDistance == best.SqrDistance && candidate.ColliderIndex < best.ColliderIndex;

                if (closer || tiedButEarlier)
                    bestCandidateByTarget[candidate.TargetKey] = i;
            }

            // Emitted in first-seen target order rather than dictionary order, so the damage,
            // sparks and log lines of one tick come out in a stable sequence.
            for (int i = 0; i < candidates.Count; i++)
            {
                int key = candidates[i].TargetKey;
                if (bestCandidateByTarget.TryGetValue(key, out int bestIndex) && bestIndex == i)
                    chosen.Add(candidates[i].ColliderIndex);
            }
        }
    }
}
