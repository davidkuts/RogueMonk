using Game.Core.Economy;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The placeholder icon silhouette for one reward type. Final art replaces all of this
    /// with the Second Hand's waveform projection; the shapes only have to be distinct at a
    /// glance from the gameplay camera until then.
    /// </summary>
    public enum RewardIconShape
    {
        /// <summary>A giver's frequency signature: vertical waveform bars.</summary>
        Waveform = 0,

        /// <summary>Dense tick cluster: a coin disc.</summary>
        Coin = 1,

        /// <summary>Slow deep pulse: a ring of blocks.</summary>
        Ring = 2,

        /// <summary>Flatline stabilizing: a cross.</summary>
        Cross = 3,

        /// <summary>Foreign warble: a six-armed asterisk.</summary>
        Asterisk = 4,

        /// <summary>Sharp single spike: a small diamond.</summary>
        Spark = 5,

        /// <summary>The boss door's mark: three rising spikes.</summary>
        BossMark = 6,
    }

    /// <summary>
    /// One reward type's content: payload numbers per tier, and how its pickup/door icon
    /// reads. Tier scales the payload number only — never what the reward does (project
    /// rarity rule). What the payload MEANS depends on the type: Minutes for a MinutesCache,
    /// Hours for an HoursCache, heal fraction (0..1 of max HP) for a Splice; Transmission,
    /// Stray and Stopgap ignore it — their content comes from their own systems.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Reward Definition", fileName = "RewardDefinition")]
    public sealed class RewardDefinition : ScriptableObject
    {
        [SerializeField] RewardType type = RewardType.MinutesCache;

        [Header("Payload per tier (meaning depends on type)")]
        [SerializeField, Tooltip("Normal-tier payload. Minutes / Hours amount for caches, heal fraction of max HP for Splice.")]
        float payloadNormal = 25f;
        [SerializeField] float payloadRare = 40f;
        [SerializeField] float payloadEpic = 60f;

        [Header("Presentation (capsule phase)")]
        [SerializeField, Tooltip("Placeholder icon silhouette shown over doors and on the pickup.")]
        RewardIconShape iconShape = RewardIconShape.Coin;
        [SerializeField, Tooltip("Optional final-art sprite. Unused by the capsule renderer; reserved for the waveform pass.")]
        Sprite iconSprite;
        [SerializeField, Tooltip("Optional final-art pickup prefab. Null spawns the capsule primitive pickup.")]
        GameObject spawnPrefab;

        public RewardType Type => type;
        public RewardIconShape IconShape => iconShape;
        public Sprite IconSprite => iconSprite;
        public GameObject SpawnPrefab => spawnPrefab;

        public float PayloadFor(RewardTier tier)
        {
            switch (tier)
            {
                case RewardTier.Epic: return payloadEpic;
                case RewardTier.Rare: return payloadRare;
                default: return payloadNormal;
            }
        }
    }
}
