using System.Collections.Generic;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Marks one renderer as something that may hide an actor, and carries its own fade.
    ///
    /// <para><b>A component, not a layer.</b> M21C established that walls and floor share the
    /// Default layer here and that the Sailspit's clearance probe depends on it — moving walls onto
    /// an "Occluder" layer would have broken that silently, in a system nobody would think to
    /// re-test. So the cast goes out against Default and the results are filtered through the
    /// collider lookup below: anything not in it is simply not an occluder.</para>
    ///
    /// <para>A live static list rather than a <c>FindObjectsByType</c> sweep, for the reason M21D
    /// recorded: rooms are torn down and rebuilt whole, which is precisely when a scene-wide search
    /// is least trustworthy and most expensive.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallOccluder : MonoBehaviour
    {
        static readonly List<WallOccluder> live = new List<WallOccluder>();
        static readonly Dictionary<Collider, WallOccluder> byCollider = new Dictionary<Collider, WallOccluder>();

        static readonly int FadeId = Shader.PropertyToID("_OccludeFade");
        static readonly int KeepId = Shader.PropertyToID("_OccludeKeep");

        /// <summary>Every occluder currently in the scene.</summary>
        public static IReadOnlyList<WallOccluder> Live => live;

        /// <summary>Resolves a collider a cast hit back to its occluder, or false when it is not one.</summary>
        public static bool TryGet(Collider collider, out WallOccluder occluder)
        {
            if (collider != null)
                return byCollider.TryGetValue(collider, out occluder);

            occluder = null;
            return false;
        }

        readonly OcclusionFadeState state = new OcclusionFadeState();

        Renderer[] renderers;
        Collider[] colliders;
        MaterialPropertyBlock block;

        /// <summary>Last value actually pushed, so an unchanged wall costs nothing per frame.</summary>
        float pushed = -1f;

        bool occluding;

        /// <summary>0 = solid, 1 = fully faded. Read by the Play-Mode checks.</summary>
        public float Fade => state.Current;

        /// <summary>True while something the director tracks is behind this wall.</summary>
        public bool IsOccluding => occluding;

        void Awake()
        {
            renderers = GetComponents<Renderer>();
            colliders = GetComponents<Collider>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            live.Add(this);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    byCollider[colliders[i]] = this;
            }
        }

        void OnDisable()
        {
            live.Remove(this);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    byCollider.Remove(colliders[i]);
            }

            // A wall torn down mid-fade must not come back faded if its renderer is reused.
            ClearFade();
        }

        /// <summary>Snaps back to solid immediately. The director calls this as it shuts down, so a
        /// scene left without one can never be left holding a hole nothing will ever close.</summary>
        public void ClearFade()
        {
            state.Reset();
            occluding = false;
            Push(0f, 1f);
        }

        /// <summary>Clears the flag at the top of a detection pass. Called only on detection frames.</summary>
        public void BeginDetectionPass() => occluding = false;

        /// <summary>Raised once per detection pass by anything this wall is standing in front of.</summary>
        public void MarkOccluding() => occluding = true;

        /// <summary>
        /// Eases toward the current flag and pushes the result. Runs every frame even though
        /// detection does not — the fade has to be smooth at frame rate, not at detection rate.
        /// </summary>
        public void Tick(float deltaTime, OcclusionFadeSettings settings)
        {
            state.Tick(deltaTime, occluding, settings.FadeInSeconds, settings.FadeOutSeconds);
            Push(state.Current, settings.FadedVisibility);
        }

        void Push(float fade, float keep)
        {
            if (Mathf.Approximately(fade, pushed))
                return;

            pushed = fade;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(block);
                block.SetFloat(FadeId, fade);
                block.SetFloat(KeepId, keep);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
