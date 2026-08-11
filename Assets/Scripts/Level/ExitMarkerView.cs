using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The visible face of one room exit: a pair of doorframe pillars on the floor and a
    /// billboarded reward icon floating above them naming what waits behind the door — the
    /// reward type as a silhouette, the fork's tier as its tint (brass / silver / gold), and
    /// a fixed hostile mark on the one door that leads to the boss.
    ///
    /// <para>The icon itself is drawn through <see cref="IRewardPreviewRenderer"/>, because the
    /// final presentation is the Second Hand projecting each exit's incoming signal as a
    /// waveform; when that lands, the renderer component swaps and this file does not change.</para>
    ///
    /// <para>Icons are primitive blocks carrying the room's own URP material rather than
    /// sprites or TextMeshes — built-in shader paths get stripped by URP builds (the
    /// magenta-health-bar lesson), while scene materials cannot fail that way. The icon
    /// billboards to the camera exactly as <c>EnemyHealthBar</c> does; the pillars stay
    /// world-anchored since they are geometry, not signage.</para>
    /// </summary>
    public sealed class ExitMarkerView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Transform iconRoot;
        Camera view;
        bool focused;

        /// <summary>
        /// Marks this door as the one the Interact press would choose: the icon grows so the
        /// preview itself answers "which one am I about to pick".
        /// </summary>
        public void SetFocused(bool value)
        {
            if (focused == value)
                return;

            focused = value;
            if (iconRoot != null)
                iconRoot.localScale = Vector3.one * (value ? 1.35f : 1f);
        }

        /// <summary>
        /// Builds a marker beside/above a doorway. <paramref name="anchor"/> is the blocker's
        /// world position; the pillars flank it at floor level and the icon floats above it.
        /// <paramref name="renderer"/> draws the actual preview; passing null falls back to the
        /// capsule primitive renderer.
        /// </summary>
        public static ExitMarkerView Build(
            Transform parent, Vector3 anchor, float floorY, float doorwayWidth,
            RewardChoice choice, RewardDefinition definition, Color tierTint,
            IRewardPreviewRenderer renderer, Material material)
        {
            var go = new GameObject($"ExitMarker_{choice}");
            go.transform.SetParent(parent, false);
            go.transform.position = anchor;

            var marker = go.AddComponent<ExitMarkerView>();

            var pillarColor = new Color(0.85f, 0.82f, 0.72f, 1f);
            float half = doorwayWidth * 0.5f + 0.35f;
            MakePillar("Pillar_L", go.transform,
                new Vector3(-half, floorY - anchor.y + 1.75f, 0f), pillarColor, material);
            MakePillar("Pillar_R", go.transform,
                new Vector3(half, floorY - anchor.y + 1.75f, 0f), pillarColor, material);

            marker.iconRoot = new GameObject("RewardIcon").transform;
            marker.iconRoot.SetParent(go.transform, false);
            marker.iconRoot.localPosition = new Vector3(0f, 2.8f, 0f);

            if (renderer == null)
                renderer = go.AddComponent<CapsuleRewardPreviewRenderer>();

            renderer.ShowPreview(marker.iconRoot, choice, definition, tierTint, material);

            return marker;
        }

        static void MakePillar(string pillarName, Transform parent, Vector3 localPosition, Color color, Material material)
        {
            GameObject pillar = RewardIconBuilder.MakeBlock(
                parent, localPosition, new Vector3(0.35f, 3.5f, 0.35f), Quaternion.identity, color, material);
            pillar.name = pillarName;
        }

        void LateUpdate()
        {
            if (iconRoot == null)
                return;

            if (view == null)
                view = Camera.main;

            // Same billboard the health bars use: the camera is fixed-yaw, so this is a constant
            // rotation in practice but stays correct if the rig ever moves.
            if (view != null)
                iconRoot.rotation = view.transform.rotation;
        }
    }
}
