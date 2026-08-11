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
        [SerializeField, Tooltip("Optional. Owns reward pickups, wallets and door-held-for-reward flow; without one, rooms behave exactly as before rewards existed.")]
        RewardDirector rewardDirector;

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
        PlayerBoons playerBoons;
        Game.Core.Player.PlayerInputReader playerInput;
        CinemachineConfiner3D confiner;
        CinemachineFollow follow;
        BoxCollider confinerVolume;
        RoomInstance currentRoom;
        RoomRunner currentRunner;
        Transform enemyParent;
        int roomIndex = -1;
        int levelIndex;
        bool awaitingLevelAdvance;

        /// <summary>
        /// The reward promised over the door the player just walked through, waiting to become
        /// the next room's reward. Null after the boss door, between levels, and at run start.
        /// </summary>
        RewardChoice? pendingReward;

        public RunContext Run { get; private set; }

        /// <summary>
        /// A dedicated stream for reward CONTENT rolls that happen at collect time (which
        /// giver calls, which Stray drifts in). Those draws depend on which doors the player
        /// chooses, so they must never touch the run stream — one derived stream absorbs them
        /// all and the run stream stays reproducible from the seed alone.
        /// </summary>
        public Game.Core.Rng.IRandomSource RewardStream { get; private set; }

        public LevelPlan Plan { get; private set; }

        public int CurrentRoomNumber => roomIndex + 1;

        /// <summary>1-based level the player is on.</summary>
        public int CurrentLevelNumber => levelIndex + 1;

        public int LevelsPerRun => settings != null ? settings.LevelsPerRun : 1;

        /// <summary>True on the last level, where beating the boss ends the run rather than the level.</summary>
        public bool OnFinalLevel => CurrentLevelNumber >= LevelsPerRun;

        public RoomRunner CurrentRoom => currentRunner;

        // The LevelCleared event and its boon-screen handshake were removed 2026-08-11 with the
        // between-levels elemental boon screen: the level-exit door leads straight into the next
        // era, and transmissions are the boon system.

        /// <summary>Raised when the whole run is won — the last boss of the last level.</summary>
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
                currentRoom.ExitChosen -= OnExitChosen;
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
            playerBoons = player.GetComponent<PlayerBoons>();
            playerInput = player.GetComponent<Game.Core.Player.PlayerInputReader>();

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
            RewardStream = Run.DeriveStream();
            levelIndex = 0;
            awaitingLevelAdvance = false;
            pendingReward = null;

            if (rewardDirector != null)
                rewardDirector.OnRunBegan(this);

            // A new run starts empty-handed. Without this a restart would inherit the previous
            // run's loadout and make its first room trivial.
            if (playerBoons != null)
                playerBoons.ClearForNewRun();

            GameLog.Info(LogCategory.Level, $"RUN START  {LevelsPerRun} level(s)");
            BuildLevel();
        }

        /// <summary>Generates and starts the level at <see cref="levelIndex"/>.</summary>
        void BuildLevel()
        {
            var generator = new LevelGenerator(settings);
            if (!generator.CanGenerate(out string reason))
            {
                GameLog.Error(LogCategory.Level, $"cannot generate a level: {reason}");
                enabled = false;
                return;
            }

            Plan = generator.Generate(Run, levelIndex);

            // Validate before playing rather than discovering a broken level mid-run. The soak
            // test makes this near-impossible, but a content edit could still break it.
            if (!LevelValidator.IsSolvable(Plan, settings, out string problem))
            {
                GameLog.Error(LogCategory.Level, $"generated level is unplayable: {problem}");
                enabled = false;
                return;
            }

            GameLog.Info(LogCategory.Level,
                $"LEVEL {CurrentLevelNumber}/{LevelsPerRun}  seed {Plan.Seed}  " +
                $"{Plan.RoomCount} rooms  {Plan.TotalEnemies} enemies total");

            roomIndex = -1;
            AdvanceRoom();
        }

        /// <summary>
        /// Called by the boon screen once the player has chosen. Kept as an explicit call rather
        /// than a timer so the run genuinely waits for them instead of racing an animation.
        ///
        /// Guarded against being called twice: it is only valid while a level is actually waiting
        /// to be continued. A mashed confirm button would otherwise advance two levels and skip one
        /// entirely, which is exactly the kind of thing a player does on a results screen.
        /// </summary>
        public void ContinueToNextLevel()
        {
            if (IsComplete || Run == null || !awaitingLevelAdvance)
                return;

            awaitingLevelAdvance = false;
            levelIndex++;
            BuildLevel();
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
                currentRoom.ExitChosen -= OnExitChosen;
                Destroy(currentRoom.gameObject);
            }

            roomIndex++;
            if (roomIndex >= Plan.RoomCount)
            {
                currentRunner = null;
                currentRoom = null;

                if (!OnFinalLevel)
                {
                    // Straight into the next era. The between-levels elemental boon screen is
                    // retired (human call 2026-08-11): transmissions ARE the boon system, the
                    // boss already paid its currency, and the level-exit door was the beat —
                    // stacking a second reward screen on top of it double-paid the boss.
                    Run.RecordLevelCleared();
                    GameLog.Info(LogCategory.Level,
                        $"LEVEL {CurrentLevelNumber}/{LevelsPerRun} CLEARED in {Run.ElapsedSeconds:0.0}s - onward");
                    levelIndex++;
                    BuildLevel();
                    return;
                }

                Run.RecordLevelCleared();
                IsComplete = true;
                GameLog.Info(LogCategory.Level,
                    $"RUN COMPLETE  seed {Run.Seed}  {Run.LevelsCleared} level(s), " +
                    $"{Run.RoomsCleared} rooms in {Run.ElapsedSeconds:0.0}s");
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
            currentRoom.ExitChosen += OnExitChosen;
            currentRoom.ApplyRole(roomPlan.Role);

            // The doors were decided at generation time with the rest of the plan: a seeded 1–4
            // reward offers for an ordinary next room (tier parity: one tier, distinct types),
            // exactly one boss-marked door when the boss is next (elites are Standard rooms and
            // do not count), none for the final room — clearing it ends the level with no door.
            if (roomPlan.ExitDoorCount > 0)
            {
                currentRoom.ConfigureExits(roomPlan.ExitRewards, settings.RewardConfigAsset);
                currentRoom.BindDoorInteraction(player, playerInput);
            }

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

            // What this room pays out: the run's very first room asks the policy (and pays out
            // BEFORE combat); every other room pays what the door the player chose promised.
            // Boss rooms pay nothing until boss drops are built. Without a reward director the
            // whole feature stands down and rooms behave exactly as they always have.
            RewardChoice? roomReward = null;
            bool rewardBeforeEnemies = false;

            if (rewardDirector != null && !roomPlan.IsBossRoom)
            {
                if (levelIndex == 0 && roomIndex == 0)
                {
                    roomReward = rewardDirector.DecideFirstRoomReward(RewardStream);
                    rewardBeforeEnemies = roomReward.HasValue;
                }
                else
                {
                    roomReward = pendingReward;
                }
            }

            pendingReward = null;
            currentRunner.HoldDoorForReward = roomReward.HasValue && !rewardBeforeEnemies;

            if (rewardDirector != null)
                rewardDirector.OnRoomStarted(roomPlan, currentRoom, currentRunner, roomReward, rewardBeforeEnemies);

            // The first room of a run waits: enemies spawn only after the opening reward is
            // collected. The reward director calls BeginHeldRoom once that happens.
            if (rewardBeforeEnemies)
                GameLog.Info(LogCategory.Level, "first room holds its reward - enemies wait for the collect");
            else
                currentRunner.Begin();
        }

        /// <summary>
        /// Starts the fight in a room whose enemies were held back for a pre-combat reward.
        /// Called by the reward director when the opening pickup is collected.
        /// </summary>
        public void BeginHeldRoom()
        {
            if (currentRunner != null && !currentRunner.IsCleared && currentRunner.WaveNumber == 0)
                currentRunner.Begin();
        }

        /// <summary>Remembers which offer the player walked through, for the next room's payout.</summary>
        void OnExitChosen(int exitIndex)
        {
            RoomPlan plan = Plan != null && roomIndex >= 0 && roomIndex < Plan.RoomCount ? Plan.Rooms[roomIndex] : null;
            if (plan == null || exitIndex < 0 || exitIndex >= plan.ExitRewards.Count)
                return;

            RewardChoice choice = plan.ExitRewards[exitIndex];
            pendingReward = choice.IsBossDoor || choice.IsLevelExit ? (RewardChoice?)null : choice;

            GameLog.Info(LogCategory.Level,
                choice.IsBossDoor ? "chose the boss door"
                : choice.IsLevelExit ? "stepped through the level exit"
                : $"chose the {choice} door");
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

            // The beaten boss's arena ends at its own level-exit door, chosen with Interact
            // like any other — the level must not cut away the instant the health bar empties.
            // Only a degenerate final room with no doors at all still advances by itself,
            // so a content set without the exit configured cannot hang the run.
            if (roomIndex >= Plan.RoomCount - 1 && Plan.Rooms[roomIndex].ExitDoorCount == 0)
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
