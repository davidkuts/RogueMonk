using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Synthesises placeholder sound effects as real .wav assets.
    ///
    /// Exists because sound files cannot be authored or downloaded from inside the project,
    /// and silent placeholders would leave the whole audio path unverifiable. These are
    /// deliberately crude — swap the files and everything keeps working, because the game only
    /// ever references them through the SoundSet asset.
    ///
    /// Menu: Monk / Regenerate Placeholder SFX
    /// </summary>
    public static class PlaceholderSfxGenerator
    {
        const int SampleRate = 44100;
        const string OutputFolder = "Assets/Audio/Placeholder";

        [MenuItem("Monk/Regenerate Placeholder SFX")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);

            // Each sound is a short envelope over a simple oscillator plus noise. The point is
            // that they are distinguishable from each other, not that they sound good.
            Write("whiff", Whiff());
            Write("hit_light", Impact(220f, 0.12f, 0.55f));
            Write("hit_heavy", Impact(120f, 0.22f, 0.85f));
            Write("dash", Dash());
            Write("perfect_dodge", Chime());
            Write("enemy_death", Impact(90f, 0.35f, 0.7f, noise: 0.5f));
            Write("player_hurt", Impact(300f, 0.18f, 0.7f, noise: 0.35f));
            Write("room_clear", Fanfare());

            AssetDatabase.Refresh();
            Debug.Log($"[Monk] Regenerated placeholder SFX in {OutputFolder}");
        }

        static float[] Whiff()
        {
            // Filtered noise swelling then cutting: reads as a swing through air.
            int length = (int)(SampleRate * 0.18f);
            var data = new float[length];
            float last = 0f;
            var rng = new System.Random(11);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(last, noise, 0.08f);      // crude low-pass
                float envelope = Mathf.Sin(t * Mathf.PI);   // swell in and out
                data[i] = last * envelope * 0.5f;
            }

            return data;
        }

        static float[] Impact(float baseHz, float seconds, float volume, float noise = 0.25f)
        {
            int length = (int)(SampleRate * seconds);
            var data = new float[length];
            var rng = new System.Random((int)baseHz);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                // Pitch drops across the hit, which is what makes it read as a thud.
                float hz = Mathf.Lerp(baseHz, baseHz * 0.45f, t);
                float tone = Mathf.Sin(2f * Mathf.PI * hz * (i / (float)SampleRate));
                float crackle = (float)(rng.NextDouble() * 2.0 - 1.0) * noise * (1f - t);
                float envelope = Mathf.Exp(-6f * t);        // sharp attack, fast decay
                data[i] = (tone + crackle) * envelope * volume;
            }

            return data;
        }

        static float[] Dash()
        {
            // Upward sweep: air moving past, ending abruptly.
            int length = (int)(SampleRate * 0.22f);
            var data = new float[length];
            float last = 0f;
            var rng = new System.Random(7);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(last, noise, 0.05f + t * 0.25f);   // opens up over time
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 520f, t) * (i / (float)SampleRate));
                float envelope = Mathf.Exp(-4f * t);
                data[i] = (last * 0.7f + tone * 0.3f) * envelope * 0.5f;
            }

            return data;
        }

        static float[] Chime()
        {
            // Two clean stacked partials: rewarding, and unmistakably not an impact.
            int length = (int)(SampleRate * 0.5f);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float seconds = i / (float)SampleRate;
                float a = Mathf.Sin(2f * Mathf.PI * 880f * seconds);
                float b = Mathf.Sin(2f * Mathf.PI * 1320f * seconds) * 0.6f;
                float envelope = Mathf.Exp(-5f * t);
                data[i] = (a + b) * envelope * 0.35f;
            }

            return data;
        }

        static float[] Fanfare()
        {
            // Three rising notes for a cleared room.
            float[] notes = { 523.25f, 659.25f, 783.99f };
            int noteLength = (int)(SampleRate * 0.16f);
            var data = new float[noteLength * notes.Length];

            for (int n = 0; n < notes.Length; n++)
            {
                for (int i = 0; i < noteLength; i++)
                {
                    float t = i / (float)noteLength;
                    float seconds = i / (float)SampleRate;
                    float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * seconds);
                    data[n * noteLength + i] = tone * Mathf.Exp(-4f * t) * 0.35f;
                }
            }

            return data;
        }

        static void Write(string name, float[] samples)
        {
            string path = Path.Combine(OutputFolder, name + ".wav");
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            int dataBytes = samples.Length * 2;   // 16-bit mono

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                       // PCM header size
            writer.Write((short)1);                 // PCM
            writer.Write((short)1);                 // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);           // byte rate
            writer.Write((short)2);                 // block align
            writer.Write((short)16);                // bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
        }
    }
}
