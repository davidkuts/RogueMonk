using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A small health bar floating above an enemy.
    ///
    /// <para>Added after a playtest where the damage numbers were invisible: without one you cannot
    /// tell a 10-damage punch from a 3-damage punch, which meant Ambershell's 70% plating — the
    /// entire point of that elite — read as nothing happening at all. A bar is the cheapest way to
    /// make "where you hit matters" legible.</para>
    ///
    /// <para>Built from primitives in world space rather than as a UI canvas: a canvas per enemy is
    /// a canvas rebuild per enemy, and there can be a dozen Scrapfeathers on screen. Two quads and
    /// a property block cost nothing.</para>
    ///
    /// <para>It bills itself out when full and undamaged, so an untouched room is not covered in
    /// bars — the bar appears the moment the enemy is worth tracking.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] EnemyActor actor;

        [SerializeField, Tooltip("Height above the body's origin. Should clear the tallest part of the silhouette.")]
        float height = 1.4f;

        [SerializeField] float width = 0.9f;
        [SerializeField] float thickness = 0.11f;

        [SerializeField, Tooltip("Hide the bar while the enemy is untouched. Off by default: a bar you can only see after you have already hit something cannot tell you what you are about to fight.")]
        bool hideWhenFull;

        [SerializeField, Tooltip("Material for the bar quads. MUST be a URP material — a primitive's default material is built-in Standard, which URP cannot render and draws as magenta.")]
        Material barMaterial;

        [SerializeField, Tooltip("Fill colour. Deliberately desaturated: saturated hues are reserved for telegraphs (DESIGN.md).")]
        Color fillColor = new Color(0.80f, 0.25f, 0.25f, 1f);

        [SerializeField, Tooltip("Backing colour behind the fill.")]
        Color backColor = new Color(0.08f, 0.08f, 0.09f, 1f);

        [SerializeField, Tooltip("How fast the bar chases the real value. Instant reads as a glitch; too slow lies about current health.")]
        float followSpeed = 9f;

        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        Transform root;
        Transform fill;
        MaterialPropertyBlock block;
        Camera view;
        float shown = 1f;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();

            block = new MaterialPropertyBlock();
            Build();
        }

        void OnDestroy()
        {
            if (root != null)
                Destroy(root.gameObject);
        }

        void Build()
        {
            root = new GameObject("HealthBar").transform;

            // World space, unparented: the bodies are scaled and rotated, and a child bar would
            // inherit both — a Cerashorn's bar would be stretched and would spin with its charge.
            root.SetParent(null, true);

            Transform back = MakeQuad("Back", backColor, root, 0f);
            back.localScale = new Vector3(width, thickness, 1f);

            fill = MakeQuad("Fill", fillColor, root, -0.01f);
            fill.localScale = new Vector3(width, thickness, 1f);
        }

        Transform MakeQuad(string name, Color color, Transform parent, float z)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, z);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // GameObject.CreatePrimitive assigns the built-in Standard material, which URP cannot
            // render — it draws as flat magenta. Assigning a real URP material is not decoration
            // here; without it the bar is unreadable and its fill cannot be told from its backing.
            if (barMaterial != null)
                renderer.sharedMaterial = barMaterial;

            var pb = new MaterialPropertyBlock();
            pb.SetColor(ColorId, color);
            renderer.SetPropertyBlock(pb);

            return quad.transform;
        }

        void LateUpdate()
        {
            if (root == null)
                return;

            // Hidden for anything that is gone, dying, or has never been touched.
            bool alive = actor != null && actor.IsAlive && !actor.IsDying;
            float fraction = alive && actor.Health != null && actor.Health.Max > 0f
                ? Mathf.Clamp01(actor.Health.Current / actor.Health.Max)
                : 0f;

            bool visible = alive && (!hideWhenFull || fraction < 0.999f);
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);

            if (!visible)
            {
                shown = fraction;
                return;
            }

            shown = Mathf.Lerp(shown, fraction, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            root.position = transform.position + Vector3.up * height;

            if (view == null)
                view = Camera.main;

            // Billboarded to the camera. The camera is fixed-yaw and top-down-ish, so this is a
            // constant rotation in practice, but reading it keeps the bar correct if the rig moves.
            if (view != null)
                root.rotation = view.transform.rotation;

            // Scaled from the left edge, so the bar drains rather than shrinking toward its middle.
            fill.localScale = new Vector3(width * shown, thickness, 1f);
            fill.localPosition = new Vector3(-width * (1f - shown) * 0.5f, 0f, -0.01f);
        }
    }
}
