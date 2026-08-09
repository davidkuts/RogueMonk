using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Ties an enemy prefab to the <see cref="CapsuleRecipe"/> its body was built from.
    ///
    /// <para>Holds no logic. It exists so the builder can find which recipe to re-bake, and so a
    /// playtest note like "Cerashorn's frill isn't reading from above" is answered by editing one
    /// asset and pressing rebuild rather than by dragging primitives in a prefab.</para>
    ///
    /// <para>When the meshes arrive this component and its <c>Body</c> child are what get deleted —
    /// ENEMIES_BIOME1.md § 7 step 6, capsule swapped for skinned mesh behind the same controller.
    /// Nothing else on the prefab changes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CapsuleBody : MonoBehaviour
    {
        [SerializeField, Tooltip("The silhouette this body was baked from. Editing it and running Monk > Capsules > Rebuild re-bakes the primitives.")]
        CapsuleRecipe recipe;

        public CapsuleRecipe Recipe => recipe;

        /// <summary>The archetype's identity hue, for the debug spawner's labels and gizmos.</summary>
        public Color IdentityColor => recipe != null ? recipe.IdentityColor : Color.grey;
    }
}
