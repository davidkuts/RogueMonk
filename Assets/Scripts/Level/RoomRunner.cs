using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Enemies;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Runs one room's fight: spawn a wave, wait for it to be cleared, spawn the next, then
    /// open the door. Tracks enemies by subscribing to their death rather than polling, so a
    /// room cannot be left permanently locked by an enemy that died in an unexpected way.
    /// </summary>
    public sealed class RoomRunner
    {
        readonly RoomInstance room;
        readonly RoomPlan plan;
        readonly LevelGenerationSettings settings;
        readonly Transform enemyParent;

        readonly List<EnemyActor> alive = new List<EnemyActor>();

        int waveIndex = -1;

        public RoomRunner(RoomInstance room, RoomPlan plan, LevelGenerationSettings settings, Transform enemyParent)
        {
            this.room = room;
            this.plan = plan;
            this.settings = settings;
            this.enemyParent = enemyParent;
        }

        public bool IsCleared { get; private set; }

        public int AliveCount => alive.Count;

        public int WaveNumber => waveIndex + 1;

        public int WaveCount => plan != null ? plan.Waves.Count : 0;

        /// <summary>Raised once every wave is dead.</summary>
        public event Action Cleared;

        /// <summary>Raised whenever a new wave lands, with its 1-based number.</summary>
        public event Action<int> WaveStarted;

        /// <summary>Raised for each enemy death, so the run can keep a kill tally.</summary>
        public event Action EnemyKilled;

        public void Begin()
        {
            GameLog.Info(LogCategory.Level,
                $"{(plan.IsBossRoom ? "BOSS ROOM" : "ROOM")} {plan.Index + 1} start  template {plan.TemplateId}  " +
                $"{plan.Waves.Count} wave(s), {plan.TotalEnemies} enemies");
            room.SetDoorOpen(false);
            AdvanceWave();
        }

        void AdvanceWave()
        {
            waveIndex++;

            if (waveIndex >= plan.Waves.Count)
            {
                IsCleared = true;
                room.SetDoorOpen(true);
                GameLog.Info(LogCategory.Level,
                    plan.IsBossRoom
                        ? $"BOSS ROOM {plan.Index + 1} CLEARED"
                        : $"ROOM {plan.Index + 1} CLEARED - door open");
                Cleared?.Invoke();
                return;
            }

            WavePlan wave = plan.Waves[waveIndex];
            GameLog.Info(LogCategory.Level,
                $"wave {waveIndex + 1}/{plan.Waves.Count} in room {plan.Index + 1}: {wave.Count} enemies");

            for (int i = 0; i < wave.Spawns.Count; i++)
                Spawn(wave.Spawns[i]);

            // A wave whose spawns all failed would otherwise hang the room forever.
            if (alive.Count == 0)
            {
                GameLog.Warn(LogCategory.Level, $"wave {waveIndex + 1} spawned nothing - skipping so the room cannot hang");
                AdvanceWave();
            }
            else
            {
                WaveStarted?.Invoke(waveIndex + 1);
            }
        }

        void Spawn(SpawnAssignment assignment)
        {
            EnemyArchetypeDefinition archetype = settings.FindArchetype(assignment.ArchetypeId);
            if (archetype == null || archetype.Prefab == null)
            {
                GameLog.Error(LogCategory.Level, $"archetype '{assignment.ArchetypeId}' has no prefab - spawn skipped");
                return;
            }

            Transform point = room.GetSpawnPoint(assignment.SpawnPointIndex);
            GameObject instance = UnityEngine.Object.Instantiate(
                archetype.Prefab, point.position, point.rotation, enemyParent);

            var actor = instance.GetComponent<EnemyActor>();
            if (actor == null)
            {
                GameLog.Error(LogCategory.Level, $"archetype '{assignment.ArchetypeId}' prefab has no EnemyActor");
                return;
            }

            alive.Add(actor);
            actor.Died += () => OnEnemyDied(actor);
        }

        void OnEnemyDied(EnemyActor actor)
        {
            alive.Remove(actor);
            EnemyKilled?.Invoke();
            PruneAndCheckCleared();
        }

        /// <summary>
        /// Drops anything destroyed or dead that never raised its event, then advances if the
        /// wave is empty. Insurance: without it, one enemy removed by any route other than its
        /// own Died event would lock the room shut forever.
        /// </summary>
        public void PruneAndCheckCleared()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] == null || !alive[i].IsAlive)
                    alive.RemoveAt(i);
            }

            if (alive.Count == 0 && !IsCleared)
                AdvanceWave();
        }

        /// <summary>
        /// Debug aid: ends the room immediately, skipping any remaining waves, and raises
        /// <see cref="Cleared"/> exactly as a real clear would — so the door, the run stats and
        /// the room transition all follow the normal path rather than a special case.
        /// </summary>
        public void ForceClear()
        {
            if (IsCleared)
                return;

            GameLog.Warn(LogCategory.Level,
                $"FORCE CLEAR room {plan.Index + 1} (debug) - skipped {plan.Waves.Count - WaveNumber} remaining wave(s)");

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null && alive[i].IsAlive)
                    alive[i].Health.TakeDamage(float.MaxValue);
            }

            alive.Clear();
            waveIndex = plan.Waves.Count - 1;
            AdvanceWave();
        }

        /// <summary>Drops any survivors, for aborting a run.</summary>
        public void Abort()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null)
                    UnityEngine.Object.Destroy(alive[i].gameObject);
            }

            alive.Clear();
        }
    }
}
