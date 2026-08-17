using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Locomotion
{
    /// <summary>
    /// Marks a prop's collider as something the Blink may carry the player through, if the
    /// dash has enough distance left to clear it (see
    /// <see cref="IDashSettings.ObstaclePhaseLeewayMeters"/>). Not stopping at every rock and
    /// crate the way a wall does is the skill-ceiling payoff of committing to a dash's fixed
    /// direction and distance.
    ///
    /// <para><b>A component, not a layer</b> — the same call <c>WallOccluder</c> made and for the
    /// same reason: floor, walls and small props all share the Default layer, and several systems
    /// (obstacle avoidance, the Sailspit's clearance probe) already key off that. Moving a prop
    /// onto its own layer to flag it risks silently breaking one of those. Marking the object
    /// itself makes dash-phasing opt-in per prop instead of an accident of layer bookkeeping.</para>
    ///
    /// <para>Walls never get this component. DESIGN.md's dash "never crosses room boundaries" —
    /// phasing through a wall would break room containment, exactly why
    /// <c>PlayerMotor.dashPhasesThroughLayers</c> deliberately excludes walls for enemies too.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DashPhaseable : MonoBehaviour
    {
        static readonly Dictionary<Collider, DashPhaseable> byCollider = new Dictionary<Collider, DashPhaseable>();

        Collider[] colliders;

        /// <summary>Resolves a collider a cast hit back to its marker, or false when it is not one.</summary>
        public static bool TryGet(Collider collider, out DashPhaseable phaseable)
        {
            if (collider != null)
                return byCollider.TryGetValue(collider, out phaseable);

            phaseable = null;
            return false;
        }

        void Awake() => colliders = GetComponents<Collider>();

        void OnEnable()
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    byCollider[colliders[i]] = this;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    byCollider.Remove(colliders[i]);
            }
        }
    }
}
