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

        /// <summary>
        /// Raised just before <see cref="ExitReached"/> with the index of the chosen door into
        /// this room's exit list — which is how the reward promised over that door becomes the
        /// next room's reward.
        /// </summary>
        public event Action<int> ExitChosen;

        [SerializeField, Tooltip("Optional socket where the room-clear reward pickup appears. Rooms without one use the centre of the play area.")]
        Transform rewardSpawnPoint;

        /// <summary>Where this room's reward pickup materialises.</summary>
        public Vector3 RewardSpawnPosition
        {
            get
            {
                if (rewardSpawnPoint != null)
                    return rewardSpawnPoint.position;

                float floorY = EntryPoint.position.y;
                return TryGetPlayArea(out Bounds play)
                    ? new Vector3(play.center.x, floorY, play.center.z)
                    : transform.position;
            }
        }

        [Header("Exits")]
        [SerializeField, Tooltip("Centre-to-centre distance between neighbouring doorways. The doors form one cluster in the middle of the exit wall — seeing one door means seeing them all — and the pitch shrinks automatically in rooms too narrow for it.")]
        float exitDoorPitch = 3.6f;

        [SerializeField, Tooltip("Gap between one door's offer appearing and the next. Long enough that a four-door fork reads as four separate offers, short enough that it never delays the walk to the pickup.")]
        float exitRevealStagger = 0.12f;

        sealed class BuiltExit
        {
            public GameObject Blocker;
            public Collider Trigger;
        }

        readonly List<BuiltExit> builtExits = new List<BuiltExit>();
        readonly List<ExitMarkerView> exitMarkers = new List<ExitMarkerView>();
        bool doorOpen;

        /// <summary>Index of the next offer to deal, or −1 when the fork is fully revealed.</summary>
        int revealNext = -1;
        float revealTimer;

        [SerializeField, Tooltip("How close the player must stand to a door for the Interact press to choose it. The nearest door inside this range is the focused one.")]
        float doorInteractRange = 3f;
        [SerializeField, Tooltip("Interact is ignored on the doors for this long after they unlock. The collect that unlocked them is the same button — without this, one press could take the reward AND walk through a door beside it.")]
        float doorSelectLockoutSeconds = 0.3f;
        float doorUnlockedAtUnscaled = float.NegativeInfinity;

        Transform interactPlayer;
        Game.Core.Player.PlayerInputReader interactInput;
        GameObject doorPrompt;
        Camera doorPromptView;
        int focusedExit = -1;
        Color blockerBaseColor = Color.white;
        static readonly int DoorBaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Hands the room what door choice needs: who is choosing and which action confirms.
        /// Called by the director when the room is built; without it the built doors simply
        /// cannot be walked through, which is the safe failure.
        /// </summary>
        public void BindDoorInteraction(Transform player, Game.Core.Player.PlayerInputReader input)
        {
            interactPlayer = player;
            interactInput = input;
        }

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
            if (open && !doorOpen)
            {
                doorUnlockedAtUnscaled = Time.unscaledTime;

                // The unlock is the reveal's deadline: a door the player can already walk through
                // must never still be announcing what is behind it.
                FinishRevealingExitMarkers();
            }

            doorOpen = open;

            if (builtExits.Count > 0)
            {
                // Built doors are chosen with the Interact press, never by touch — so the
                // blocker STAYS, sealing the doorway against a walk into the void, and only
                // its tint changes: lit means "press to enter", dark means locked.
                for (int i = 0; i < builtExits.Count; i++)
                {
                    GameObject blocker = builtExits[i].Blocker;
                    if (blocker == null)
                        continue;

                    var renderer = blocker.GetComponent<MeshRenderer>();
                    if (renderer == null)
                        continue;

                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(DoorBaseColorId, open
                        ? Color.Lerp(blockerBaseColor, Color.white, 0.55f)
                        : blockerBaseColor);
                    renderer.SetPropertyBlock(block);
                }

                if (!open)
                    ClearDoorFocus();

                return;
            }

            if (doorBlocker != null)
                doorBlocker.SetActive(!open);

            if (exitTrigger != null)
                exitTrigger.enabled = open;
        }

        /// <summary>
        /// Replaces the room's single authored door with one doorway per entry of
        /// <paramref name="rewards"/>, clustered at the middle of the exit wall, each with a
        /// visible frame and a floating reward icon naming what waits behind it (type as
        /// silhouette, tier as tint). The plan decides the offers (1–4 ordinarily, exactly one
        /// boss mark when the boss is next, zero for the final room); the cluster guarantees
        /// that finding one door means finding them all, even in a wide room.
        /// </summary>
        public void ConfigureExits(IReadOnlyList<RewardChoice> rewards, RewardGenerationConfig rewardConfig)
        {
            int count = rewards != null ? rewards.Count : 0;
            if (count <= 0 || doorBlocker == null || exitTrigger == null)
                return;

            Transform blockerT = doorBlocker.transform;
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
            {
                markerMaterial = blockerRenderer.sharedMaterial;
                if (markerMaterial != null && markerMaterial.HasProperty(DoorBaseColorId))
                    blockerBaseColor = markerMaterial.GetColor(DoorBaseColorId);
            }

            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * pitch;
                Vector3 shift = blockerT.right * offset;

                GameObject blocker = Instantiate(doorBlocker, blockerT.position + shift, blockerT.rotation, blockerT.parent);
                blocker.name = $"DoorBlocker_Exit{i + 1}";
                blocker.SetActive(true);

                // No touch trigger is cloned: built doors are chosen with the Interact press,
                // and the blocker doubles as the thing that keeps the doorway solid.
                RewardChoice choice = rewards[i];
                RewardDefinition definition = null;
                Color tierTint = Color.white;
                if (rewardConfig != null)
                {
                    definition = rewardConfig.FindDefinition(choice.Type);
                    tierTint = rewardConfig.BandTint(choice.Band);
                }

                ExitMarkerView marker = ExitMarkerView.Build(
                    transform, blockerT.position + shift, transform.position.y,
                    blockerT.localScale.x, choice, definition, tierTint,
                    GetComponent<IRewardPreviewRenderer>(), markerMaterial);

                // The offer is not revealed until the room is cleared: a fight should be read
                // as a fight, not shopped from behind enemies — and the reveal is the clear's
                // second reward beat.
                marker.gameObject.SetActive(false);
                exitMarkers.Add(marker);

                builtExits.Add(new BuiltExit { Blocker = blocker });
            }

            // The authored door becomes a dormant template; the built exits are the room's
            // doors from here on.
            doorBlocker.SetActive(false);
            exitTrigger.enabled = false;
            exitTrigger.gameObject.SetActive(false);

            SetDoorOpen(doorOpen);
        }

        /// <summary>
        /// Shows the reward icons over the doors. Called by the runner the moment the room is
        /// cleared — before the door itself opens when a reward gates it, because choosing the
        /// next room is exactly what the player thinks about while walking to the pickup.
        /// </summary>
        public void RevealExitMarkers()
        {
            if (exitMarkers.Count == 0)
                return;

            // Dealt one at a time rather than switched on together. A bank of icons appearing on a
            // single frame reads as scenery loading in; dealt in sequence it reads as an offer,
            // which is what the moment actually is.
            revealNext = 0;
            revealTimer = 0f;
        }

        /// <summary>
        /// Shows anything the stagger has not reached yet, at once. The door unlocking is the
        /// deadline: an offer the player can already walk through must never still be arriving.
        /// </summary>
        void FinishRevealingExitMarkers()
        {
            while (revealNext >= 0 && revealNext < exitMarkers.Count)
                RevealNextMarker();

            revealNext = -1;
        }

        void RevealNextMarker()
        {
            ExitMarkerView marker = exitMarkers[revealNext];
            revealNext++;

            if (marker == null)
                return;

            marker.gameObject.SetActive(true);
            marker.PlayReveal();
            Game.Core.Audio.AudioDirector.PlaySound(Game.Core.Audio.GameSound.DoorReveal);
        }

        void TickExitMarkerReveal()
        {
            if (revealNext < 0 || revealNext >= exitMarkers.Count)
                return;

            // Unscaled: the clear lands inside the killing blow's hitstop.
            revealTimer -= Time.unscaledDeltaTime;
            if (revealTimer > 0f)
                return;

            RevealNextMarker();
            revealTimer = exitRevealStagger;

            if (revealNext >= exitMarkers.Count)
                revealNext = -1;
        }

        /// <summary>
        /// Drives door choice: the nearest unlocked door inside the interact range is focused
        /// (its icon grows, the prompt appears over it), and the Interact press walks through
        /// it. Touch never advances a built door — a choice this permanent deserves a confirm,
        /// and a dash past the doorway must not eat it.
        /// </summary>
        void Update()
        {
            // Ticked before the focus early-out: the offers are dealt on the clear, which is
            // strictly before the door they sit over unlocks.
            TickExitMarkerReveal();

            if (!doorOpen || builtExits.Count == 0 || interactPlayer == null)
            {
                ClearDoorFocus();
                return;
            }

            int nearest = -1;
            float bestDistance = float.MaxValue;
            Vector3 player = interactPlayer.position;

            for (int i = 0; i < builtExits.Count; i++)
            {
                GameObject blocker = builtExits[i].Blocker;
                if (blocker == null)
                    continue;

                Vector3 door = blocker.transform.position;
                float dx = door.x - player.x;
                float dz = door.z - player.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance <= doorInteractRange && distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = i;
                }
            }

            SetDoorFocus(nearest);

            bool lockedOut = Time.unscaledTime < doorUnlockedAtUnscaled + doorSelectLockoutSeconds;
            if (focusedExit >= 0 && !lockedOut && interactInput != null && interactInput.InteractPressedThisFrame)
                NotifyExitReached(focusedExit);
        }

        void SetDoorFocus(int index)
        {
            focusedExit = index;

            for (int i = 0; i < exitMarkers.Count; i++)
            {
                if (exitMarkers[i] != null)
                    exitMarkers[i].SetFocused(i == index);
            }

            if (index >= 0 && builtExits[index].Blocker != null)
            {
                if (doorPrompt == null)
                    doorPrompt = InteractPrompt.Build(transform, Vector3.zero);

                doorPrompt.transform.position =
                    builtExits[index].Blocker.transform.position + Vector3.up * 1.6f;
                doorPrompt.SetActive(true);
            }
            else if (doorPrompt != null)
            {
                doorPrompt.SetActive(false);
            }
        }

        void ClearDoorFocus()
        {
            if (focusedExit != -1)
                SetDoorFocus(-1);
            else if (doorPrompt != null && doorPrompt.activeSelf)
                doorPrompt.SetActive(false);
        }

        void LateUpdate()
        {
            if (doorPrompt == null || !doorPrompt.activeSelf)
                return;

            if (doorPromptView == null)
                doorPromptView = Camera.main;

            if (doorPromptView != null)
                doorPrompt.transform.rotation = doorPromptView.transform.rotation;
        }

        /// <summary>
        /// The moment a door is chosen — by the Interact press on a built door, or by the
        /// legacy touch trigger on an authored single-door room.
        /// </summary>
        internal void NotifyExitReached(int exitIndex)
        {
            GameLog.Info(LogCategory.Level, $"exit {exitIndex} chosen in {name}");
            ExitChosen?.Invoke(exitIndex);
            ExitReached?.Invoke();
        }
    }
}
