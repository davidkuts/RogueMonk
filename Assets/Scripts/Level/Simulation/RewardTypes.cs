using System.Collections.Generic;
using Game.Core.Economy;

namespace Game.Level
{
    /// <summary>
    /// Every kind of per-room reward a door can lead to (REWARDS.md §2).
    ///
    /// Recalibration and SupplyDrop exist for forward compatibility — the generator excludes
    /// them via their config entry's enabled flag until those systems are built. Do not remove
    /// them from the enum; serialized data will reference these values.
    /// </summary>
    public enum RewardType
    {
        /// <summary>3-choice complication draft from one giver (BOONS.md).</summary>
        Transmission = 0,

        /// <summary>Run-currency payout.</summary>
        MinutesCache = 1,

        /// <summary>Meta-currency payout (rarer).</summary>
        HoursCache = 2,

        /// <summary>Healing: rewind Cole's body, capped at the biome-entry snapshot (§3).</summary>
        Splice = 3,

        /// <summary>Passive trinket, one equipped at a time (§4).</summary>
        Stray = 4,

        /// <summary>Single-use carried consumable (§5). Grant/carry only for now.</summary>
        Stopgap = 5,

        /// <summary>NOT YET GENERATED. Upgrade one owned complication a tier (§6).</summary>
        Recalibration = 6,

        /// <summary>NOT YET GENERATED. The shopkeeperless shop (§7).</summary>
        SupplyDrop = 7,
    }

    /// <summary>
    /// One door's offer: what kind of help, at what quality. Two special doors carry no
    /// reward: the boss door (the mark the player learns to read as "the boss is behind this
    /// one") and the level exit — the one door out of a beaten boss's arena, wearing the same
    /// glyph after every boss so leaving an era is always the same signal.
    /// </summary>
    public readonly struct RewardChoice
    {
        public readonly RewardType Type;
        public readonly RewardTier Tier;
        public readonly bool IsBossDoor;
        public readonly bool IsLevelExit;

        public RewardChoice(RewardType type, RewardTier tier)
        {
            Type = type;
            Tier = tier;
            IsBossDoor = false;
            IsLevelExit = false;
        }

        RewardChoice(bool isBossDoor, bool isLevelExit)
        {
            Type = default;
            Tier = default;
            IsBossDoor = isBossDoor;
            IsLevelExit = isLevelExit;
        }

        public static RewardChoice BossDoor => new RewardChoice(true, false);

        public static RewardChoice LevelExit => new RewardChoice(false, true);

        public override string ToString() =>
            IsBossDoor ? "BossDoor" : IsLevelExit ? "LevelExit" : $"{Type}({Tier})";
    }

    /// <summary>One reward type's generator knobs, as the engine-free roller sees them.</summary>
    public readonly struct RewardTypeOption
    {
        public readonly RewardType Type;
        public readonly bool Enabled;
        public readonly float Weight;

        public RewardTypeOption(RewardType type, bool enabled, float weight)
        {
            Type = type;
            Enabled = enabled;
            Weight = weight;
        }
    }

    /// <summary>Generator tuning, implemented by the config ScriptableObject and by test fakes.</summary>
    public interface IRewardConfig
    {
        /// <summary>Relative weight of each fork rolling Normal / Rare / Epic.</summary>
        float TierWeight(RewardTier tier);

        /// <summary>All reward types the generator knows, with their enable flags and weights.</summary>
        IReadOnlyList<RewardTypeOption> TypeOptions { get; }
    }
}
