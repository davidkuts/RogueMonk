using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Builds the capsule-phase reward icons out of primitive blocks, one shape per reward
    /// type, tinted with the fork's tier colour. Shared by the door markers and the room
    /// pickup so a door's promise and the thing that then appears are visibly the same object.
    ///
    /// <para>Primitives carrying a URP material rather than sprites or TextMeshes, for the
    /// reason ExitMarkerView documents: built-in shader paths get stripped by URP builds (the
    /// magenta-health-bar lesson), while a shared scene material cannot fail that way.</para>
    /// </summary>
    public static class RewardIconBuilder
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The boss door carries no reward, so its mark keeps a fixed hostile tint.</summary>
        public static readonly Color BossMarkColor = new Color(0.75f, 0.25f, 0.20f);

        /// <summary>
        /// Builds one icon under <paramref name="parent"/>, centred on its local origin, about
        /// one unit tall. The caller owns placement, billboarding and any spin.
        /// </summary>
        public static void Build(Transform parent, RewardIconShape shape, Color tint, Material material)
        {
            switch (shape)
            {
                case RewardIconShape.Waveform:
                    float[] heights = { 0.45f, 0.85f, 1.25f, 0.7f, 0.5f };
                    for (int i = 0; i < heights.Length; i++)
                    {
                        MakeBlock(parent, new Vector3((i - 2) * 0.24f, 0f, 0f),
                            new Vector3(0.13f, heights[i], 0.13f), Quaternion.identity, tint, material);
                    }

                    break;

                case RewardIconShape.Coin:
                    MakeDisc(parent, Vector3.zero, 0.95f, tint, material);
                    break;

                case RewardIconShape.Ring:
                    const int segments = 10;
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / segments;
                        MakeBlock(parent, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.55f,
                            new Vector3(0.2f, 0.2f, 0.13f), Quaternion.identity, tint, material);
                    }

                    break;

                case RewardIconShape.Cross:
                    MakeBlock(parent, Vector3.zero, new Vector3(1.1f, 0.28f, 0.13f), Quaternion.identity, tint, material);
                    MakeBlock(parent, Vector3.zero, new Vector3(0.28f, 1.1f, 0.13f), Quaternion.identity, tint, material);
                    break;

                case RewardIconShape.Asterisk:
                    for (int i = 0; i < 3; i++)
                    {
                        MakeBlock(parent, Vector3.zero, new Vector3(1.1f, 0.18f, 0.13f),
                            Quaternion.Euler(0f, 0f, i * 60f), tint, material);
                    }

                    break;

                case RewardIconShape.Spark:
                    MakeBlock(parent, Vector3.zero, new Vector3(0.55f, 0.55f, 0.13f),
                        Quaternion.Euler(0f, 0f, 45f), tint, material);
                    break;

                case RewardIconShape.BossMark:
                    MakeBlock(parent, new Vector3(-0.3f, -0.15f, 0f), new Vector3(0.2f, 0.7f, 0.13f), Quaternion.identity, tint, material);
                    MakeBlock(parent, new Vector3(0f, 0.1f, 0f), new Vector3(0.2f, 1.2f, 0.13f), Quaternion.identity, tint, material);
                    MakeBlock(parent, new Vector3(0.3f, -0.15f, 0f), new Vector3(0.2f, 0.7f, 0.13f), Quaternion.identity, tint, material);
                    break;
            }
        }

        public static GameObject MakeBlock(
            Transform parent, Vector3 localPosition, Vector3 scale, Quaternion localRotation,
            Color color, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "IconBlock";
            Object.Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = localRotation;
            block.transform.localScale = scale;
            Paint(block, color, material);
            return block;
        }

        static void MakeDisc(Transform parent, Vector3 localPosition, float diameter, Color color, Material material)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "IconDisc";
            Object.Destroy(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = localPosition;

            // A cylinder's axis is local Y; laying it on X presents the flat face forward.
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale = new Vector3(diameter, 0.05f, diameter);
            Paint(disc, color, material);
        }

        static void Paint(GameObject go, Color color, Material material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // CreatePrimitive assigns the built-in Standard material, which URP draws magenta.
            if (material != null)
                renderer.sharedMaterial = material;

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
