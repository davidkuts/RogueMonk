using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Rng;
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
        readonly RunContext run;
        readonly Transform playerTransform;

        readonly List<EnemyActor> alive = new List<EnemyActor>();

        int waveIndex = -1;
        bool aborted;

        /// <summary>
        /// How close a planned spawn point may be to the player before it is considered unfair.
        /// Roughly the grunt's attack reach plus a dodge, so an enemy never appears already inside
        /// its own striking distance.
        /// </summary>
        const float minSpawnDistanceFromPlayer = 5f;

        public RoomRunner(
            RoomInstance room,
            RoomPlan plan,
            LevelGenerationSettings settings,
            Transform enemyParent,
            RunContext run = null,
            Transform playerTransform = null)
        {
            this.room = room;
            this.plan = plan;
            this.settings = settings;
            this.enemyParent = enemyParent;
            this.run = run;
            this.playerTransform = playerTransform;
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

        /// <summary>Raised when a boss enters play, so the HUD can put its bar up.</summary>
        public event Action<IBossEncounter> BossSpawned;

        /// <summary>Raised when the boss goes down, so the HUD can take the bar away.</summary>
        public event Action BossDefeated;

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
                Spawn(ReplaceUnfairSpawnPoint(wave.Spawns[i], wave));

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

        /// <summary>
        /// Moves a spawn that would drop an enemy in the player's lap to the furthest free point in
        /// the room. Anything at a fair distance is left exactly where the plan put it.
        ///
        /// The generated plan stays the authority on <em>what</em> spawns and <em>how many</em>, so
        /// a seed still reproduces the same fight. Only the position of a spawn that would have
        /// materialised on top of the player moves, and only because it had to — that is the one
        /// case where honouring the plan costs the player a hit they could never have read.
        /// </summary>
        SpawnAssignment ReplaceUnfairSpawnPoint(SpawnAssignment assignment, WavePlan wave)
        {
            Transform player = playerTransform;
            if (player == null || room == null)
                return assignment;

            Transform planned = room.GetSpawnPoint(assignment.SpawnPointIndex);
            if (planned == null || PlanarDistance(planned.position, player.position) >= minSpawnDistanceFromPlayer)
                return assignment;

            int bestIndex = -1;
            float bestDistance = -1f;

            for (int i = 0; i < room.SpawnPointCount; i++)
            {
                if (IsPointTakenByWave(i, wave, assignment))
                    continue;

                Transform candidate = room.GetSpawnPoint(i);
                if (candidate == null)
                    continue;

                float distance = PlanarDistance(candidate.position, player.position);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestIndex == assignment.SpawnPointIndex)
                return assignment;

            GameLog.Info(LogCategory.Level,
                $"spawn point {assignment.SpawnPointIndex} was {PlanarDistance(planned.position, player.position):0.0}m " +
                $"from the player - moved '{assignment.ArchetypeId}' to point {bestIndex} ({bestDistance:0.0}m)");

            return new SpawnAssignment(assignment.ArchetypeId, bestIndex);
        }

        /// <summary>True when another spawn in this wave already claims the point.</summary>
        bool IsPointTakenByWave(int pointIndex, WavePlan wave, SpawnAssignment self)
        {
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                SpawnAssignment other = wave.Spawns[i];
                if (other.SpawnPointIndex == pointIndex && other.SpawnPointIndex != self.SpawnPointIndex)
                    return true;
            }

            return false;
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
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

            BindIfBoss(instance, actor);
        }

        /// <summary>
        /// Hands a boss its randomness and announces the encounter.
        ///
        /// The stream is derived, not the run's own: how many times a boss draws depends on how
        /// many moves it lands, which depends on the player — consuming from the run stream here
        /// would make the seed stop reproducing the run.
        /// </summary>
        void BindIfBoss(GameObject instance, EnemyActor actor)
        {
            var boss = instance.GetComponent<BossController>();
            if (boss == null)
                return;

            if (run == null)
            {
                GameLog.Error(LogCategory.Level,
                    $"boss '{actor.name}' spawned without a RunContext - it cannot be seeded and will never act");
                return;
            }

            boss.Bind(run.DeriveStream());

            var encounter = new BossEncounter(actor, boss);
            actor.DeathSequenceStarted += () => BossDefeated?.Invoke();
            BossSpawned?.Invoke(encounter);
        }

        void OnEnemyDied(EnemyActor actor)
        {
            // A body destroyed by Abort still resolves its death a frame later. Without this the
            // teardown would report a phantom kill and advance a room the run has already left.
            if (aborted)
                return;

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
            if (aborted)
                return;

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                // IsAlive goes false on the killing blow, but a body playing a death beat is still
                // standing there. Dropping it here would open the door over a corpse and tear the
                // runner down before the beat finished.
                if (alive[i] == null || (!alive[i].IsAlive && !alive[i].IsDying))
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
            aborted = true;

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null)
                    UnityEngine.Object.Destroy(alive[i].gameObject);
            }

            alive.Clear();
        }
    }
}
