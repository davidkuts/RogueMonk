using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Fits every wall under this node with a <see cref="WallOccluder"/> at load.
    ///
    /// <para>One authoring point per room instead of one per wall. Rooms are hand-built prefabs and
    /// walls get added to them; a per-wall component would mean the first wall somebody forgot is a
    /// wall that silently keeps hiding wind-ups, which is the exact failure this milestone exists to
    /// fix. Declaring "everything under here occludes" cannot be forgotten by omission.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallOccluderGroup : MonoBehaviour
    {
        [SerializeField, Tooltip("Fit children that are switched off at load too, so a wall enabled later is already covered.")]
        bool includeInactive = true;

        void Awake()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive);

            // The centre of everything under this node is the room's middle, which is all a wall
            // needs to work out which way it faces. Measured from the walls themselves rather than
            // taken from this transform, so a group parented somewhere off-centre still answers
            // correctly.
            var bounds = new Bounds();
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !Qualifies(renderers[i]))
                    continue;

                if (hasBounds)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                else
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
            }

            if (!hasBounds)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !Qualifies(renderer))
                    continue;

                WallOccluder occluder = renderer.GetComponent<WallOccluder>();
                if (occluder == null)
                    occluder = renderer.gameObject.AddComponent<WallOccluder>();

                occluder.SetGroupCentre(bounds.center);
            }
        }

        /// <summary>
        /// Detection is a physics cast, so a renderer with no collider can never be found by one.
        /// Fitting it would be dead weight that reads as coverage.
        /// </summary>
        static bool Qualifies(Renderer renderer) => renderer.GetComponent<Collider>() != null;
    }
}
