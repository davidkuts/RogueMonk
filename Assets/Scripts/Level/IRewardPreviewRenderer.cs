using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Renders one door's reward preview. All door preview drawing goes through this seam so
    /// the final presentation — the Second Hand projecting each exit's incoming signal as a
    /// waveform (REWARDS.md §8) — swaps in without touching door or reward logic. The capsule
    /// phase ships <see cref="CapsuleRewardPreviewRenderer"/>.
    /// </summary>
    public interface IRewardPreviewRenderer
    {
        /// <summary>
        /// Draws the preview for <paramref name="choice"/> under <paramref name="anchor"/>.
        /// <paramref name="definition"/> is null for the boss door, which has no reward.
        /// </summary>
        void ShowPreview(Transform anchor, RewardChoice choice, RewardDefinition definition, Color tierTint, Material material);
    }

    /// <summary>
    /// Capsule-phase preview: the reward type's primitive icon silhouette, tinted with the
    /// fork's tier colour (brass / silver / gold).
    /// </summary>
    public sealed class CapsuleRewardPreviewRenderer : MonoBehaviour, IRewardPreviewRenderer
    {
        public void ShowPreview(Transform anchor, RewardChoice choice, RewardDefinition definition, Color tierTint, Material material)
        {
            if (choice.IsBossDoor)
            {
                RewardIconBuilder.Build(anchor, RewardIconShape.BossMark, RewardIconBuilder.BossMarkColor, material);
                return;
            }

            RewardIconShape shape = definition != null ? definition.IconShape : RewardIconShape.Coin;
            RewardIconBuilder.Build(anchor, shape, tierTint, material);
        }
    }
}
