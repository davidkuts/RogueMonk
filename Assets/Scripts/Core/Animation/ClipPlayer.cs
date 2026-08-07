using Game.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game.Core.Animation
{
    /// <summary>
    /// Plays single clips with a cross-fade, straight through the Playables API.
    ///
    /// This is the free alternative DESIGN.md allows in place of Animancer (a paid Asset Store
    /// package). It exists so the combat simulation stays the only state machine: gameplay code
    /// says "play this clip now" rather than poking parameters at an Animator Controller and
    /// hoping the graph agrees with the frame data.
    ///
    /// With no Animator or no clips it does nothing at all, so the gray-box capsules keep
    /// working until real models arrive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClipPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("Left empty, an Animator on this object or its children is used.")]
        Animator animator;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
        bool[] slotValid = new bool[2];

        int activeSlot;
        float fadeDuration;
        float fadeElapsed;
        bool fading;

        /// <summary>False when there is no Animator to drive; every call becomes a no-op.</summary>
        public bool IsReady { get; private set; }

        /// <summary>The clip currently being faded to, or null.</summary>
        public AnimationClip CurrentClip { get; private set; }

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null)
                return;

            graph = PlayableGraph.Create($"{name}-clips");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(mixer);

            graph.Play();
            IsReady = true;
            GameLog.Debug(LogCategory.Core, $"clip player ready on '{name}'");
        }

        void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        /// <summary>
        /// Cross-fades to <paramref name="clip"/>. Re-playing the clip already showing is
        /// ignored unless <paramref name="restart"/> is set, so a held movement state does not
        /// restart its walk cycle every frame.
        /// </summary>
        public void Play(AnimationClip clip, float fadeSeconds = 0.1f, bool loop = true, bool restart = false, float speed = 1f)
        {
            if (!IsReady || clip == null)
                return;

            if (!restart && CurrentClip == clip)
                return;

            int nextSlot = 1 - activeSlot;
            DestroySlot(nextSlot);

            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetSpeed(speed);
            playable.SetDuration(loop ? double.MaxValue : clip.length);

            mixer.DisconnectInput(nextSlot);
            mixer.ConnectInput(nextSlot, playable, 0);
            slots[nextSlot] = playable;
            slotValid[nextSlot] = true;

            activeSlot = nextSlot;
            CurrentClip = clip;
            fadeDuration = Mathf.Max(0f, fadeSeconds);
            fadeElapsed = 0f;
            fading = fadeDuration > 0f;

            if (!fading)
                ApplyWeights(1f);
        }

        void Update()
        {
            if (!IsReady || !fading)
                return;

            fadeElapsed += Time.deltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);
            ApplyWeights(t);

            if (t >= 1f)
            {
                fading = false;
                DestroySlot(1 - activeSlot);
            }
        }

        void ApplyWeights(float weightToActive)
        {
            mixer.SetInputWeight(activeSlot, weightToActive);
            mixer.SetInputWeight(1 - activeSlot, 1f - weightToActive);
        }

        void DestroySlot(int index)
        {
            if (!slotValid[index])
                return;

            mixer.DisconnectInput(index);
            if (slots[index].IsValid())
                slots[index].Destroy();

            slotValid[index] = false;
        }
    }
}
