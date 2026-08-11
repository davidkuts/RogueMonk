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

        [Header("Band weights (one roll per fork — the band decides what KIND of doors appear)")]
        [SerializeField, Tooltip("Healing / run currency / consumables.")]
        float basicWeight = 5f;
        [SerializeField, Tooltip("Meta currency and Strays.")]
        float valuableWeight = 3f;
        [SerializeField, Tooltip("A transmission draft — the only door on its fork.")]
        float boonWeight = 3f;
        [SerializeField, Tooltip("The two-giver, higher-rarity draft. Deliberately rare.")]
        float eliteBoonWeight = 1f;

        [Header("Reward types")]
        [SerializeField] List<TypeEntry> types = new List<TypeEntry>();

        [Header("Band presentation (brass / silver / gold / purple)")]
        [SerializeField] Color basicTint = new Color(0.78f, 0.58f, 0.30f);
        [SerializeField] Color valuableTint = new Color(0.80f, 0.84f, 0.90f);
        [SerializeField] Color boonTint = new Color(1.00f, 0.84f, 0.25f);
        [SerializeField] Color eliteBoonTint = new Color(0.70f, 0.35f, 0.95f);

        [Header("Pickup")]
        [SerializeField, Tooltip("How close the player must stand for the prompt and the collect press to work.")]
        float pickupRadius = 2.5f;

        [Header("First room")]
        [SerializeField, Tooltip("Decides the run's first-room reward. Swap the asset to change the rule — items and story flags will want to.")]
        FirstRoomRewardPolicy firstRoomPolicy;

        readonly List<RewardTypeOption> optionView = new List<RewardTypeOption>();

        public float PickupRadius => Mathf.Max(0.5f, pickupRadius);

        public FirstRoomRewardPolicy FirstRoomPolicy => firstRoomPolicy;

        public float BandWeight(RewardBand band)
        {
            switch (band)
            {
                case RewardBand.EliteBoon: return eliteBoonWeight;
                case RewardBand.Boon: return boonWeight;
                case RewardBand.Valuable: return valuableWeight;
                default: return basicWeight;
            }
        }

        public Color BandTint(RewardBand band)
        {
            switch (band)
            {
                case RewardBand.EliteBoon: return eliteBoonTint;
                case RewardBand.Boon: return boonTint;
                case RewardBand.Valuable: return valuableTint;
                default: return basicTint;
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
                        optionView.Add(new RewardTypeOption(
                            entry.definition.Type, entry.definition.Band, entry.enabled, entry.weight));
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
