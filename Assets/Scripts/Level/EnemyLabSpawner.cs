using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Rng;
using Game.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Level
{
    /// <summary>
    /// One archetype the lab can spawn, bound to a number key.
    /// </summary>
    [Serializable]
    public struct LabArchetype
    {
        [Tooltip("Shown in the on-screen legend.")]
        public string Label;

        [Tooltip("Enemy prefab. Needs an EnemyActor; a MovesetEnemyController gets a seeded stream.")]
        public GameObject Prefab;

        [Tooltip("How many to drop per press. Swiftjaw spawns in pairs; Scrapfeathers spawn as a flock.")]
        public int CountPerPress;
    }

    /// <summary>
    /// The Cretaceous test bench: spawn any archetype on a number key, spawn a scripted mixed
    /// wave, and wipe the arena — without generating a level or running a room.
    ///
    /// <para>ENEMIES_BIOME1.md § 7 puts the capsule graybox first and says the fight must be
    /// proven fun before any art exists. That needs an arena you can be standing in within two
    /// seconds of pressing play, with the enemy you are iterating on and nothing else.</para>
    ///
    /// <para>Deliberately one scene rather than one scene per enemy. Eight scenes that differ only
    /// in which prefab is pre-placed all drift apart the first time the arena floor, the token
    /// broker or the player rig changes, and then a Swiftjaw playtest and an Ambershell playtest
    /// are no longer measuring the same thing. Isolation is what matters and a key press gives
    /// it — press 1 for a Swiftjaw arena, press 0 for the mixed wave.</para>
    ///
    /// <para>Everything here logs at Warning level, exactly as <see cref="DebugCheats"/> does, so a
    /// lab log can never be mistaken for an honest run.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyLabSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("Bound to keys 1..9 in order.")]
        LabArchetype[] archetypes = new LabArchetype[0];

        [Header("Mixed wave (key 0)")]
        [SerializeField, Tooltip("Indices into the archetype list. ENEMIES_BIOME1.md 5 forbids a Scrapfeathers-only wave, so a mixed wave is authored rather than rolled.")]
        int[] mixedWaveArchetypes = new int[0];

        [Header("Placement")]
        [SerializeField] Transform player;
        [SerializeField, Tooltip("Enemies appear on a ring at this radius, far enough that their spawn grace is not the only thing protecting the player.")]
        float spawnRadius = 7f;
        [SerializeField, Tooltip("Parent for spawned bodies, so Clear can drop them all at once.")]
        Transform enemyParent;

        [Header("Keys")]
        [SerializeField] Key clearKey = Key.Backspace;
        [SerializeField] Key mixedWaveKey = Key.Digit0;

        [Header("Determinism")]
        [SerializeField, Tooltip("The lab has no RunContext, so it owns a seed of its own. The same seed replays the same fight.")]
        uint labSeed = 12345u;

        static readonly Key[] DigitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        readonly List<EnemyActor> spawned = new List<EnemyActor>();

        IRandomSource random;
        int spawnCounter;

        /// <summary>Live bodies, for the on-screen readout.</summary>
        public int AliveCount
        {
            get
            {
                Prune();
                return spawned.Count;
            }
        }

        void Awake()
        {
            random = new XorShiftRandom(labSeed);

            if (player == null)
            {
                GameObject found = GameObject.FindWithTag("Player");
                if (found != null)
                    player = found.transform;
            }

            if (enemyParent == null)
                enemyParent = transform;

            int bound = Mathf.Min(DigitKeys.Length, archetypes.Length);

            GameLog.Warn(LogCategory.Level,
                bound == 0
                    ? $"ENEMY LAB active - seed {labSeed}. No archetypes wired up yet, so there is nothing to spawn."
                    : $"ENEMY LAB active - seed {labSeed}. Keys 1-{bound} spawn, " +
                      $"{mixedWaveKey} mixed wave, {clearKey} clear.");
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[clearKey].wasPressedThisFrame)
                Clear();

            if (keyboard[mixedWaveKey].wasPressedThisFrame)
                SpawnMixedWave();

            for (int i = 0; i < DigitKeys.Length && i < archetypes.Length; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame)
                    SpawnArchetype(i);
            }
        }

        /// <summary>Drops one archetype's worth of bodies onto the ring.</summary>
        public void SpawnArchetype(int index)
        {
            if (index < 0 || index >= archetypes.Length)
                return;

            LabArchetype entry = archetypes[index];
            if (entry.Prefab == null)
            {
                GameLog.Warn(LogCategory.Level, $"lab slot {index + 1} ('{entry.Label}') has no prefab");
                return;
            }

            int count = Mathf.Max(1, entry.CountPerPress);
            for (int i = 0; i < count; i++)
                SpawnOne(entry.Prefab, entry.Label);

            GameLog.Warn(LogCategory.Level, $"DEBUG lab: spawned {count}x {entry.Label}");
        }

        /// <summary>Spawns the authored mixed wave — the "does this composition work" check.</summary>
        public void SpawnMixedWave()
        {
            if (mixedWaveArchetypes.Length == 0)
            {
                GameLog.Warn(LogCategory.Level, "lab: no mixed wave authored");
                return;
            }

            for (int i = 0; i < mixedWaveArchetypes.Length; i++)
                SpawnArchetype(mixedWaveArchetypes[i]);

            GameLog.Warn(LogCategory.Level, $"DEBUG lab: mixed wave of {mixedWaveArchetypes.Length} group(s)");
        }

        /// <summary>Removes every body the lab spawned. The arena, not the room, is the unit here.</summary>
        public void Clear()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                    Destroy(spawned[i].gameObject);
            }

            spawned.Clear();
            AttackTokenBroker.Current?.Pool.ReleaseAll();

            GameLog.Warn(LogCategory.Level, "DEBUG lab: cleared");
        }

        void SpawnOne(GameObject prefab, string label)
        {
            Vector3 center = player != null ? player.position : transform.position;

            // Spread around a ring by index rather than at random, so two Swiftjaws never land on
            // top of each other and a repeated press walks around the player instead of piling up.
            float angle = spawnCounter * 137.508f; // golden angle: successive spawns stay far apart
            spawnCounter++;

            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * spawnRadius;
            Vector3 position = new Vector3(center.x + offset.x, center.y, center.z + offset.z);

            GameObject instance = Instantiate(prefab, position, Quaternion.LookRotation(-offset.normalized, Vector3.up), enemyParent);

            var actor = instance.GetComponent<EnemyActor>();
            if (actor == null)
            {
                GameLog.Error(LogCategory.Level, $"lab prefab '{label}' has no EnemyActor");
                Destroy(instance);
                return;
            }

            // Same contract the real spawner honours: a multi-move enemy draws a variable number
            // of times, so it gets its own derived stream rather than sharing the lab's.
            var moveset = instance.GetComponent<MovesetEnemyController>();
            if (moveset != null)
                moveset.Bind(new XorShiftRandom(((XorShiftRandom)random).NextUInt()));

            spawned.Add(actor);
        }

        void Prune()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] == null || (!spawned[i].IsAlive && !spawned[i].IsDying))
                    spawned.RemoveAt(i);
            }
        }

        /// <summary>The legend, so the key bindings are readable without opening this file.</summary>
        public string DescribeBindings()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < archetypes.Length && i < DigitKeys.Length; i++)
                text.AppendLine($"{i + 1}  {archetypes[i].Label}");

            text.AppendLine($"0  mixed wave");
            text.AppendLine($"{clearKey}  clear");
            text.Append(AttackTokenBroker.DescribeUsage());
            return text.ToString();
        }
    }
}
