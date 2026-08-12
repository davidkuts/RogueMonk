using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Every number the wall-occlusion fade uses. One asset, so the whole effect is retuned without
    /// opening a script — CLAUDE.md rule 2.
    ///
    /// <para>The feature exists because the camera sits south of the arena at a 50° pitch and the
    /// south wall regularly covers the player, or worse, an enemy mid-wind-up. DESIGN.md's promise
    /// is that every attack can be read and answered; a telegraph nobody can see breaks it from a
    /// direction the player has no way to defend against.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Occlusion Fade Settings", fileName = "OcclusionFadeSettings")]
    public sealed class OcclusionFadeSettings : ScriptableObject
    {
        /// <summary>
        /// Hard ceiling on tracked actors, matched by <c>MONK_MAX_OCCLUSION_REVEALS</c> in
        /// MonkOcclusionFade.hlsl. A shader array is fixed at compile time, so this is the one
        /// number that cannot live in data alone — raising it means editing both.
        /// </summary>
        public const int MaxTrackedActorsCeiling = 8;

        [Header("Fade")]
        [SerializeField, Range(0f, 1f), Tooltip("Share of pixels a fully faded wall keeps. 0.25 leaves a quarter of it standing, which is enough to read as a wall while the actor reads through it. Zero would delete the wall outright and lose the sense of a room.")]
        float fadedVisibility = 0.25f;

        [SerializeField, Tooltip("Seconds to fade IN once a wall starts occluding. Short: whatever it is hiding is already hidden.")]
        float fadeInSeconds = 0.2f;

        [SerializeField, Tooltip("Seconds to fade back OUT. Can afford to be the slower of the two — nothing is being concealed while it finishes.")]
        float fadeOutSeconds = 0.2f;

        [Header("Detection")]
        [SerializeField, Tooltip("Radius of the camera-to-actor spherecast. Wider than a ray so a body half-behind a wall edge still registers, which is exactly the case a ray misses and the player resents.")]
        float spherecastRadius = 0.45f;

        [SerializeField, Range(1, 10), Tooltip("Run detection every Nth frame. The easing runs every frame regardless, so raising this costs latency on the first frame of a fade and nothing else.")]
        int detectionIntervalFrames = 3;

        [SerializeField, Tooltip("Which layers can occlude. Walls sit on Default alongside the floor; occluders are identified by carrying a WallOccluder, never by their layer — M21C's ground and clearance probes depend on walls staying on Default.")]
        LayerMask occluderLayers = 1;

        [SerializeField, Tooltip("Height added to an actor's transform for the cast and the reveal centre. Both the player and the enemies are CharacterController capsules whose transform already sits at the BODY CENTRE, so zero is chest height and is what you want: aiming a metre higher pointed the cast above the character's own head, which made a wall have to be taller than the actor before it counted as hiding them, and pushed the reveal disc off the body it is meant to cover.")]
        float trackHeightOffset;

        [SerializeField, Range(1, MaxTrackedActorsCeiling), Tooltip("Most actors tracked at once. Over the cap, the player is always kept and the nearest enemies to the camera win — those are the ones the near wall is hiding.")]
        int maxTrackedActors = MaxTrackedActorsCeiling;

        [SerializeField, Tooltip("Track live enemies as well as the player. Off makes this a player-only camera fix.")]
        bool trackEnemies = true;

        [SerializeField, Range(0f, 1f), Tooltip("How squarely a wall must face the camera before it may fade. The camera looks north and down, so only the SOUTH wall is ever genuinely in the way; the side walls run alongside the view and used to open a hole right next to a player standing in front of them. 0.5 admits the camera-facing wall and nothing else. Zero switches the filter off and lets any occluder fade.")]
        float cameraFacingDot = 0.5f;

        [Header("Reveal disc")]
        [SerializeField, Range(0.01f, 0.5f), Tooltip("Radius of the dithered hole, as a fraction of screen HEIGHT. This is what keeps a 20 m wall from dissolving whole — only the patch actually covering the actor goes. Size it against the body PLUS its wind-up: a telegraph that reads half-cut is the failure this feature exists to prevent.")]
        float revealRadiusScreenHeights = 0.16f;

        [SerializeField, Range(0f, 0.2f), Tooltip("Soft edge on the hole, in the same units. Zero gives a hard-edged circle, which reads as a hole cut in the wall rather than the wall thinning out.")]
        float revealSoftnessScreenHeights = 0.05f;

        public float FadedVisibility => Mathf.Clamp01(fadedVisibility);
        public float FadeInSeconds => Mathf.Max(0f, fadeInSeconds);
        public float FadeOutSeconds => Mathf.Max(0f, fadeOutSeconds);
        public float SpherecastRadius => Mathf.Max(0.01f, spherecastRadius);
        public int DetectionIntervalFrames => Mathf.Max(1, detectionIntervalFrames);
        public LayerMask OccluderLayers => occluderLayers;
        public float TrackHeightOffset => trackHeightOffset;
        public int MaxTrackedActors => Mathf.Clamp(maxTrackedActors, 1, MaxTrackedActorsCeiling);
        public bool TrackEnemies => trackEnemies;
        public float CameraFacingDot => Mathf.Clamp01(cameraFacingDot);
        public float RevealRadiusScreenHeights => Mathf.Max(0.001f, revealRadiusScreenHeights);
        public float RevealSoftnessScreenHeights => Mathf.Max(0f, revealSoftnessScreenHeights);
    }
}
