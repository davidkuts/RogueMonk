using System;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// A short rolling record of where the player was and how hurt they were.
    ///
    /// <para>Exists for the Stored Rewind Stopgap (REWARDS.md §5): "instant 2-second personal
    /// rewind (position + health)". A rewind needs a past to rewind to, and the game does not
    /// otherwise keep one.</para>
    ///
    /// <para>A fixed ring buffer, sampled on a timer rather than every frame. Two seconds of
    /// 60 fps samples would be 120 entries to answer one question with; sampling every 50 ms gives
    /// the same answer at a fortieth of the cost, and a rewind landing within 50 ms of the exact
    /// requested instant is a distinction the player cannot perceive.</para>
    ///
    /// <para>Engine-free apart from <c>Vector3</c>, so the buffer arithmetic — which is the part
    /// that gets subtly wrong — is testable.</para>
    /// </summary>
    public sealed class RewindHistory
    {
        public readonly struct Sample
        {
            public readonly float Time;
            public readonly Vector3 Position;
            public readonly float Health;

            public Sample(float time, Vector3 position, float health)
            {
                Time = time;
                Position = position;
                Health = health;
            }
        }

        readonly Sample[] samples;
        readonly float sampleIntervalSeconds;

        int count;
        int next;
        float elapsed;
        float nextSampleAt;

        /// <summary>How many samples are currently held. Caps at the buffer size.</summary>
        public int Count => count;

        /// <summary>The longest rewind this buffer can actually answer.</summary>
        public float CapacitySeconds => samples.Length * sampleIntervalSeconds;

        public RewindHistory(float windowSeconds, float sampleIntervalSeconds = 0.05f)
        {
            this.sampleIntervalSeconds = Mathf.Max(0.001f, sampleIntervalSeconds);

            // One spare slot so a full window is still answerable after the buffer wraps.
            int size = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(0.001f, windowSeconds) / this.sampleIntervalSeconds) + 1);
            samples = new Sample[size];
        }

        /// <summary>
        /// Advances the clock and records a sample when one is due. Callers may hand this every
        /// frame; it decides for itself when to actually store anything.
        /// </summary>
        public void Tick(float deltaTime, Vector3 position, float health)
        {
            if (deltaTime > 0f)
                elapsed += deltaTime;

            if (count > 0 && elapsed < nextSampleAt)
                return;

            nextSampleAt = elapsed + sampleIntervalSeconds;

            samples[next] = new Sample(elapsed, position, health);
            next = (next + 1) % samples.Length;
            if (count < samples.Length)
                count++;
        }

        /// <summary>
        /// The state closest to <paramref name="secondsAgo"/> before now.
        ///
        /// <para>Clamps to the oldest sample held rather than failing: a rewind used two seconds
        /// into a room should still rewind as far as it can, because refusing to fire — and
        /// consuming the item anyway — is the worst possible answer for a panic button.</para>
        /// </summary>
        public bool TrySample(float secondsAgo, out Sample sample)
        {
            sample = default;
            if (count == 0)
                return false;

            float target = elapsed - Mathf.Max(0f, secondsAgo);

            // Walk back from newest to oldest and take the first sample at or before the target.
            // Oldest wins if the target predates everything held.
            Sample best = default;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                int index = (next - 1 - i + samples.Length * 2) % samples.Length;
                Sample candidate = samples[index];

                best = candidate;
                found = true;

                if (candidate.Time <= target)
                    break;
            }

            sample = best;
            return found;
        }

        /// <summary>Drops everything. Used on death, room transitions and run start.</summary>
        public void Clear()
        {
            count = 0;
            next = 0;
            elapsed = 0f;
            nextSampleAt = 0f;
            Array.Clear(samples, 0, samples.Length);
        }
    }
}
