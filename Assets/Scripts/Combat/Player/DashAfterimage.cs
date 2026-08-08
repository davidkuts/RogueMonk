using System.Collections.Generic;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Spawns fading copies of the character along the dash path.
    ///
    /// This is the dash's animation. A 0.18 s dash is about five frames at 30 fps, so a bespoke
    /// clip would be invisible even if Mixamo had one — which is why the convention in this
    /// genre is to sell a dash with afterimages and a trail while the body keeps whatever pose
    /// it already had. Nothing here touches the simulation; it only reads dash state.
    ///
    /// Works with the gray-box capsules today and with skinned characters later: skinned
    /// meshes are baked to a static snapshot at spawn time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashAfterimage : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField, Tooltip("Renderers to copy. Left empty, every renderer under this object is used.")]
        Renderer[] sources = new Renderer[0];
        [SerializeField] Material ghostMaterial;

        [Header("Trail")]
        [SerializeField, Tooltip("Ghosts spawned across the dash. More = a denser streak.")]
        int ghostCount = 8;
        [SerializeField, Tooltip("How long each ghost lingers after being dropped.")]
        float ghostLifetime = 0.32f;
        [SerializeField, Tooltip("Reserved dash hue — matches the HUD pips so the resource and the move read as the same thing.")]
        Color ghostColor = new Color(0.24f, 0.78f, 0.92f, 0.85f);
        [SerializeField, Tooltip("Extra brightness on a perfect dodge, so a refunded charge is unmistakable.")]
        Color perfectDodgeColor = new Color(1f, 0.86f, 0.35f, 0.85f);

        [Header("Vortex")]
        [SerializeField, Tooltip("Optional. The spin smears in the same dash hue, because it is the same wrist unit doing it — the Second Hand's whole kit reads as one colour family.")]
        PlayerVortex vortex;
        [SerializeField, Tooltip("Gap between ghosts during a spin. Tighter than the dash: a spin has to leave a continuous ring rather than a row of separate bodies.")]
        float vortexGhostInterval = 0.035f;
        [SerializeField, Tooltip("Ghost lifetime multiplier during a spin. Longer, so the earliest ghosts are still up when the body comes back round and the smear closes into a circle.")]
        float vortexGhostLifetimeScale = 1.6f;

        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly List<Mesh> bakedMeshes = new List<Mesh>();

        MaterialPropertyBlock block;
        bool wasDashing;
        bool wasSpinning;
        float spawnInterval;
        float spawnTimer;
        float currentGhostLifetime;
        bool perfectDodgeThisDash;

        static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");

        struct Ghost
        {
            public GameObject Root;
            public Renderer[] Renderers;
            public float Remaining;
            public float Lifetime;
        }

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<PlayerMotor>();
            if (vortex == null)
                vortex = GetComponent<PlayerVortex>();

            if (sources == null || sources.Length == 0)
                sources = GetComponentsInChildren<Renderer>();

            block = new MaterialPropertyBlock();
            currentGhostLifetime = ghostLifetime;
            enabled = motor != null && ghostMaterial != null && sources.Length > 0;
        }

        void OnDestroy()
        {
            for (int i = 0; i < bakedMeshes.Count; i++)
            {
                if (bakedMeshes[i] != null)
                    Destroy(bakedMeshes[i]);
            }
        }

        void Update()
        {
            bool dashing = motor.Dash != null && motor.Dash.IsDashing;

            if (dashing && !wasDashing)
                BeginDash();
            else if (!dashing && wasDashing)
                perfectDodgeThisDash = false;

            // Deliberately NOT emitted during the vortex any more. Body ghosts work for the dash
            // because the body travels; on a stationary spin the torso stays put, every ghost lands
            // on the same spot and the result is a blob in the middle rather than a rotation. The
            // spin is drawn by VortexSmear instead, as ribbons traced by the feet — the only part
            // actually sweeping the circle.
            bool spinning = false;
            if (spinning && !wasSpinning)
                BeginSpin();

            if (dashing)
            {
                // A perfect dodge refunds the charge mid-dash; recolour the remaining ghosts so
                // the reward is visible in the same beat it happens.
                if (!perfectDodgeThisDash && motor.Dash.Charges.Available > 0 && motor.Dash.IsInvulnerable)
                    perfectDodgeThisDash = false;
            }

            if (dashing || spinning)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnGhost();
                    spawnTimer = spawnInterval;
                }
            }

            wasDashing = dashing;
            wasSpinning = spinning;
            FadeGhosts();
        }

        /// <summary>Called by combat when the dash refunds a charge, to recolour the trail.</summary>
        public void FlagPerfectDodge() => perfectDodgeThisDash = true;

        void BeginDash()
        {
            perfectDodgeThisDash = false;
            spawnInterval = ghostCount > 0 ? 0.18f / ghostCount : 0.03f;
            spawnTimer = 0f;
            currentGhostLifetime = ghostLifetime;
        }

        void BeginSpin()
        {
            spawnInterval = Mathf.Max(0.01f, vortexGhostInterval);
            spawnTimer = 0f;
            currentGhostLifetime = ghostLifetime * Mathf.Max(0.1f, vortexGhostLifetimeScale);
        }

        void SpawnGhost()
        {
            var root = new GameObject("DashGhost");
            root.transform.SetPositionAndRotation(transform.position, transform.rotation);
            root.transform.localScale = transform.lossyScale;

            var renderers = new List<Renderer>();
            for (int i = 0; i < sources.Length; i++)
            {
                Renderer source = sources[i];
                if (source == null || !source.enabled)
                    continue;

                Mesh mesh = ResolveMesh(source);
                if (mesh == null)
                    continue;

                var part = new GameObject("Part");
                part.transform.SetParent(root.transform, false);
                part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                part.transform.localScale = source.transform.lossyScale;

                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ghostMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderers.Add(renderer);
            }

            if (renderers.Count == 0)
            {
                Destroy(root);
                return;
            }

            ghosts.Add(new Ghost
            {
                Root = root,
                Renderers = renderers.ToArray(),
                Remaining = currentGhostLifetime,
                Lifetime = currentGhostLifetime,
            });
        }

        Mesh ResolveMesh(Renderer source)
        {
            if (source is SkinnedMeshRenderer skinned)
            {
                // Snapshot the posed mesh; a live SkinnedMeshRenderer would keep animating.
                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                bakedMeshes.Add(baked);
                return baked;
            }

            var filter = source.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        void FadeGhosts()
        {
            float deltaTime = Time.deltaTime;
            Color target = perfectDodgeThisDash ? perfectDodgeColor : ghostColor;

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i];
                ghost.Remaining -= deltaTime;

                if (ghost.Remaining <= 0f || ghost.Root == null)
                {
                    if (ghost.Root != null)
                        Destroy(ghost.Root);

                    ghosts.RemoveAt(i);
                    continue;
                }

                // Linear rather than squared: the squared curve dropped the trail to near
                // nothing almost immediately, which is why the first version barely showed.
                float t = ghost.Lifetime > 0f ? ghost.Remaining / ghost.Lifetime : 0f;
                var colour = new Color(target.r, target.g, target.b, target.a * t);

                for (int r = 0; r < ghost.Renderers.Length; r++)
                {
                    if (ghost.Renderers[r] == null)
                        continue;

                    ghost.Renderers[r].GetPropertyBlock(block);
                    block.SetColor(GhostColorId, colour);
                    ghost.Renderers[r].SetPropertyBlock(block);
                }

                ghosts[i] = ghost;
            }
        }
    }
}
