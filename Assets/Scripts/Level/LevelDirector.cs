using System;
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

        CinemachineConfiner3D confiner;
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

        void Awake()
        {
            if (settings == null || player == null)
            {
                Debug.LogError($"{nameof(LevelDirector)} on '{name}' needs settings and a player.", this);
                enabled = false;
                return;
            }

            enemyParent = new GameObject("SpawnedEnemies").transform;

            if (gameplayCamera != null)
            {
                confiner = gameplayCamera.GetComponent<CinemachineConfiner3D>();
                if (confiner == null)
                    confiner = gameplayCamera.gameObject.AddComponent<CinemachineConfiner3D>();
            }
        }

        void Start() => BeginRun(fixedSeed);

        void Update()
        {
            if (Run != null)
                Run.Tick(Time.deltaTime);
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
                currentRunner.Cleared -= OnRoomCleared;
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
            currentRoom.name = $"Room_{roomIndex + 1}_{roomPlan.TemplateId}";
            currentRoom.ExitReached += OnExitReached;

            MovePlayerTo(currentRoom.EntryPoint);
            ApplyCameraBounds(currentRoom);

            currentRunner = new RoomRunner(currentRoom, roomPlan, settings, enemyParent);
            currentRunner.Cleared += OnRoomCleared;
            currentRunner.Begin();
        }

        void OnRoomCleared()
        {
            Run.RecordRoomCleared();

            // The final room needs no door: clearing it ends the level.
            if (roomIndex >= Plan.RoomCount - 1)
                AdvanceRoom();
        }

        void OnExitReached() => AdvanceRoom();

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

        void ApplyCameraBounds(RoomInstance room)
        {
            if (confiner == null || room.CameraBounds == null)
                return;

            // Confiner3D reads the collider directly each frame — unlike the 2D confiner there
            // is no baked shape cache to invalidate, so reassigning is the whole job.
            confiner.BoundingVolume = room.CameraBounds;
        }
    }
}
