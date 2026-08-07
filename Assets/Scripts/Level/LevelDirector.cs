using System;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Rng;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Owns a run: creates the seeded <see cref="RunContext"/>, generates the level, then walks
    /// the player through the rooms one at a time. Only the current room exists in the scene,
    /// which keeps the camera confiner, the enemy count and the NavMesh story simple.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDirector : MonoBehaviour
    {
        [SerializeField] LevelGenerationSettings settings;
        [SerializeField] Transform player;
        [SerializeField, Tooltip("The gameplay vcam. Its confiner is retargeted per room.")]
        CinemachineCamera gameplayCamera;

        [Header("Seeding")]
        [SerializeField, Tooltip("Leave 0 to pick a seed from the system clock and log it.")]
        uint fixedSeed;

        [Header("Camera confinement")]
        [SerializeField, Tooltip("How far inside the room edges the camera stops. This is the follow-versus-see-past-the-walls knob: larger hides the void beyond the walls but makes the camera stop following sooner. Set to 0 for the unconfined feel.")]
        Vector2 confinerMargin = new Vector2(2.5f, 1f);
        [SerializeField, Tooltip("Vertical size of the confiner volume. Must comfortably contain the camera's height or it will be clamped and stop following.")]
        float confinerHeight = 30f;

        PlayerAttackController playerAttacks;
        PlayerHealth playerHealth;
        CinemachineConfiner3D confiner;
        CinemachineFollow follow;
        BoxCollider confinerVolume;
        RoomInstance currentRoom;
        RoomRunner currentRunner;
        Transform enemyParent;
        int roomIndex = -1;

        public RunContext Run { get; private set; }

        public LevelPlan Plan { get; private set; }

        public int CurrentRoomNumber => roomIndex + 1;

        public RoomRunner CurrentRoom => currentRunner;

        /// <summary>Raised when the final room is cleared.</summary>
        public event Action LevelCompleted;

        /// <summary>Raised as each room is built, so UI can announce it.</summary>
        public event Action<RoomPlan> RoomEntered;

        /// <summary>
        /// Raised when a boss enters play. Re-raised from the room runner so views can bind to the
        /// director, which outlives every room, instead of to a runner that is torn down each time.
        /// </summary>
        public event Action<IBossEncounter> BossEncounterStarted;

        /// <summary>Raised when the boss goes down.</summary>
        public event Action BossEncounterEnded;

        /// <summary>True once the player is standing in the boss room.</summary>
        public bool InBossRoom => Plan != null && roomIndex >= 0 && roomIndex < Plan.RoomCount && Plan.Rooms[roomIndex].IsBossRoom;

        /// <summary>True when clearing the current room opens the door to the boss.</summary>
        public bool BossRoomIsNext => Plan != null && roomIndex >= 0 && roomIndex + 1 < Plan.RoomCount && Plan.Rooms[roomIndex + 1].IsBossRoom;

        /// <summary>True once the level is won. Stops the director doing anything further.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Starts a fresh run. <paramref name="sameSeed"/> replays the current one exactly —
        /// the debug aid DESIGN.md asks for; otherwise a new seed is derived from this run's
        /// own stream so consecutive restarts do not repeat.
        /// </summary>
        public void RestartRun(bool sameSeed)
        {
            uint seed = Run == null ? fixedSeed : (sameSeed ? Run.Seed : Run.NextRunSeed());

            if (currentRunner != null)
            {
                DetachRunner(currentRunner);
                currentRunner.Abort();
                currentRunner = null;
            }

            if (currentRoom != null)
            {
                currentRoom.ExitReached -= OnExitReached;
                Destroy(currentRoom.gameObject);
                currentRoom = null;
            }

            // Anything that outlived its room — a projectile in flight, for instance.
            for (int i = enemyParent.childCount - 1; i >= 0; i--)
                Destroy(enemyParent.GetChild(i).gameObject);

            IsComplete = false;
            enabled = true;
            GameLog.Info(LogCategory.Level, $"RESTART  {(sameSeed ? "same" : "new")} seed");
            BeginRun(seed);
        }

        void Awake()
        {
            if (settings == null || player == null)
            {
                Debug.LogError($"{nameof(LevelDirector)} on '{name}' needs settings and a player.", this);
                enabled = false;
                return;
            }

            enemyParent = new GameObject("SpawnedEnemies").transform;

            // Run stats are gathered here because this is the only object that outlives a room
            // and knows the RunContext. Without this the death screen would show all zeros.
            playerAttacks = player.GetComponent<PlayerAttackController>();
            playerHealth = player.GetComponent<PlayerHealth>();

            if (playerAttacks != null)
                playerAttacks.Hit += OnPlayerDealtDamage;

            if (playerHealth != null)
            {
                playerHealth.Damaged += OnPlayerTookDamage;
                playerHealth.PerfectDodged += OnPerfectDodge;
            }

            if (gameplayCamera != null)
            {
                confiner = gameplayCamera.GetComponent<CinemachineConfiner3D>();
                if (confiner == null)
                    confiner = gameplayCamera.gameObject.AddComponent<CinemachineConfiner3D>();

                follow = gameplayCamera.GetComponent<CinemachineFollow>();

                // One volume reused for every room. Its collider is a trigger on a disabled
                // layer-free object: it exists purely as a shape for the confiner to read.
                var volumeGo = new GameObject("CameraConfinerVolume");
                volumeGo.transform.SetParent(transform, false);
                confinerVolume = volumeGo.AddComponent<BoxCollider>();
                confinerVolume.isTrigger = true;
                confiner.BoundingVolume = confinerVolume;
            }
        }

        void Start() => BeginRun(fixedSeed);

        void Update()
        {
            if (Run != null)
                Run.Tick(Time.deltaTime);

            // Cheap safety net against a room that can never be cleared because an enemy left
            // the alive list by some route other than dying.
            if (currentRunner != null && !currentRunner.IsCleared && currentRunner.AliveCount > 0)
                currentRunner.PruneAndCheckCleared();
        }

        public void BeginRun(uint seed)
        {
            if (seed == 0u)
                seed = (uint)Environment.TickCount;

            Run = new RunContext(seed);

            var generator = new LevelGenerator(settings);
            if (!generator.CanGenerate(out string reason))
            {
                GameLog.Error(LogCategory.Level, $"cannot generate a level: {reason}");
                enabled = false;
                return;
            }

            Plan = generator.Generate(Run);

            // Validate before playing rather than discovering a broken level mid-run. The soak
            // test makes this near-impossible, but a content edit could still break it.
            if (!LevelValidator.IsSolvable(Plan, settings, out string problem))
            {
                GameLog.Error(LogCategory.Level, $"generated level is unplayable: {problem}");
                enabled = false;
                return;
            }

            GameLog.Info(LogCategory.Level,
                $"LEVEL PLAN  seed {Plan.Seed}  {Plan.RoomCount} rooms  {Plan.TotalEnemies} enemies total");

            roomIndex = -1;
            AdvanceRoom();
        }

        void AdvanceRoom()
        {
            if (currentRunner != null)
            {
                DetachRunner(currentRunner);
                currentRunner.Abort();
            }

            if (currentRoom != null)
            {
                currentRoom.ExitReached -= OnExitReached;
                Destroy(currentRoom.gameObject);
            }

            roomIndex++;
            if (roomIndex >= Plan.RoomCount)
            {
                IsComplete = true;
                GameLog.Info(LogCategory.Level,
                    $"LEVEL COMPLETE  seed {Plan.Seed}  {Run.RoomsCleared} rooms in {Run.ElapsedSeconds:0.0}s");
                LevelCompleted?.Invoke();
                return;
            }

            RoomPlan roomPlan = Plan.Rooms[roomIndex];
            RoomTemplateDefinition template = settings.FindTemplate(roomPlan.TemplateId);
            if (template == null || template.Prefab == null)
            {
                GameLog.Error(LogCategory.Level, $"template '{roomPlan.TemplateId}' has no prefab - run cannot continue");
                enabled = false;
                return;
            }

            currentRoom = Instantiate(template.Prefab, Vector3.zero, Quaternion.identity);
            currentRoom.name = $"Room_{roomIndex + 1}_{roomPlan.TemplateId}{(roomPlan.IsBossRoom ? "_BOSS" : string.Empty)}";
            currentRoom.ExitReached += OnExitReached;
            currentRoom.ApplyRole(roomPlan.Role);

            RoomEntered?.Invoke(roomPlan);

            MovePlayerTo(currentRoom.EntryPoint);
            ApplyCameraBounds(currentRoom);

            // The player is passed in so the runner can refuse to drop an enemy in their lap;
            // MovePlayerTo above has already put them at the entry point, so it reads a settled
            // position rather than wherever they were in the previous room.
            currentRunner = new RoomRunner(currentRoom, roomPlan, settings, enemyParent, Run, player);
            currentRunner.Cleared += OnRoomCleared;
            currentRunner.EnemyKilled += OnEnemyKilled;
            currentRunner.BossSpawned += OnBossSpawned;
            currentRunner.BossDefeated += OnBossDefeated;
            currentRunner.Begin();
        }

        void DetachRunner(RoomRunner runner)
        {
            runner.Cleared -= OnRoomCleared;
            runner.EnemyKilled -= OnEnemyKilled;
            runner.BossSpawned -= OnBossSpawned;
            runner.BossDefeated -= OnBossDefeated;
        }

        void OnBossSpawned(IBossEncounter encounter) => BossEncounterStarted?.Invoke(encounter);

        void OnBossDefeated() => BossEncounterEnded?.Invoke();

        void OnRoomCleared()
        {
            Run.RecordRoomCleared();
            Game.Core.Audio.AudioDirector.PlaySound(Game.Core.Audio.GameSound.RoomClear);

            // The final room needs no door: clearing it ends the level.
            if (roomIndex >= Plan.RoomCount - 1)
                AdvanceRoom();
        }

        void OnExitReached() => AdvanceRoom();

        /// <summary>Debug aid: jump straight to the next room, cleared or not.</summary>
        public void SkipToNextRoom()
        {
            if (Plan == null || IsComplete)
                return;

            GameLog.Warn(LogCategory.Level, $"DEBUG: skipping from room {roomIndex + 1}");
            AdvanceRoom();
        }

        void OnEnemyKilled()
        {
            if (Run != null)
                Run.RecordKill();

            Game.Core.Audio.AudioDirector.PlaySound(Game.Core.Audio.GameSound.EnemyDeath);
        }

        void OnPlayerDealtDamage(HitContext context)
        {
            if (Run != null)
                Run.RecordDamageDealt(context.Damage);
        }

        void OnPlayerTookDamage(float amount)
        {
            if (Run != null)
                Run.RecordDamageTaken(amount);
        }

        void OnPerfectDodge()
        {
            if (Run != null)
                Run.RecordPerfectDodge();
        }

        void OnDestroy()
        {
            if (playerAttacks != null)
                playerAttacks.Hit -= OnPlayerDealtDamage;

            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerTookDamage;
                playerHealth.PerfectDodged -= OnPerfectDodge;
            }
        }

        void MovePlayerTo(Transform anchor)
        {
            // The controller must be disabled across a teleport or it will fight the move and
            // snap the player back.
            var controller = player.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null)
                controller.enabled = false;

            player.SetPositionAndRotation(anchor.position, anchor.rotation);

            if (controller != null)
                controller.enabled = wasEnabled;

            Physics.SyncTransforms();
        }

        /// <summary>
        /// Builds the volume the *camera* may occupy, which is the room's play area pushed into
        /// camera space by the follow offset and made tall enough to contain the camera height.
        ///
        /// Confining the camera to the room volume itself — which is what this did originally —
        /// puts the camera outside its own bounds (it sits ~13 m up and 10 m back), so the
        /// confiner clamps it to the nearest face every frame and it stops following the player.
        /// </summary>
        void ApplyCameraBounds(RoomInstance room)
        {
            if (confiner == null || confinerVolume == null)
                return;

            if (!room.TryGetPlayArea(out Bounds play))
            {
                confinerVolume.size = new Vector3(10000f, confinerHeight, 10000f);
                return;
            }

            Vector3 offset = follow != null ? follow.FollowOffset : new Vector3(0f, 12f, -10f);

            confinerVolume.transform.position = new Vector3(
                play.center.x + offset.x,
                play.center.y + offset.y,
                play.center.z + offset.z);

            // Shrinking by the margin is what stops the camera showing past the walls; it must
            // never go negative or the confiner would pin the camera to a single point.
            confinerVolume.center = Vector3.zero;
            confinerVolume.size = new Vector3(
                Mathf.Max(0.5f, play.size.x - confinerMargin.x * 2f),
                Mathf.Max(1f, confinerHeight),
                Mathf.Max(0.5f, play.size.z - confinerMargin.y * 2f));

            GameLog.Debug(LogCategory.Camera,
                $"confiner volume centre {confinerVolume.transform.position} size {confinerVolume.size}");
        }
    }
}
