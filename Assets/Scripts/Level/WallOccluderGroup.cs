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

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                // Detection is a physics cast, so a renderer with no collider can never be found by
                // one. Fitting it would be dead weight that reads as coverage.
                if (renderer.GetComponent<Collider>() == null)
                    continue;

                if (renderer.GetComponent<WallOccluder>() == null)
                    renderer.gameObject.AddComponent<WallOccluder>();
            }
        }
    }
}
