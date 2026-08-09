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

        [Header("Exits")]
        [SerializeField, Tooltip("Centre-to-centre distance between neighbouring doorways. The doors form one cluster in the middle of the exit wall — seeing one door means seeing them all — and the pitch shrinks automatically in rooms too narrow for it.")]
        float exitDoorPitch = 3.6f;

        sealed class BuiltExit
        {
            public GameObject Blocker;
            public Collider Trigger;
        }

        readonly List<BuiltExit> builtExits = new List<BuiltExit>();
        bool doorOpen;

        [Header("Boss signalling")]
        [SerializeField, Tooltip("Tint applied to the room's geometry when it hosts the boss, so the space itself reads as different before anything attacks. Deliberately a cold desaturated slate: the boss's melee telegraph is red, and a red room would swallow it (CLAUDE.md rule 7 reserves saturated hues for gameplay information).")]
        Color bossTint = new Color(0.20f, 0.21f, 0.27f);
        [SerializeField, Range(0f, 1f), Tooltip("How far the room's own colours are pulled toward the tint. High enough to read as a different space, low enough to leave the palette legible.")]
        float bossTintStrength = 0.55f;
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

                block.SetColor(BaseColorId, Color.Lerp(baseColor, bossTint, Mathf.Clamp01(bossTintStrength)));
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
            doorOpen = open;

            if (builtExits.Count > 0)
            {
                for (int i = 0; i < builtExits.Count; i++)
                {
                    if (builtExits[i].Blocker != null)
                        builtExits[i].Blocker.SetActive(!open);

                    if (builtExits[i].Trigger != null)
                        builtExits[i].Trigger.enabled = open;
                }

                return;
            }

            if (doorBlocker != null)
                doorBlocker.SetActive(!open);

            if (exitTrigger != null)
                exitTrigger.enabled = open;
        }

        /// <summary>
        /// Replaces the room's single authored door with <paramref name="count"/> doorways
        /// clustered at the middle of the exit wall, each with a visible frame and a floating
        /// number naming the reward behind it (placeholder digits for now). The plan decides
        /// how many (1–4 ordinarily, exactly one when the boss is next, zero for the final
        /// room); the cluster guarantees that finding one door means finding them all, even in
        /// a wide room.
        ///
        /// <para>Every door leads to the same generated next room today; the choice is the
        /// signposting groundwork for per-exit rewards.</para>
        /// </summary>
        public void ConfigureExits(int count, IReadOnlyList<int> labels)
        {
            if (count <= 0 || doorBlocker == null || exitTrigger == null)
                return;

            Transform blockerT = doorBlocker.transform;
            Transform triggerT = exitTrigger.transform;

            // The pitch shrinks when the wall cannot seat the cluster at full spacing, rather
            // than pushing the outer doors through the side walls.
            float pitch = Mathf.Max(1f, exitDoorPitch);
            if (count > 1 && TryGetPlayArea(out Bounds play))
            {
                float usable = play.size.x - blockerT.localScale.x - 1f;
                pitch = Mathf.Min(pitch, usable / (count - 1));
            }

            Material markerMaterial = null;
            var blockerRenderer = doorBlocker.GetComponent<MeshRenderer>();
            if (blockerRenderer != null)
                markerMaterial = blockerRenderer.sharedMaterial;

            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * pitch;
                Vector3 shift = blockerT.right * offset;

                GameObject blocker = Instantiate(doorBlocker, blockerT.position + shift, blockerT.rotation, blockerT.parent);
                blocker.name = $"DoorBlocker_Exit{i + 1}";
                blocker.SetActive(true);

                GameObject triggerGo = Instantiate(exitTrigger.gameObject, triggerT.position + shift, triggerT.rotation, triggerT.parent);
                triggerGo.name = $"ExitTrigger_Exit{i + 1}";
                triggerGo.SetActive(true);
                var trigger = triggerGo.GetComponent<Collider>();

                int label = labels != null && i < labels.Count ? labels[i] : i + 1;
                ExitMarkerView.Build(
                    transform, blockerT.position + shift, transform.position.y,
                    blockerT.localScale.x, label, markerMaterial);

                builtExits.Add(new BuiltExit { Blocker = blocker, Trigger = trigger });
            }

            // The authored door becomes a dormant template; the built exits are the room's
            // doors from here on.
            doorBlocker.SetActive(false);
            exitTrigger.enabled = false;
            exitTrigger.gameObject.SetActive(false);

            SetDoorOpen(doorOpen);
        }

        /// <summary>Called by the trigger's forwarder when the player steps into a cleared exit.</summary>
        internal void NotifyExitReached()
        {
            GameLog.Info(LogCategory.Level, $"exit reached in {name}");
            ExitReached?.Invoke();
        }
    }
}
