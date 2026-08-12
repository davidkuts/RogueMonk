using UnityEngine;

namespace Game.Core.Feedback
{
    /// <summary>
    /// One floating damage number: rises, drifts, fades and puts itself away.
    ///
    /// <para>Its digit segments are built once and then switched on and off per value, so showing
    /// a number allocates nothing. Billboards to the camera the way the health bars do, because on
    /// a fixed top-down rig a world-aligned glyph is frequently edge-on and unreadable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberLabel : MonoBehaviour
    {
        /// <summary>Most digits a single number can show. Nothing in the game hits for 10,000.</summary>
        public const int MaxDigits = 4;

        Transform glyphRoot;
        GameObject minus;
        GameObject[][] cells;          // [digit place][segment]
        Renderer[] allRenderers;
        MaterialPropertyBlock block;
        Camera view;

        Vector3 velocity;
        Color color;
        float lifetime;
        float remaining;
        float baseScale;
        float gravity;

        /// <summary>
        /// Whether this number pops on arrival. A direct hit does; a damage-over-time tick does
        /// not — the punch means "something just landed", which is exactly what a DoT tick is not,
        /// and a body carrying several stacks would otherwise pulse continuously.
        /// </summary>
        bool punch;

        /// <summary>True while this label is in flight and must not be handed out again.</summary>
        public bool InUse => remaining > 0f;

        public void Build(Material material)
        {
            block = new MaterialPropertyBlock();

            glyphRoot = new GameObject("Glyphs").transform;
            glyphRoot.SetParent(transform, false);

            cells = new GameObject[MaxDigits][];
            for (int i = 0; i < MaxDigits; i++)
            {
                var cell = new GameObject($"Digit_{i}").transform;
                cell.SetParent(glyphRoot, false);
                cells[i] = DigitBuilder.BuildDigitCell(cell, material);
            }

            var minusRoot = new GameObject("Minus").transform;
            minusRoot.SetParent(glyphRoot, false);
            minus = DigitBuilder.BuildBar(minusRoot, material);

            allRenderers = glyphRoot.GetComponentsInChildren<Renderer>(true);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows <paramref name="value"/> at <paramref name="worldPosition"/>. A negative value
        /// draws a leading minus, which is what damage taken uses.
        /// </summary>
        public void Show(
            Vector3 worldPosition, int value, Color tint, float scale,
            float lifetimeSeconds, Vector3 drift, float arcGravity, bool punchOnArrival = true)
        {
            punch = punchOnArrival;

            bool negative = value < 0;
            int magnitude = Mathf.Abs(value);
            int digits = Mathf.Min(DigitBuilder.DigitCount(magnitude), MaxDigits);

            // Lay the used cells out centred, and park the unused ones by switching them off.
            int marks = digits + (negative ? 1 : 0);
            float left = -(marks - 1) * DigitBuilder.DigitWidth * 0.5f;
            int slot = 0;

            minus.SetActive(negative);
            if (negative)
            {
                minus.transform.parent.localPosition = new Vector3(left, 0f, 0f);
                slot++;
            }

            for (int i = 0; i < MaxDigits; i++)
            {
                bool used = i < digits;
                GameObject[] segments = cells[i];

                if (!used)
                {
                    for (int s = 0; s < segments.Length; s++)
                        segments[s].SetActive(false);
                    continue;
                }

                // Cell 0 is the most significant digit on screen, so it reads left to right.
                int place = digits - 1 - i;
                bool[] mask = DigitBuilder.Glyph(DigitBuilder.DigitAt(magnitude, place));
                for (int s = 0; s < segments.Length; s++)
                    segments[s].SetActive(mask[s]);

                segments[0].transform.parent.localPosition =
                    new Vector3(left + slot * DigitBuilder.DigitWidth, 0f, 0f);
                slot++;
            }

            transform.position = worldPosition;
            color = tint;
            baseScale = scale;
            velocity = drift;
            gravity = arcGravity;
            lifetime = Mathf.Max(0.0001f, lifetimeSeconds);
            remaining = lifetime;
            transform.localScale = Vector3.one * scale;

            gameObject.SetActive(true);
            Tint(1f);
        }

        void LateUpdate()
        {
            if (remaining <= 0f)
                return;

            // Unscaled, like every other impact effect: a hit triggers hitstop, and the number is
            // describing the exact frame that freeze is holding.
            float deltaTime = Time.unscaledDeltaTime;
            remaining -= deltaTime;

            float t = 1f - Mathf.Clamp01(remaining / lifetime);

            velocity += Vector3.down * (gravity * deltaTime);    // gentle arc, so it lofts and settles
            transform.position += velocity * deltaTime;

            // Pops slightly larger on arrival, then shrinks away as it fades. A DoT tick holds its
            // size instead: it did not "land", and a punch would claim it did.
            float pop = punch
                ? (t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 0.9f, (t - 0.15f) / 0.85f))
                : 1f;
            transform.localScale = Vector3.one * (baseScale * pop);

            if (view == null)
                view = Camera.main;
            if (view != null)
                transform.rotation = view.transform.rotation;

            // Holds full strength for the first half, then fades: the number has to be readable
            // before it starts leaving.
            Tint(t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f);

            if (remaining <= 0f)
                gameObject.SetActive(false);
        }

        void Tint(float alpha)
        {
            if (allRenderers == null)
                return;

            var faded = new Color(color.r, color.g, color.b, color.a * alpha);
            for (int i = 0; i < allRenderers.Length; i++)
                DigitBuilder.Paint(allRenderers[i], faded, block);
        }
    }
}
