using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// One primitive in a capsule-phase body.
    ///
    /// <para>Everything here is silhouette. ENEMIES_BIOME1.md § 1 sets a black-blob test — all
    /// seven bodies must be distinguishable with zero colour and zero animation — so the flattened
    /// box that is Cerashorn's frill and the vertical quad that is Sailspit's sail are doing the
    /// real work. Colour is the second layer, never the crutch.</para>
    /// </summary>
    [Serializable]
    public struct CapsulePart
    {
        [Tooltip("Child object name. Also the zone id when this part is armoured.")]
        public string Name;

        public PrimitiveType Primitive;

        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
        public Vector3 LocalScale;

        [Tooltip("Use the accent colour rather than the identity colour. Underbellies, frills, plates.")]
        public bool UseAccent;

        [Tooltip("Keep this part's collider and mark it a DamageZone. Everything else is visual only and has its collider stripped.")]
        public bool IsDamageZone;

        [Tooltip("Zone only: an amber plate. False makes it an honestly soft spot.")]
        public bool Armored;

        [Range(0f, 1f), Tooltip("Zone only: fraction of damage an intact plate eats.")]
        public float DamageReduction;
    }

    /// <summary>
    /// A capsule-phase body: an identity colour and a handful of primitives arranged into a
    /// recognisable shape.
    ///
    /// <para>Exists so the roster can be built and re-built from data while the meshes do not
    /// exist yet. ENEMIES_BIOME1.md § 7 is explicit that mechanics never wait on art — the capsule
    /// proves the fight, and the mesh later drops in behind the same controller. Keeping the body
    /// in an asset rather than hand-placed in each prefab means a silhouette can be retuned during
    /// a playtest without anyone opening a prefab.</para>
    ///
    /// <para>Identity colours are locked in ENEMIES_BIOME1.md § 1 and must stay clear of the
    /// reserved gameplay channels. Sailspit's violet moved to a muted plum on 2026-08-09 for
    /// exactly that reason: saturated violet already means "a gap-closer is coming".</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Capsule Recipe", fileName = "CapsuleRecipe")]
    public sealed class CapsuleRecipe : ScriptableObject
    {
        [SerializeField, Tooltip("Shown in the debug spawner menu.")]
        string displayName = "Enemy";

        [SerializeField, Tooltip("The archetype's one identity hue. Must not sit in a reserved gameplay channel unless it is Ambershell's amber, which is the channel used correctly.")]
        Color identityColor = Color.grey;

        [SerializeField, Tooltip("Secondary hue for underbellies, frills and plates. Keeps the silhouette readable without adding a second identity.")]
        Color accentColor = new Color(0.8f, 0.8f, 0.78f);

        [SerializeField, Tooltip("Assembled in order under a 'Body' child. Positions are in the enemy's local space, Z forward.")]
        CapsulePart[] parts = new CapsulePart[0];

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Color IdentityColor => identityColor;
        public Color AccentColor => accentColor;
        public CapsulePart[] Parts => parts;

        public Color ColorFor(in CapsulePart part) => part.UseAccent ? accentColor : identityColor;
    }
}
