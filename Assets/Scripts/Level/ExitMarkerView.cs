using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The visible face of one room exit: a pair of doorframe pillars on the floor and a
    /// number floating above them naming the reward behind the door (placeholder digits for
    /// now — "1"/"2" on an ordinary choice, "9" when the Tyrant is next).
    ///
    /// <para>The digits are built from flattened cubes as seven-segment glyphs rather than a
    /// TextMesh. The legacy text path depends on a built-in font material whose shader URP may
    /// strip from a build — the exact failure mode that turned the first health bars magenta —
    /// while primitive cubes carrying the room's own URP material cannot fail that way.</para>
    ///
    /// <para>The number billboards to the camera exactly as <c>EnemyHealthBar</c> does; the
    /// pillars stay world-anchored since they are geometry, not signage.</para>
    /// </summary>
    public sealed class ExitMarkerView : MonoBehaviour
    {
        // Segment bits: 0=top, 1=top-right, 2=bottom-right, 3=bottom, 4=bottom-left, 5=top-left, 6=middle.
        static readonly int[] DigitSegments =
        {
            0b0111111, // 0
            0b0000110, // 1
            0b1011011, // 2
            0b1001111, // 3
            0b1100110, // 4
            0b1101101, // 5
            0b1111101, // 6
            0b0000111, // 7
            0b1111111, // 8
            0b1101111, // 9
        };

        const float DigitHeight = 1.1f;
        const float DigitWidth = 0.6f;
        const float SegmentThickness = 0.14f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Transform numberRoot;
        Camera view;

        /// <summary>
        /// Builds a marker beside/above a doorway. <paramref name="anchor"/> is the blocker's
        /// world position; the pillars flank it at floor level and the number floats above it.
        /// </summary>
        public static ExitMarkerView Build(
            Transform parent, Vector3 anchor, float floorY, float doorwayWidth, int number, Material material)
        {
            var go = new GameObject($"ExitMarker_{number}");
            go.transform.SetParent(parent, false);
            go.transform.position = anchor;

            var marker = go.AddComponent<ExitMarkerView>();

            var pillarColor = new Color(0.85f, 0.82f, 0.72f, 1f);
            float half = doorwayWidth * 0.5f + 0.35f;
            marker.MakeBlock("Pillar_L", go.transform,
                new Vector3(-half, floorY - anchor.y + 1.75f, 0f), new Vector3(0.35f, 3.5f, 0.35f), pillarColor, material);
            marker.MakeBlock("Pillar_R", go.transform,
                new Vector3(half, floorY - anchor.y + 1.75f, 0f), new Vector3(0.35f, 3.5f, 0.35f), pillarColor, material);

            marker.numberRoot = new GameObject("Number").transform;
            marker.numberRoot.SetParent(go.transform, false);
            marker.numberRoot.localPosition = new Vector3(0f, 2.6f, 0f);
            marker.BuildNumber(number, material);

            return marker;
        }

        void BuildNumber(int number, Material material)
        {
            if (number < 0)
                number = 0;

            // Digits laid out right to left so multi-digit numbers read correctly.
            var bright = new Color(0.98f, 0.96f, 0.88f, 1f);
            float step = DigitWidth + 0.35f;
            int remaining = number;
            int index = 0;

            do
            {
                int digit = remaining % 10;
                remaining /= 10;

                var digitRoot = new GameObject($"Digit_{digit}").transform;
                digitRoot.SetParent(numberRoot, false);
                digitRoot.localPosition = new Vector3(-index * step, 0f, 0f);

                int segments = DigitSegments[digit];
                float w = DigitWidth, h = DigitHeight, t = SegmentThickness;
                var horizontal = new Vector3(w, t, t);
                var vertical = new Vector3(t, h * 0.5f - t * 0.5f, t);

                if ((segments & 1 << 0) != 0) MakeBlock("A", digitRoot, new Vector3(0f, h * 0.5f, 0f), horizontal, bright, material);
                if ((segments & 1 << 1) != 0) MakeBlock("B", digitRoot, new Vector3(w * 0.5f, h * 0.25f, 0f), vertical, bright, material);
                if ((segments & 1 << 2) != 0) MakeBlock("C", digitRoot, new Vector3(w * 0.5f, -h * 0.25f, 0f), vertical, bright, material);
                if ((segments & 1 << 3) != 0) MakeBlock("D", digitRoot, new Vector3(0f, -h * 0.5f, 0f), horizontal, bright, material);
                if ((segments & 1 << 4) != 0) MakeBlock("E", digitRoot, new Vector3(-w * 0.5f, -h * 0.25f, 0f), vertical, bright, material);
                if ((segments & 1 << 5) != 0) MakeBlock("F", digitRoot, new Vector3(-w * 0.5f, h * 0.25f, 0f), vertical, bright, material);
                if ((segments & 1 << 6) != 0) MakeBlock("G", digitRoot, Vector3.zero, horizontal, bright, material);

                index++;
            } while (remaining > 0);

            // Recentre so a multi-digit number hangs symmetrically over the doorway.
            float width = (index - 1) * step;
            numberRoot.localPosition += new Vector3(width * 0.5f, 0f, 0f);
        }

        void MakeBlock(string blockName, Transform parent, Vector3 localPosition, Vector3 scale, Color color, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = scale;

            var renderer = block.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // CreatePrimitive assigns the built-in Standard material, which URP draws as magenta.
            if (material != null)
                renderer.sharedMaterial = material;

            var block2 = new MaterialPropertyBlock();
            block2.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block2);
        }

        void LateUpdate()
        {
            if (numberRoot == null)
                return;

            if (view == null)
                view = Camera.main;

            // Same billboard the health bars use: the camera is fixed-yaw, so this is a constant
            // rotation in practice but stays correct if the rig ever moves.
            if (view != null)
                numberRoot.rotation = view.transform.rotation;
        }
    }
}
