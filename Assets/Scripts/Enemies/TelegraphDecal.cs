using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Draws the footprint an attack is about to cover, flat on the floor, and fills it as the
    /// wind-up runs.
    ///
    /// This is the boss's second readability channel. Telegraph colour alone says "an attack is
    /// coming"; on a rigless capsule with four moves it cannot say <em>which</em>, or how far it
    /// reaches. The decal answers both — and because the fill reaches the outline exactly as the
    /// attack goes active, it also answers "when".
    ///
    /// It runs on the unscaled clock: a wind-up that ends in hitstop must not have its last frames
    /// stretched, or the fill would stop short of the outline at the moment of the strike.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelegraphDecal : MonoBehaviour
    {
        [SerializeField, Tooltip("A unit quad lying flat (rotated 90 on X) using the Monk/Telegraph material.")]
        MeshRenderer quad;
        [SerializeField, Tooltip("Height above the floor. Enough to beat z-fighting, little enough to still read as painted on.")]
        float groundOffset = 0.03f;
        [SerializeField, Tooltip("Peak opacity of the outline.")]
        float maxAlpha = 0.85f;
        [SerializeField, Tooltip("Extra metres added around the hitbox, so the decal is a fair warning rather than a hairline.")]
        float padding = 0.15f;
        [SerializeField, Tooltip("Environment layers that count as floor. Must NOT include characters, or the decal rides up onto a capsule instead of lying on the ground.")]
        LayerMask groundLayers = 1;

        MaterialPropertyBlock block;
        Transform quadTransform;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RectId = Shader.PropertyToID("_Rect");

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (quad == null)
                return;

            quadTransform = quad.transform;
            quad.enabled = false;

            // Detached from the boss on purpose. The boss capsule is scaled up, and a child would
            // inherit that scale — every footprint would be drawn larger than the hitbox it is
            // promising, which is worse than drawing none at all. Living in world space also means
            // the decal does not rotate with the boss after facing locks.
            quadTransform.SetParent(null, true);
            quadTransform.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            // Detaching means the decal no longer dies with the boss; it has to be cleaned up here
            // or every kill would leave a quad behind.
            if (quadTransform != null)
                Destroy(quadTransform.gameObject);
        }

        void OnDisable() => Hide();

        /// <summary>
        /// Places and updates the decal for one wind-up.
        /// </summary>
        /// <param name="progress">0..1 through the wind-up. Drives the fill, so it reaches the outline on the strike.</param>
        public void Show(in HitboxShape shape, Vector3 origin, Vector3 forward, Color color, float progress)
        {
            if (quad == null)
                return;

            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 center = shape.WorldCenter(origin, forward);

            bool box = shape.Kind == HitboxKind.Box;
            float width = box ? shape.Size.x : shape.Radius * 2f;
            float depth = box ? shape.Size.z : shape.Radius * 2f;

            quadTransform.SetPositionAndRotation(
                new Vector3(center.x, GroundHeight(center) + groundOffset, center.z),
                Quaternion.LookRotation(Vector3.down, forward));

            quadTransform.localScale = new Vector3(width + padding * 2f, depth + padding * 2f, 1f);

            quad.enabled = true;
            quad.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(FillId, Mathf.Clamp01(progress));
            block.SetFloat(AlphaId, maxAlpha);
            block.SetFloat(RectId, box ? 1f : 0f);
            quad.SetPropertyBlock(block);
        }

        public void Hide()
        {
            if (quad != null)
                quad.enabled = false;
        }

        /// <summary>
        /// Finds the floor under the decal so it lies on the ground rather than at the attacker's
        /// waist. Falls back to the room's y = 0 plane, which every authored room uses.
        ///
        /// The mask is load-bearing: casting against everything makes the ray land on whichever
        /// capsule happens to be under the start point, which puts the decal in mid-air.
        /// </summary>
        float GroundHeight(Vector3 center)
        {
            return Physics.Raycast(center + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f,
                       groundLayers, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 0f;
        }
    }
}
