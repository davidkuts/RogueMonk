using UnityEngine;

namespace Game.Core.Audio
{
    /// <summary>Every one-shot the game can play. Named by event, not by file.</summary>
    public enum GameSound
    {
        Whiff = 0,
        HitLight = 1,
        HitHeavy = 2,
        Dash = 3,
        PerfectDodge = 4,
        EnemyDeath = 5,
        PlayerHurt = 6,
        RoomClear = 7,

        /// <summary>The counter-attack a perfect dodge unlocks. Deliberately the loudest thing the player can make happen.</summary>
        Riposte = 8,
    }

    /// <summary>
    /// Maps gameplay events to clips and their mix. Data, so replacing the placeholder
    /// synthesised sounds with real ones is an asset swap and nothing more.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Sound Set", fileName = "SoundSet")]
    public sealed class SoundSet : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public GameSound Sound;
            public AudioClip Clip;

            [Range(0f, 1f)] public float Volume;

            [Tooltip("Random pitch spread. Small amounts stop repeated hits sounding mechanical.")]
            [Range(0f, 0.5f)] public float PitchVariance;

            [Tooltip("Minimum gap between repeats, so a multi-hit frame does not stack into a wall of noise.")]
            public float MinIntervalSeconds;
        }

        [SerializeField]
        Entry[] entries = new Entry[0];

        public bool TryGet(GameSound sound, out Entry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Sound == sound && entries[i].Clip != null)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
