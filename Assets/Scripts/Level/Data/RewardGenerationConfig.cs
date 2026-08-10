using System;
using System.Collections.Generic;
using Game.Core.Economy;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Every knob of the door-reward system in one asset, so a playtest retune never touches
    /// code: tier weights, per-type weights and enable flags, the content definitions, tier
    /// tint colours and the pickup interaction radius. Referenced by
    /// <see cref="LevelGenerationSettings"/> for generation and by the reward director for
    /// presentation and effects.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Reward Generation Config", fileName = "RewardGenerationConfig")]
    public sealed class RewardGenerationConfig : ScriptableObject, IRewardConfig
    {
        [Serializable]
        public sealed class TypeEntry
        {
            [Tooltip("The content definition for this type: payloads per tier, icon shape.")]
            public RewardDefinition definition;
            [Tooltip("Excluded from generation while off. Recalibration and SupplyDrop ship disabled.")]
            public bool enabled = true;
            [Tooltip("Relative likelihood of appearing on a fork once its tier is rolled.")]
            public float weight = 1f;
        }

        [Header("Tier weights (one roll per fork — REWARDS.md §8)")]
        [SerializeField] float normalWeight = 6f;
        [SerializeField] float rareWeight = 3f;
        [SerializeField] float epicWeight = 1f;

        [Header("Reward types")]
        [SerializeField] List<TypeEntry> types = new List<TypeEntry>();

        [Header("Tier presentation (brass / silver / gold)")]
        [SerializeField] Color normalTint = new Color(0.78f, 0.58f, 0.30f);
        [SerializeField] Color rareTint = new Color(0.80f, 0.84f, 0.90f);
        [SerializeField] Color epicTint = new Color(1.00f, 0.84f, 0.25f);

        [Header("Pickup")]
        [SerializeField, Tooltip("How close the player must stand for the prompt and the collect press to work.")]
        float pickupRadius = 2.5f;

        [Header("First room")]
        [SerializeField, Tooltip("Decides the run's first-room reward. Swap the asset to change the rule — items and story flags will want to.")]
        FirstRoomRewardPolicy firstRoomPolicy;

        readonly List<RewardTypeOption> optionView = new List<RewardTypeOption>();

        public float PickupRadius => Mathf.Max(0.5f, pickupRadius);

        public FirstRoomRewardPolicy FirstRoomPolicy => firstRoomPolicy;

        public float TierWeight(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Epic: return epicWeight;
                case RewardTier.Rare: return rareWeight;
                default: return normalWeight;
            }
        }

        public Color TierTint(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Epic: return epicTint;
                case RewardTier.Rare: return rareTint;
                default: return normalTint;
            }
        }

        public IReadOnlyList<RewardTypeOption> TypeOptions
        {
            get
            {
                optionView.Clear();
                for (int i = 0; i < types.Count; i++)
                {
                    TypeEntry entry = types[i];
                    if (entry != null && entry.definition != null)
                        optionView.Add(new RewardTypeOption(entry.definition.Type, entry.enabled, entry.weight));
                }

                return optionView;
            }
        }

        public RewardDefinition FindDefinition(RewardType type)
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != null && types[i].definition != null && types[i].definition.Type == type)
                    return types[i].definition;
            }

            return null;
        }
    }
}
