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

        /// <summary>Centre of the room this wall belongs to, handed over by <see cref="WallOccluderGroup"/>.</summary>
        Vector3 groupCentre;
        bool hasGroupCentre;

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

        /// <summary>
        /// Tells this wall where the middle of its room is, which is the only thing needed to know
        /// which way it faces. Called by <see cref="WallOccluderGroup"/> as it fits the wall.
        /// </summary>
        public void SetGroupCentre(Vector3 centre)
        {
            groupCentre = centre;
            hasGroupCentre = true;
        }

        /// <summary>
        /// True when this wall stands between the camera and the room — the only wall that can hide
        /// anything standing on the floor.
        ///
        /// <para><b>Why this filter exists.</b> The camera looks north and down at ~50°, so only the
        /// south wall is ever genuinely in the way. The side walls run <em>alongside</em> the view
        /// instead: a cast from the camera to somebody hugging one grazes its length, and the
        /// stretch of it nearer the camera than they are overlaps them on screen. The result was a
        /// hole opening in a wall the player was standing in <em>front</em> of, which reads as a
        /// glitch rather than as help (human call 2026-08-12: "only apply this to the south wall").
        /// The far wall has the same problem from the other side and cannot hide anyone at all.</para>
        ///
        /// <para>Decided from geometry rather than from a name or a hand-ticked flag, so a rotated
        /// room, or a camera that is ever re-aimed, picks the right wall without anybody
        /// remembering to re-author four prefabs. A wall with no group — one fitted by hand — cannot
        /// answer the question and is left alone rather than silently excluded.</para>
        /// </summary>
        public bool FacesCamera(Vector3 cameraForwardPlanar, float minDot)
        {
            if (!hasGroupCentre || minDot <= 0f)
                return true;

            Vector3 outward = transform.position - groupCentre;
            outward.y = 0f;

            // A wall sitting on the room's own centre line — a free-standing pillar — has no
            // outward direction, so the test cannot speak for it.
            if (outward.sqrMagnitude < 0.0001f)
                return true;

            // Outward points away from the room. It faces the camera when it points back along the
            // view direction, which is what makes the dot negative.
            return Vector3.Dot(outward.normalized, cameraForwardPlanar) <= -minDot;
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
