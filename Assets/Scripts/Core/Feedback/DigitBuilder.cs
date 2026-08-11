using UnityEngine;

namespace Game.Core.Feedback
{
    /// <summary>
    /// Seven-segment digit glyphs built from primitive blocks.
    ///
    /// <para>Not a TextMesh, and not for want of trying: built-in font shader paths get stripped
    /// by URP player builds, which is the magenta-health-bar lesson this project already paid for
    /// once. A cube carrying a shared scene material cannot fail that way, so anything that has to
    /// survive a build renders as geometry. The reward door labels were built this way for the
    /// same reason, before per-door rewards replaced them with icons.</para>
    ///
    /// <para>Segments are built ONCE into a reusable rig and then switched on and off per value —
    /// damage numbers pop several times a second in a busy room, and rebuilding a dozen cubes for
    /// each one would allocate through the whole fight.</para>
    ///
    /// <para>Segment layout, standard calculator order:</para>
    /// <code>
    ///  --0--
    /// |     |
    /// 5     1
    /// |     |
    ///  --6--
    /// |     |
    /// 4     2
    /// |     |
    ///  --3--
    /// </code>
    /// </summary>
    public static class DigitBuilder
    {
        public const int SegmentsPerDigit = 7;

        /// <summary>Width of one digit cell in local units, including the gap to the next.</summary>
        public const float DigitWidth = 0.62f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        const float Height = 1f;
        const float Thickness = 0.14f;
        const float Depth = 0.12f;

        /// <summary>Which segments each digit 0–9 lights.</summary>
        static readonly bool[][] Glyphs =
        {
            new[] { true,  true,  true,  true,  true,  true,  false }, // 0
            new[] { false, true,  true,  false, false, false, false }, // 1
            new[] { true,  true,  false, true,  true,  false, true  }, // 2
            new[] { true,  true,  true,  true,  false, false, true  }, // 3
            new[] { false, true,  true,  false, false, true,  true  }, // 4
            new[] { true,  false, true,  true,  false, true,  true  }, // 5
            new[] { true,  false, true,  true,  true,  true,  true  }, // 6
            new[] { true,  true,  true,  false, false, false, false }, // 7
            new[] { true,  true,  true,  true,  true,  true,  true  }, // 8
            new[] { true,  true,  true,  true,  false, true,  true  }, // 9
        };

        /// <summary>The segment mask for one digit.</summary>
        public static bool[] Glyph(int digit) => Glyphs[Mathf.Clamp(digit, 0, 9)];

        /// <summary>
        /// Builds one digit cell's seven segments under <paramref name="parent"/>, all active, and
        /// returns them in segment order. The caller positions the cell and toggles the segments.
        /// </summary>
        public static GameObject[] BuildDigitCell(Transform parent, Material material)
        {
            float w = DigitWidth - 0.16f;          // glyph width, leaving a gap between cells
            float halfH = Height * 0.5f;
            float quarterH = Height * 0.25f;
            float halfW = w * 0.5f;

            var horizontal = new Vector3(w, Thickness, Depth);
            var vertical = new Vector3(Thickness, halfH, Depth);

            var segments = new GameObject[SegmentsPerDigit];
            segments[0] = MakeSegment(parent, new Vector3(0f, halfH, 0f), horizontal, material);
            segments[1] = MakeSegment(parent, new Vector3(halfW, quarterH, 0f), vertical, material);
            segments[2] = MakeSegment(parent, new Vector3(halfW, -quarterH, 0f), vertical, material);
            segments[3] = MakeSegment(parent, new Vector3(0f, -halfH, 0f), horizontal, material);
            segments[4] = MakeSegment(parent, new Vector3(-halfW, -quarterH, 0f), vertical, material);
            segments[5] = MakeSegment(parent, new Vector3(-halfW, quarterH, 0f), vertical, material);
            segments[6] = MakeSegment(parent, Vector3.zero, horizontal, material);
            return segments;
        }

        /// <summary>Builds a single bar, for a minus sign or any other one-segment mark.</summary>
        public static GameObject BuildBar(Transform parent, Material material) =>
            MakeSegment(parent, Vector3.zero, new Vector3(DigitWidth * 0.5f, Thickness, Depth), material);

        /// <summary>How many digits <paramref name="value"/> needs. Zero still needs one.</summary>
        public static int DigitCount(int value)
        {
            value = Mathf.Abs(value);
            return value == 0 ? 1 : Mathf.FloorToInt(Mathf.Log10(value)) + 1;
        }

        /// <summary>The digit at <paramref name="place"/>, counting 0 as the units column.</summary>
        public static int DigitAt(int value, int place) =>
            Mathf.Abs(value) / (int)Mathf.Pow(10, place) % 10;

        public static void Paint(Renderer renderer, Color color, MaterialPropertyBlock block)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        static GameObject MakeSegment(Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Seg";
            Object.Destroy(block.GetComponent<Collider>());

            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = scale;

            var renderer = block.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // CreatePrimitive hands out the built-in Standard material, which URP draws magenta.
            if (material != null)
                renderer.sharedMaterial = material;

            return block;
        }
    }
}
