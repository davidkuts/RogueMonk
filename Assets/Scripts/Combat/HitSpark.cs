using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A brief flash at the point of impact: a quad that scales up and fades out.
    ///
    /// Runs on the unscaled clock on purpose. A hit triggers hitstop, so a spark ticking on
    /// the scaled clock would freeze at its first frame for the whole freeze and then vanish —
    /// the exact moment it is meant to be selling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitSpark : MonoBehaviour
    {
        [SerializeField] float lifetimeSeconds = 0.18f;
        [SerializeField] float startScale = 0.35f;
        [SerializeField] float endScale = 1.5f;

        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        Color color = Color.white;
        float remaining;

        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
        }

        public void Play(Vector3 position, Vector3 normal, Color tint, float scale)
        {
            transform.position = position;
            // Face the camera rather than the hit normal: on a fixed top-down view a
            // normal-aligned quad is frequently edge-on and effectively invisible.
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;

            color = tint;
            remaining = lifetimeSeconds;
            transform.localScale = Vector3.one * (startScale * scale);
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (remaining <= 0f)
                return;

            remaining -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.0001f, lifetimeSeconds));

            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);

            if (meshRenderer != null)
            {
                meshRenderer.GetPropertyBlock(block);
                block.SetColor(GhostColorId, new Color(color.r, color.g, color.b, color.a * (1f - t)));
                meshRenderer.SetPropertyBlock(block);
            }

            if (remaining <= 0f)
                gameObject.SetActive(false);
        }
    }
}
