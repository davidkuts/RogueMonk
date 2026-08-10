using Game.Core.Economy;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Decides the reward that greets the player in the first room of a run, before any
    /// enemy exists (PROMPT_REWARDS Phase 6). A policy object rather than a constant because
    /// the spec requires this default to be modifiable later by items or story flags — a
    /// different policy is a different asset in the config's inspector slot, not a code edit.
    ///
    /// The default: always a Transmission (the draft rolls its own random giver), at a fixed
    /// tier. Subclass and override <see cref="Decide"/> for anything cleverer.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/First Room Reward Policy", fileName = "FirstRoomRewardPolicy")]
    public class FirstRoomRewardPolicy : ScriptableObject
    {
        [SerializeField, Tooltip("Reward type waiting in the run's first room.")]
        RewardType type = RewardType.Transmission;
        [SerializeField, Tooltip("Its tier. The tier-parity generator is bypassed here by design.")]
        RewardTier tier = RewardTier.Normal;

        public virtual RewardChoice Decide(IRandomSource random) => new RewardChoice(type, tier);
    }
}
