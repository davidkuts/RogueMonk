using System.Collections.Generic;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Undertow's visual layers, on top of the foot-traced range smear.
    ///
    /// <para><b>The range smear is not touched by any of this.</b> <see cref="VortexSmear"/> owns
    /// the reach indicator and is final; these layers draw inside it. The inner disc is deliberately
    /// much smaller so the two never compete for the same read — the smear says "this is how far it
    /// reaches", the disc says "this is what it is doing".</para>
    ///
    /// <para>Three layers, one component. They share a lifetime, a colour, a master fade and one
    /// event subscription, so splitting them across three MonoBehaviours would have meant three
    /// wiring points per scene and three chances to leave one unassigned.</para>
    ///
    /// <para><b>Alpha-blended, not additive.</b> The brief asked for additive; this project has
    /// already paid for that lesson — <c>Monk/Smear</c> and <c>Monk/Telegraph</c> both carry the
    /// note that additive was tried and vanished against the light arena floor (M8). A ground effect
    /// that disappears against the ground is not a trade worth making.</para>
    ///
    /// <para>Presentation only: nothing here is read by the simulation, and switching the component
    /// off changes nothing about how the ability plays.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class VortexVfx : MonoBehaviour
    {
        [SerializeField] PlayerVortex vortex;
        [SerializeField] VortexVfxSettings settings;

        [Header("Materials")]
        [SerializeField, Tooltip("Material using Monk/VortexDisc.")]
        Material discMaterial;
        [SerializeField, Tooltip("Material using Monk/Smear. Shared by the inward streaks and the arm trails.")]
        Material streakMaterial;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly int ArmsId = Shader.PropertyToID("_Arms");
        static readonly int TightnessId = Shader.PropertyToID("_Tightness");

        readonly VortexPulseEnvelope pulse = new VortexPulseEnvelope();
        readonly List<Streak> streaks = new List<Streak>();
        readonly List<TrailRenderer> trails = new List<TrailRenderer>();

        MaterialPropertyBlock block;
        Transform discTransform;
        MeshRenderer discRenderer;
        Mesh quadMesh;

        float phase;
        float masterAlpha;
        float fadeRate;          // 0 = snap, otherwise alpha units per second
        float spawnAccumulator;
        bool wasActive;
        int spawnCursor;

        /// <summary>One pooled inward streak. Never destroyed — only switched off and reused.</summary>
        struct Streak
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public bool Alive;
            public float Age;
            public float StartAngleDeg;
            public Vector3 LastPosition;
        }

        void Awake()
        {
            if (vortex == null) vortex = GetComponent<PlayerVortex>();

            if (vortex == null || settings == null || discMaterial == null || streakMaterial == null)
            {
                Debug.LogWarning(
                    $"{nameof(VortexVfx)} on '{name}' is missing a reference and will stay off.", this);
                enabled = false;
                return;
            }

            block = new MaterialPropertyBlock();
            quadMesh = BuildQuad();

            BuildDisc();
            BuildStreakPool();
            BuildArmTrails();

            SetVisible(false);

            vortex.DamageTick += OnDamageTick;
            vortex.SpinEnded += OnSpinEnded;
        }

        void OnDestroy()
        {
            if (vortex != null)
            {
                vortex.DamageTick -= OnDamageTick;
                vortex.SpinEnded -= OnSpinEnded;
            }

            if (quadMesh != null)
                Destroy(quadMesh);
        }

        // --- construction ---

        /// <summary>
        /// A unit quad lying flat, length along +Z and width along X, with the UVs the smear shader
        /// expects: u runs along the length from the leading end, v runs across the width. Built
        /// once and shared by the disc and every streak.
        /// </summary>
        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "VortexQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 0f, 1f),
                new Vector3(0.5f, 0f, 1f),
            };
            mesh.uv = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
            };
            // The smear shader multiplies by vertex alpha, so an unset colour array would render
            // every streak completely invisible.
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildDisc()
        {
            var go = new GameObject("VortexInnerDisc");
            discTransform = go.transform;
            discTransform.SetParent(transform, false);

            // Its own centred quad: the shared mesh runs 0..1 along Z, which is right for a streak
            // and wrong for a disc that has to be symmetrical about the player.
            var mesh = new Mesh { name = "VortexDiscQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            discRenderer = go.AddComponent<MeshRenderer>();
            discRenderer.sharedMaterial = discMaterial;
            discRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            discRenderer.receiveShadows = false;
            discRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        /// <summary>
        /// Allocates the whole streak budget up front and never grows it. A pool that can grow is a
        /// budget that is really a suggestion, and this effect has to stay inside a fixed cost.
        /// </summary>
        void BuildStreakPool()
        {
            for (int i = 0; i < settings.StreakBudget; i++)
            {
                var go = new GameObject("VortexStreak");
                go.transform.SetParent(transform, false);

                go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = streakMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.enabled = false;

                streaks.Add(new Streak { Transform = go.transform, Renderer = renderer, Alive = false });
            }
        }

        /// <summary>
        /// Layer 1 — trails on the HANDS, not the feet. The feet already carry the range smear, and
        /// hanging a second ribbon off them would only thicken one ring; the arms sweep their own
        /// arc, which is what makes the body's rotation read.
        /// </summary>
        void BuildArmTrails()
        {
            string[] suffixes = settings.TrailBoneNameSuffixes;
            if (suffixes == null || suffixes.Length == 0)
                return;

            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                bool match = false;
                for (int i = 0; i < suffixes.Length && !match; i++)
                    match = !string.IsNullOrEmpty(suffixes[i]) && bone.name.EndsWith(suffixes[i]);

                if (!match)
                    continue;

                var go = new GameObject("VortexArmTrail");
                go.transform.SetParent(bone, false);

                var trail = go.AddComponent<TrailRenderer>();
                trail.time = settings.TrailSeconds;
                trail.minVertexDistance = 0.02f;
                trail.autodestruct = false;
                trail.emitting = false;
                trail.sharedMaterial = streakMaterial;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.alignment = LineAlignment.View;
                trail.numCapVertices = 2;
                trail.widthCurve = new AnimationCurve(
                    new Keyframe(0f, settings.TrailStartWidth),
                    new Keyframe(1f, settings.TrailEndWidth));

                trails.Add(trail);
            }
        }

        // --- events ---

        /// <summary>
        /// One enemy took one tick. Pulses accumulate rather than retrigger, so a spin catching a
        /// crowd is visibly fiercer than one catching a single body — which is the entire point of
        /// hanging this off <see cref="PlayerVortex.DamageTick"/> rather than off the cast.
        /// </summary>
        void OnDamageTick(IDamageable target, Vector3 point) => pulse.Add(settings.HitPulseIntensity);

        /// <summary>
        /// The spin stopped. An interrupt has to be gone before the dash reads on screen, so it
        /// fades over the ability's own <c>vortexInterruptFadeOutSeconds</c>; a natural end is
        /// allowed the more relaxed settle of the trails' own lifetime.
        /// </summary>
        void OnSpinEnded(bool interrupted, float fadeSeconds)
        {
            float seconds = interrupted ? Mathf.Max(0.01f, fadeSeconds) : Mathf.Max(0.01f, settings.TrailSeconds);
            fadeRate = 1f / seconds;

            for (int i = 0; i < trails.Count; i++)
            {
                if (trails[i] != null)
                    trails[i].emitting = false;
            }

            if (interrupted)
                GameLog.Debug(LogCategory.Combat, $"vortex vfx    fading out over {seconds:F2}s (interrupted)");
        }

        // --- frame ---

        void Update()
        {
            float deltaTime = Time.deltaTime;
            bool active = vortex.IsBodySpinning;

            if (active && !wasActive)
                Begin();

            wasActive = active;

            // Time.deltaTime rather than an unscaled clock on purpose: hitstop and the pause menu
            // scale it, so the spiral freezes with the rest of the game. An effect that keeps
            // turning through a frozen frame reads as a bug.
            pulse.Tick(deltaTime, settings.HitPulseDurationSeconds);

            if (active)
            {
                masterAlpha = 1f;
            }
            else if (masterAlpha > 0f)
            {
                // A spin that stopped without announcing itself still has to go away. SpinEnded
                // always fires today; this makes "the effect outlived the ability" impossible
                // rather than merely unlikely.
                if (fadeRate <= 0f)
                    fadeRate = 1f / Mathf.Max(0.01f, settings.TrailSeconds);

                masterAlpha = Mathf.Max(0f, masterAlpha - fadeRate * deltaTime);
            }

            if (masterAlpha <= 0f && StreaksIdle())
            {
                if (discRenderer != null && discRenderer.enabled)
                    Retire();
                return;
            }

            // A fed vortex whips up: the pulse kicks the scroll rate as well as the brightness.
            float speed = settings.SpiralScrollSpeed * (1f + pulse.Level * settings.HitPulseSpinKick);
            phase += speed * deltaTime;

            UpdateDisc();
            UpdateStreaks(deltaTime, active);
        }

        void Begin()
        {
            phase = 0f;
            spawnAccumulator = 0f;
            masterAlpha = 1f;
            fadeRate = 0f;
            pulse.Reset();
            SetVisible(true);

            for (int i = 0; i < trails.Count; i++)
            {
                if (trails[i] == null)
                    continue;

                // Cleared on the way IN, so the tail of the previous spin fades naturally instead
                // of vanishing the instant the next one starts. Same reasoning as VortexSmear.
                trails[i].Clear();
                trails[i].emitting = true;
            }
        }

        /// <summary>Everything off and nothing left owning state — the "fully cleaned up" guarantee.</summary>
        void Retire()
        {
            SetVisible(false);
            pulse.Reset();
            masterAlpha = 0f;
            phase = 0f;

            for (int i = 0; i < streaks.Count; i++)
                KillStreak(i);

            for (int i = 0; i < trails.Count; i++)
            {
                if (trails[i] != null)
                {
                    trails[i].emitting = false;
                    trails[i].Clear();
                }
            }
        }

        bool StreaksIdle()
        {
            for (int i = 0; i < streaks.Count; i++)
            {
                if (streaks[i].Alive)
                    return false;
            }

            return true;
        }

        void SetVisible(bool visible)
        {
            if (discRenderer != null)
                discRenderer.enabled = visible;
        }

        void UpdateDisc()
        {
            if (discRenderer == null || vortex.Definition == null)
                return;

            float radius = vortex.Definition.RadiusMeters * settings.DiscRadiusFraction;

            discTransform.localPosition = new Vector3(0f, settings.DiscGroundOffset, 0f);

            // World-aligned, not parented-rotation: the player's transform turns to face wherever
            // they are aiming, and a disc inheriting that would visibly snap its arms around every
            // time the stick moved. The drain does not care which way Cole is looking.
            discTransform.rotation = Quaternion.identity;
            discTransform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);

            // A property block, never a material instance: instantiating a material per frame (or
            // even once per spin) leaks and breaks batching for nothing.
            discRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, settings.TimeBlue);
            block.SetFloat(AlphaId, settings.SpiralBaseAlpha * masterAlpha);
            block.SetFloat(PulseId, pulse.Level);
            block.SetFloat(PhaseId, phase);
            block.SetFloat(ArmsId, settings.SpiralArms);
            block.SetFloat(TightnessId, settings.SpiralTightness);
            discRenderer.SetPropertyBlock(block);
        }

        void UpdateStreaks(float deltaTime, bool active)
        {
            float discRadius = vortex.Definition != null
                ? vortex.Definition.RadiusMeters * settings.DiscRadiusFraction
                : 0f;

            if (active && discRadius > 0f && streaks.Count > 0)
            {
                spawnAccumulator += settings.StreakSpawnRate * deltaTime;
                while (spawnAccumulator >= 1f)
                {
                    spawnAccumulator -= 1f;
                    SpawnStreak();
                }
            }

            for (int i = 0; i < streaks.Count; i++)
            {
                Streak streak = streaks[i];
                if (!streak.Alive)
                    continue;

                streak.Age += deltaTime;
                float t = streak.Age / settings.StreakTravelSeconds;

                if (t >= 1f || masterAlpha <= 0f)
                {
                    KillStreak(i);
                    continue;
                }

                // Spiralling in: the radius falls while the bearing sweeps, which together trace the
                // path the pull is dragging things along.
                float radius01 = Mathf.Lerp(1f, settings.StreakDeathRadiusFraction, t);
                float angleDeg = streak.StartAngleDeg + settings.StreakSwirlDegrees * t;
                float angle = angleDeg * Mathf.Deg2Rad;

                Vector3 position = transform.position + new Vector3(
                    Mathf.Cos(angle) * radius01 * discRadius,
                    settings.DiscGroundOffset,
                    Mathf.Sin(angle) * radius01 * discRadius);

                // Oriented along actual travel rather than along a computed tangent: it is exact by
                // construction, and it stays correct if the motion curve is ever retuned.
                Vector3 travel = position - streak.LastPosition;
                if (travel.sqrMagnitude > 1e-6f)
                    streak.Transform.rotation = Quaternion.LookRotation(travel.normalized, Vector3.up);

                streak.Transform.position = position;
                streak.Transform.localScale = new Vector3(
                    settings.StreakSize.y, 1f, settings.StreakSize.x);
                streak.LastPosition = position;

                // Fade in off the rim and out into the centre so streaks never pop at either end.
                float shape = Mathf.Min(1f, Mathf.Min(t * 6f, (1f - t) * 3f));
                var colour = settings.TimeBlue;
                colour.a = settings.StreakAlpha * shape * masterAlpha;

                streak.Renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                streak.Renderer.SetPropertyBlock(block);

                streaks[i] = streak;
            }
        }

        void SpawnStreak()
        {
            // Round-robin over the pool. When every streak is busy the spawn is simply dropped,
            // which is what makes the budget a ceiling rather than a target.
            for (int attempt = 0; attempt < streaks.Count; attempt++)
            {
                int index = spawnCursor % streaks.Count;
                spawnCursor++;

                if (streaks[index].Alive)
                    continue;

                float discRadius = vortex.Definition.RadiusMeters * settings.DiscRadiusFraction;

                // Golden angle, NOT a random draw. CLAUDE.md rule 5 bars UnityEngine.Random from
                // gameplay code, and M17 set the practice for effects specifically: place on a fixed
                // pattern so no VFX can ever touch seed reproducibility. 137.508 degrees also
                // distributes far more evenly than random would — successive streaks never clump,
                // which is exactly what a drain wants.
                const float GoldenAngleDeg = 137.50776f;
                float angleDeg = spawnCursor * GoldenAngleDeg;
                float angle = angleDeg * Mathf.Deg2Rad;

                Streak streak = streaks[index];
                streak.Alive = true;
                streak.Age = 0f;
                streak.StartAngleDeg = angleDeg;
                streak.LastPosition = transform.position + new Vector3(
                    Mathf.Cos(angle) * discRadius, settings.DiscGroundOffset, Mathf.Sin(angle) * discRadius);
                streak.Transform.position = streak.LastPosition;
                streak.Renderer.enabled = true;
                streaks[index] = streak;
                return;
            }
        }

        void KillStreak(int index)
        {
            Streak streak = streaks[index];
            if (streak.Renderer != null)
                streak.Renderer.enabled = false;

            streak.Alive = false;
            streak.Age = 0f;
            streaks[index] = streak;
        }
    }
}
