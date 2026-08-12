using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The warning that something is about to land here, drawn on the floor for the whole flight.
    ///
    /// <para><b>Deliberately a different visual from the thing it warns about.</b> This is a bright
    /// ring whose fill grows and reaches the outline exactly as the glob arrives — the same "the
    /// fill is the clock" language every ground telegraph in the game already uses, so the player
    /// reads it as *incoming* without being taught. The puddle it becomes is darker, static and
    /// softly breathing, so *arrived and still dangerous* never looks like *about to happen*. The
    /// two are never on screen together: this dies on the frame the goo is born.</para>
    ///
    /// <para>Builds its own quad rather than needing a prefab, so a lobbed shot has one fewer thing
    /// to wire up and one fewer thing to leave unassigned.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImpactTelegraph : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RectId = Shader.PropertyToID("_Rect");
        static readonly int ArcId = Shader.PropertyToID("_ArcDegrees");
        static readonly int EdgeId = Shader.PropertyToID("_EdgeWidth");

        MaterialPropertyBlock block;
        MeshRenderer decal;
        Mesh mesh;

        float duration;
        float elapsed;
        float alpha = 0.85f;

        /// <summary>
        /// Draws a warning of <paramref name="radius"/> that completes in <paramref name="seconds"/>.
        /// Removes itself when the flight ends, whatever happens to the projectile.
        /// </summary>
        public static ImpactTelegraph Spawn(
            Vector3 groundPosition, float radius, float seconds, Color colour, Material material, float groundOffset)
        {
            if (material == null)
                return null;

            var go = new GameObject("ImpactTelegraph");
            go.transform.position = new Vector3(groundPosition.x, groundPosition.y + groundOffset, groundPosition.z);

            var telegraph = go.AddComponent<ImpactTelegraph>();
            telegraph.Build(radius, seconds, colour, material);
            return telegraph;
        }

        void Build(float radius, float seconds, Color colour, Material material)
        {
            duration = Mathf.Max(0.01f, seconds);
            block = new MaterialPropertyBlock();

            mesh = new Mesh { name = "ImpactTelegraphQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            var quad = new GameObject("Decal");
            quad.transform.SetParent(transform, false);
            quad.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);

            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            decal = quad.AddComponent<MeshRenderer>();
            decal.sharedMaterial = material;
            decal.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            decal.receiveShadows = false;
            decal.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            decal.GetPropertyBlock(block);
            block.SetColor(ColorId, colour);
            block.SetFloat(FillId, 0f);
            block.SetFloat(AlphaId, alpha);
            block.SetFloat(RectId, 0f);
            block.SetFloat(ArcId, 360f);
            // A pronounced outline: this has to read as a boundary being drawn around ground the
            // player still has time to leave.
            block.SetFloat(EdgeId, 0.14f);
            decal.SetPropertyBlock(block);
        }

        /// <summary>Ends the warning early — used when the shot is cut short rather than landing.</summary>
        public void Dismiss()
        {
            if (this != null)
                Destroy(gameObject);
        }

        void Update()
        {
            elapsed += Time.deltaTime;

            float fill = Mathf.Clamp01(elapsed / duration);
            decal.GetPropertyBlock(block);
            block.SetFloat(FillId, fill);
            block.SetFloat(AlphaId, alpha);
            decal.SetPropertyBlock(block);

            // Held one frame past full rather than destroyed at exactly 1: the projectile's own
            // arrival is what removes this, and the warning must never blink out early.
            if (elapsed > duration + 0.5f)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }
    }
}
