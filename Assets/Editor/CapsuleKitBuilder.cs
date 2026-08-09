using Game.Combat;
using Game.Enemies;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds a <see cref="CapsuleRecipe"/> into an actual body of primitives under a prefab or a
    /// scene object.
    ///
    /// <para>Editor-time rather than runtime on purpose. The armoured parts become real colliders
    /// with <see cref="DamageZone"/> on them, and <c>EnemyActor</c> caches its zones in
    /// <c>Awake</c> — building the body at runtime would race that and leave an Ambershell whose
    /// plates exist but are never found. Baking into the prefab also means the silhouette is
    /// inspectable, which is the whole point of the capsule phase.</para>
    ///
    /// <para>Idempotent: it deletes the existing <c>Body</c> child and rebuilds it, so a recipe can
    /// be re-run after every tweak without accumulating debris.</para>
    /// </summary>
    public static class CapsuleKitBuilder
    {
        const string BodyChildName = "Body";

        [MenuItem("Monk/Capsules/Rebuild Selected Enemy Bodies")]
        public static void RebuildSelected()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("Select one or more enemy prefabs (or scene objects) carrying a CapsuleBody.");
                return;
            }

            int built = 0;
            foreach (GameObject go in selection)
            {
                var body = go.GetComponent<CapsuleBody>();
                if (body == null || body.Recipe == null)
                    continue;

                Build(go, body.Recipe);
                EditorUtility.SetDirty(go);
                built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"CapsuleKitBuilder: rebuilt {built} body(ies).");
        }

        /// <summary>
        /// Replaces <paramref name="root"/>'s body with the one described by
        /// <paramref name="recipe"/>. Returns the new body root.
        /// </summary>
        public static GameObject Build(GameObject root, CapsuleRecipe recipe)
        {
            if (root == null || recipe == null)
                return null;

            Transform existing = root.transform.Find(BodyChildName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var body = new GameObject(BodyChildName);
            body.transform.SetParent(root.transform, false);
            body.layer = root.layer;

            Material material = ResolveMaterial();
            bool anyZone = false;

            foreach (CapsulePart part in recipe.Parts)
            {
                GameObject piece = GameObject.CreatePrimitive(part.Primitive);
                piece.name = string.IsNullOrWhiteSpace(part.Name) ? part.Primitive.ToString() : part.Name;
                piece.transform.SetParent(body.transform, false);

                // CreatePrimitive puts everything on Default. A zone collider on the wrong layer is
                // invisible to every hitbox query in the game, so the plating would simply never be
                // hit — and the enemy would look invulnerable rather than armoured.
                piece.layer = root.layer;
                piece.transform.localPosition = part.LocalPosition;
                piece.transform.localEulerAngles = part.LocalEulerAngles;
                piece.transform.localScale = part.LocalScale == Vector3.zero ? Vector3.one : part.LocalScale;

                var collider = piece.GetComponent<Collider>();

                if (part.IsDamageZone)
                {
                    anyZone = true;
                    // A zone collider must be a trigger: it exists to be *found* by an overlap
                    // query, not to push the body around. Left solid, an enemy's own plates would
                    // fight its CharacterController.
                    if (collider != null)
                        collider.isTrigger = true;

                    var zone = piece.AddComponent<DamageZone>();
                    var serialized = new SerializedObject(zone);
                    serialized.FindProperty("zoneId").stringValue = piece.name;
                    serialized.FindProperty("armored").boolValue = part.Armored;
                    serialized.FindProperty("damageReduction").floatValue = part.DamageReduction;
                    serialized.FindProperty("blocksStagger").boolValue = part.Armored;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (collider != null)
                {
                    // Visual only. Leaving these colliders in would have every hitbox query find
                    // five copies of the same enemy and would block movement against its own art.
                    Object.DestroyImmediate(collider);
                }

                var renderer = piece.GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;

                if (material != null)
                    renderer.sharedMaterial = material;

                // Written into a property block rather than a material instance so seven
                // archetypes share one material and the toon pass stays a single draw setup.
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", recipe.ColorFor(part));
                renderer.SetPropertyBlock(block);
            }

            // Marks the body hittable only on its zones. Added when the recipe has any, removed
            // when it has none, so re-baking a recipe that lost its plating does not leave a body
            // that nothing can hit.
            var marker = root.GetComponent<ZonedBody>();
            if (anyZone && marker == null)
                root.AddComponent<ZonedBody>();
            else if (!anyZone && marker != null)
                Object.DestroyImmediate(marker);

            return body;
        }

        /// <summary>
        /// The shared enemy material. Falls back to the pipeline default rather than failing, so a
        /// missing art asset never blocks a playtest.
        /// </summary>
        static Material ResolveMaterial()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material EnemyCapsule");
            if (guids.Length > 0)
            {
                var found = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (found != null)
                    return found;
            }

            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        }
    }
}
