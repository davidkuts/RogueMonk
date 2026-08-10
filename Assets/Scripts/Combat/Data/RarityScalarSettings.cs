using Game.Core.Economy;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The one place rarity becomes a number (BOONS.md §3): Normal 1.0×, Rare 1.5×,
    /// Epic 2.25× by default. Rarity scales numbers only, never mechanics — so this asset is
    /// the entire difference between a Normal boon and an Epic one, and retuning a whole tier
    /// is one field.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Rarity Scalar Settings", fileName = "RarityScalars")]
    public sealed class RarityScalarSettings : ScriptableObject
    {
        [SerializeField] float normalScalar = 1f;
        [SerializeField] float rareScalar = 1.5f;
        [SerializeField] float epicScalar = 2.25f;

        public float Scalar(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Epic: return epicScalar;
                case RewardTier.Rare: return rareScalar;
                default: return normalScalar;
            }
        }
    }
}
