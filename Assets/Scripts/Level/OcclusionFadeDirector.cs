using System.Collections.Generic;
using Game.Enemies;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Decides which walls are hiding somebody and tells the shader where to cut.
    ///
    /// <para>The mask a wall fragment is clipped against is three terms multiplied:
    /// the <b>per-renderer fade</b> this component eases (1 while the wall occludes anything),
    /// a <b>screen-space reveal disc</b> around each tracked actor, and a <b>depth test</b> that
    /// only cuts fragments nearer the camera than the actor they are being cut for.</para>
    ///
    /// <para>The per-renderer term is what stops a reveal disc punching a hole in a wall the actor
    /// is standing in <em>front</em> of, and it is why walls are still tracked individually even
    /// though the hole itself is per-pixel. The disc is what made splitting the 20 m wall meshes
    /// unnecessary: granularity comes from the shader rather than from the geometry, so only the
    /// patch actually covering a body dissolves and the toon outline gains no seams.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OcclusionFadeDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("Every tunable the effect has.")]
        OcclusionFadeSettings settings;

        [SerializeField, Tooltip("The player. Always tracked, whatever else is on screen.")]
        Transform player;

        [SerializeField, Tooltip("The rendering camera. Falls back to Camera.main when left empty.")]
        Camera view;

        static readonly int RevealsId = Shader.PropertyToID("_OcclusionReveals");
        static readonly int RevealCountId = Shader.PropertyToID("_OcclusionRevealCount");
        static readonly int RevealSoftnessId = Shader.PropertyToID("_OcclusionRevealSoftness");

        // Fixed length, always uploaded whole: Unity locks a global array's size at the first set,
        // so handing it a shorter one later silently keeps the stale tail.
        readonly Vector4[] reveals = new Vector4[OcclusionFadeSettings.MaxTrackedActorsCeiling];
        readonly List<Vector3> tracked = new List<Vector3>();
        readonly List<Vector3> enemyCandidates = new List<Vector3>();
        readonly RaycastHit[] castHits = new RaycastHit[16];

        int frameCounter;

        /// <summary>How many actors were tracked on the last frame. For the Play-Mode checks.</summary>
        public int TrackedCount => tracked.Count;

        void OnDisable()
        {
            // Leaving the globals set would let a scene with no director inherit the last one's
            // holes the moment anything drives a fade.
            PublishReveals(0);

            IReadOnlyList<WallOccluder> occluders = WallOccluder.Live;
            for (int i = 0; i < occluders.Count; i++)
            {
                if (occluders[i] != null)
                    occluders[i].ClearFade();
            }
        }

        void LateUpdate()
        {
            if (settings == null)
                return;

            if (view == null)
                view = Camera.main;

            if (view == null)
                return;

            GatherTrackedActors();

            frameCounter++;
            if (frameCounter >= settings.DetectionIntervalFrames)
            {
                frameCounter = 0;
                RunDetection();
            }

            // Unscaled, deliberately. Hitstop fires because something just hit, which is exactly
            // when the player most needs to see what did it — a fade that stalls through the
            // freeze holds the wall solid over the one frame that matters. It also makes the
            // effect immune to the time kit (Pocket Freeze, Stored Rewind, slow-mo), which drives
            // timeScale for gameplay reasons that have nothing to do with visibility.
            float deltaTime = Time.unscaledDeltaTime;
            IReadOnlyList<WallOccluder> occluders = WallOccluder.Live;
            for (int i = 0; i < occluders.Count; i++)
            {
                if (occluders[i] != null)
                    occluders[i].Tick(deltaTime, settings);
            }

            PublishReveals(tracked.Count);
        }

        /// <summary>
        /// Builds this frame's tracked list: the player always, then live enemies.
        ///
        /// <para>"Active or aggroed" is simply alive-and-not-dying here, because this game has no
        /// dormant spawns — a wave's enemies engage on the frame they appear. Off-screen bodies are
        /// dropped because a wall cannot be hiding something that is not in the frame at all.</para>
        /// </summary>
        void GatherTrackedActors()
        {
            tracked.Clear();
            enemyCandidates.Clear();

            Vector3 offset = Vector3.up * settings.TrackHeightOffset;

            if (player != null)
                tracked.Add(player.position + offset);

            if (!settings.TrackEnemies)
                return;

            IReadOnlyList<EnemyActor> enemies = EnemyActor.Live;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyActor enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || enemy.IsDying)
                    continue;

                Vector3 position = enemy.transform.position + offset;
                if (IsOnScreen(position))
                    enemyCandidates.Add(position);
            }

            // Over the cap the nearest to the camera win: those are the ones the near wall — the
            // south wall this whole feature exists for — is most likely to be covering.
            int room = settings.MaxTrackedActors - tracked.Count;
            Vector3 eye = view.transform.position;

            for (int taken = 0; taken < room && enemyCandidates.Count > 0; taken++)
            {
                int nearest = 0;
                float nearestDistance = (enemyCandidates[0] - eye).sqrMagnitude;
                for (int i = 1; i < enemyCandidates.Count; i++)
                {
                    float distance = (enemyCandidates[i] - eye).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearest = i;
                        nearestDistance = distance;
                    }
                }

                tracked.Add(enemyCandidates[nearest]);
                enemyCandidates.RemoveAt(nearest);
            }
        }

        bool IsOnScreen(Vector3 worldPosition)
        {
            Vector3 viewport = view.WorldToViewportPoint(worldPosition);

            // Generous margins: a body half off the edge is still half on screen, and its wind-up
            // is exactly the one the player is least prepared for.
            return viewport.z > 0f
                && viewport.x > -0.15f && viewport.x < 1.15f
                && viewport.y > -0.15f && viewport.y < 1.15f;
        }

        void RunDetection()
        {
            IReadOnlyList<WallOccluder> occluders = WallOccluder.Live;
            for (int i = 0; i < occluders.Count; i++)
            {
                if (occluders[i] != null)
                    occluders[i].BeginDetectionPass();
            }

            Vector3 origin = view.transform.position;
            int layers = settings.OccluderLayers;
            float radius = settings.SpherecastRadius;

            for (int i = 0; i < tracked.Count; i++)
            {
                Vector3 toActor = tracked[i] - origin;
                float distance = toActor.magnitude;
                if (distance <= 0.01f)
                    continue;

                int hits = Physics.SphereCastNonAlloc(
                    origin, radius, toActor / distance, castHits, distance, layers,
                    QueryTriggerInteraction.Ignore);

                for (int h = 0; h < hits; h++)
                {
                    if (WallOccluder.TryGet(castHits[h].collider, out WallOccluder occluder))
                        occluder.MarkOccluding();
                }
            }
        }

        /// <summary>
        /// Uploads the reveal discs in VIEW space.
        ///
        /// <para>View space rather than screen UVs on purpose: UVs would drag in render scale and
        /// the platform's UV-origin flip, both easy to get subtly wrong and invisible until a build.
        /// The shader divides x/y by depth, which gives angular coordinates — so a radius expressed
        /// as a fraction of screen height converts once, here, through the vertical half-FOV.</para>
        /// </summary>
        void PublishReveals(int count)
        {
            count = Mathf.Clamp(count, 0, reveals.Length);

            float radius = 0f;
            float softness = 0f;

            if (count > 0 && view != null && !view.orthographic)
            {
                // The full screen height spans 2 * tan(halfFov) of these angular units.
                float halfHeight = 2f * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
                radius = settings.RevealRadiusScreenHeights * halfHeight;
                softness = settings.RevealSoftnessScreenHeights * halfHeight;
            }

            Matrix4x4 worldToView = view != null ? view.worldToCameraMatrix : Matrix4x4.identity;

            for (int i = 0; i < reveals.Length; i++)
            {
                if (i >= count)
                {
                    reveals[i] = Vector4.zero;
                    continue;
                }

                Vector3 viewPosition = worldToView.MultiplyPoint3x4(tracked[i]);
                reveals[i] = new Vector4(viewPosition.x, viewPosition.y, viewPosition.z, radius);
            }

            Shader.SetGlobalVectorArray(RevealsId, reveals);
            Shader.SetGlobalFloat(RevealCountId, count);
            Shader.SetGlobalFloat(RevealSoftnessId, softness);
        }
    }
}
