using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The authored side of a room: where the player enters, where enemies appear, what the
    /// camera may see, and the door that stays shut until the room is cleared. Everything here
    /// is placed by hand in the prefab; nothing is generated.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomInstance : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the player appears when entering this room.")]
        Transform entryPoint;
        [SerializeField, Tooltip("Tagged spawn points. The generator never places more enemies than there are points here.")]
        Transform[] spawnPoints = new Transform[0];
        [SerializeField, Tooltip("Blocks the exit until the room is cleared.")]
        GameObject doorBlocker;
        [SerializeField, Tooltip("Trigger the player touches to advance. Enabled only once cleared.")]
        Collider exitTrigger;
        [SerializeField, Tooltip("Bounds the camera may not leave while this room is active.")]
        Collider cameraBounds;

        public Transform EntryPoint => entryPoint != null ? entryPoint : transform;

        public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

        public Collider CameraBounds => cameraBounds;

        /// <summary>Raised when the player touches the exit trigger of a cleared room.</summary>
        public event Action ExitReached;

        void Awake() => SetDoorOpen(false);

        public Transform GetSpawnPoint(int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return transform;

            // Clamped rather than thrown: the generator guarantees valid indices, and a level
            // that spawns an enemy slightly off-place beats one that throws mid-run.
            return spawnPoints[Mathf.Clamp(index, 0, spawnPoints.Length - 1)];
        }

        public IReadOnlyList<Transform> SpawnPoints => spawnPoints;

        public void SetDoorOpen(bool open)
        {
            if (doorBlocker != null)
                doorBlocker.SetActive(!open);

            if (exitTrigger != null)
                exitTrigger.enabled = open;
        }

        /// <summary>Called by the trigger's forwarder when the player steps into a cleared exit.</summary>
        internal void NotifyExitReached()
        {
            GameLog.Info(LogCategory.Level, $"exit reached in {name}");
            ExitReached?.Invoke();
        }
    }
}
