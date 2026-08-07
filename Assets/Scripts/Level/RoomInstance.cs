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
        [SerializeField, Tooltip("The room's playable floor area. The camera confiner volume is derived from this, not used as it.")]
        Collider cameraBounds;

        public Transform EntryPoint => entryPoint != null ? entryPoint : transform;

        public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

        public Collider CameraBounds => cameraBounds;

        /// <summary>
        /// World-space bounds of the playable area. The camera is confined to a volume derived
        /// from this, offset into camera space — confining the camera to the room volume itself
        /// is wrong, because a top-down camera sits above and behind the room, not inside it.
        /// </summary>
        public bool TryGetPlayArea(out Bounds area)
        {
            if (cameraBounds == null)
            {
                area = default;
                return false;
            }

            area = cameraBounds.bounds;
            return true;
        }

        /// <summary>Raised when the player touches the exit trigger of a cleared room.</summary>
        public event Action ExitReached;

        [Header("Boss signalling")]
        [SerializeField, Tooltip("Tint applied to the room's geometry when it hosts the boss, so the space itself reads as different before anything attacks.")]
        Color bossTint = new Color(0.42f, 0.16f, 0.22f);
        [SerializeField, Tooltip("Optional object enabled only in the boss room.")]
        GameObject bossMarker;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>What this room is being used for this run.</summary>
        public RoomRole Role { get; private set; } = RoomRole.Standard;

        void Awake() => SetDoorOpen(false);

        /// <summary>
        /// Applies the room's role. For the boss room this is the only thing distinguishing it
        /// until real mechanics exist, so it deliberately changes the whole space rather than
        /// adding a small badge.
        /// </summary>
        public void ApplyRole(RoomRole role)
        {
            Role = role;

            if (bossMarker != null)
                bossMarker.SetActive(role == RoomRole.Boss);

            if (role != RoomRole.Boss)
                return;

            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(block);
                Material material = renderer.sharedMaterial;
                Color baseColor = material != null && material.HasProperty(BaseColorId)
                    ? material.GetColor(BaseColorId)
                    : Color.white;

                block.SetColor(BaseColorId, Color.Lerp(baseColor, bossTint, 0.75f));
                renderer.SetPropertyBlock(block);
            }
        }

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
