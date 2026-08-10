using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Splice's arithmetic (REWARDS.md §3), pure so it can be tested without a scene: a
    /// Splice restores a fraction of max HP but can never rewind Cole past the biome-entry
    /// snapshot — healing is naturally capped per biome without a potion economy.
    /// </summary>
    public static class SpliceMath
    {
        /// <summary>
        /// The health value after a splice. <paramref name="depthFraction"/> is the tier's
        /// rewind fraction of <paramref name="maxHealth"/>, already including any Stray depth
        /// bonus. The ceiling is the biome-entry snapshot; a splice never LOWERS health, so a
        /// player somehow above the snapshot simply gains nothing.
        /// </summary>
        public static float Heal(float current, float maxHealth, float depthFraction, float biomeEntryCeiling)
        {
            float healed = current + Mathf.Max(0f, depthFraction) * Mathf.Max(0f, maxHealth);
            float ceiling = Mathf.Min(Mathf.Max(biomeEntryCeiling, current), maxHealth);
            return Mathf.Min(healed, ceiling);
        }
    }
}
