using System.Collections.Generic;

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
    /// A fork's quality band (human redesign 2026-08-11). The band decides what KIND of
    /// reward a fork can offer — not a multiplier on the same rewards:
    /// Basic is healing, run currency and consumables; Valuable is meta currency and Strays;
    /// Boon is a transmission draft; EliteBoon is the rare two-giver draft whose cards roll
    /// higher rarities. Boon rarity itself (Normal/Rare/Epic per card) is a separate roll made
    /// by the draft, not by the door.
    /// </summary>
    public enum RewardBand
    {
        /// <summary>Healing / run currency / consumables. Reads as brass.</summary>
        Basic = 0,

        /// <summary>Meta currency and Strays. Reads as silver.</summary>
        Valuable = 1,

        /// <summary>A transmission draft. Reads as gold.</summary>
        Boon = 2,

        /// <summary>Two givers on the channel at once, higher-rarity cards. Reads as purple.</summary>
        EliteBoon = 3,
    }

    /// <summary>
    /// One door's offer: what kind of help, at what quality band. Two special doors carry no
    /// reward: the boss door (the mark the player learns to read as "the boss is behind this
    /// one") and the level exit — the one door out of a beaten boss's arena, wearing the same
    /// glyph after every boss so leaving an era is always the same signal.
    /// </summary>
    public readonly struct RewardChoice
    {
        public readonly RewardType Type;
        public readonly RewardBand Band;
        public readonly bool IsBossDoor;
        public readonly bool IsLevelExit;

        /// <summary>
        /// For boon doors: WHICH giver's transmission waits behind this one, decided at
        /// generation so the door can wear the giver's colour and the choice is readable
        /// before entering (REWARDS.md §8's "readable WHICH giver"). The draft honours the
        /// pin unless that giver has nothing left to offer.
        /// </summary>
        public readonly Game.Combat.GiverId PinnedGiver;

        public readonly bool HasPinnedGiver;

        public RewardChoice(RewardType type, RewardBand band)
        {
            Type = type;
            Band = band;
            IsBossDoor = false;
            IsLevelExit = false;
            PinnedGiver = default;
            HasPinnedGiver = false;
        }

        public RewardChoice(RewardType type, RewardBand band, Game.Combat.GiverId pinnedGiver)
        {
            Type = type;
            Band = band;
            IsBossDoor = false;
            IsLevelExit = false;
            PinnedGiver = pinnedGiver;
            HasPinnedGiver = true;
        }

        RewardChoice(bool isBossDoor, bool isLevelExit)
        {
            Type = default;
            Band = default;
            IsBossDoor = isBossDoor;
            IsLevelExit = isLevelExit;
            PinnedGiver = default;
            HasPinnedGiver = false;
        }

        public static RewardChoice BossDoor => new RewardChoice(true, false);

        public static RewardChoice LevelExit => new RewardChoice(false, true);

        public override string ToString() =>
            IsBossDoor ? "BossDoor"
            : IsLevelExit ? "LevelExit"
            : HasPinnedGiver ? $"{Type}({Band}/{PinnedGiver})"
            : $"{Type}({Band})";
    }

    /// <summary>One reward type's generator knobs, as the engine-free roller sees them.</summary>
    public readonly struct RewardTypeOption
    {
        public readonly RewardType Type;
        public readonly RewardBand Band;
        public readonly bool Enabled;
        public readonly float Weight;

        public RewardTypeOption(RewardType type, RewardBand band, bool enabled, float weight)
        {
            Type = type;
            Band = band;
            Enabled = enabled;
            Weight = weight;
        }
    }

    /// <summary>Generator tuning, implemented by the config ScriptableObject and by test fakes.</summary>
    public interface IRewardConfig
    {
        /// <summary>Relative weight of a fork rolling each quality band.</summary>
        float BandWeight(RewardBand band);

        /// <summary>All reward types the generator knows, with their band, enable flag and weight.</summary>
        IReadOnlyList<RewardTypeOption> TypeOptions { get; }

        /// <summary>
        /// The givers a boon door may be pinned to — the implemented ones. A boon fork offers
        /// at least two doors, each pinned to a different giver from this list.
        /// </summary>
        IReadOnlyList<Game.Combat.GiverId> BoonGivers { get; }
    }
}
