using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// One thrown fragment of a death pop.
    ///
    /// <para>Ticks itself, and that is the whole reason it is a component rather than an entry in
    /// an array owned by <see cref="DeathPop"/>: trash dies with a zero-length death beat, so the
    /// body's GameObject is deactivated on the very frame it is killed and anything driven from
    /// there would stop on its first frame. The pieces live at the scene root and finish on their
    /// own.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathPopPiece : MonoBehaviour
    {
        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer pieceRenderer;
        MaterialPropertyBlock block;
        Vector3 velocity;
        Vector3 spin;
        Color color;
        float lifetime;
        float remaining;
        float startScale;

        void Awake()
        {
            pieceRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public void Play(Vector3 position, Vector3 launchVelocity, Color tint, float scale, float lifetimeSeconds)
        {
            transform.position = position;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * scale;

            velocity = launchVelocity;
            // Derived from the launch direction rather than drawn: cosmetics never touch the run
            // RNG, or simply seeing more deaths would change the level (the M16.1 fragment rule).
            spin = new Vector3(launchVelocity.z, launchVelocity.x, launchVelocity.y) * 90f;
            color = tint;
            startScale = scale;
            lifetime = Mathf.Max(0.0001f, lifetimeSeconds);
            remaining = lifetime;

            gameObject.SetActive(true);
        }

        void Update()
        {
            if (remaining <= 0f)
                return;

            // Unscaled, like every other impact effect: a death can land inside hitstop, and the
            // pop is exactly the frame that freeze is holding.
            float deltaTime = Time.unscaledDeltaTime;
            remaining -= deltaTime;

            float t = 1f - Mathf.Clamp01(remaining / lifetime);

            velocity += Vector3.down * (12f * deltaTime);
            transform.position += velocity * deltaTime;
            transform.Rotate(spin * deltaTime, Space.Self);
            transform.localScale = Vector3.one * (startScale * (1f - t));

            if (pieceRenderer != null)
            {
                var faded = new Color(color.r, color.g, color.b, color.a * (1f - t));
                pieceRenderer.GetPropertyBlock(block);
                // Set both so the piece reads whether it was given the ghost shader or an
                // ordinary lit material.
                block.SetColor(GhostColorId, faded);
                block.SetColor(BaseColorId, faded);
                pieceRenderer.SetPropertyBlock(block);
            }

            if (remaining <= 0f)
                gameObject.SetActive(false);
        }
    }
}
