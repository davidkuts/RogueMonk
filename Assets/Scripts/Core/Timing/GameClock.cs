using System;
using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Core.Timing
{
    /// <summary>
    /// The single owner of <c>Time.timeScale</c>.
    ///
    /// Before this existed, combat's hitstop set the time scale directly. Adding a pause menu
    /// would have given the value two independent owners, and whichever wrote last would win —
    /// unpausing in the middle of a hitstop, or a hitstop ending while paused, would have
    /// resumed the game underneath the menu. Everything that wants to stop time now asks here.
    ///
    /// Pause outranks freeze, and a freeze does not tick down while paused, so any hitstop
    /// still owing when the player unpauses plays out properly instead of being eaten.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class GameClock : MonoBehaviour
    {
        /// <summary>There is exactly one clock; callers reach it without needing a wired reference.</summary>
        public static GameClock Instance { get; private set; }

        readonly HitstopController freeze = new HitstopController();
        readonly SlowMotionController slowMotion = new SlowMotionController();

        /// <summary>The freeze timer, exposed for the debug overlay.</summary>
        public HitstopController Freeze => freeze;

        /// <summary>The slow-motion timer. Outranked by both pause and freeze.</summary>
        public SlowMotionController SlowMotion => slowMotion;

        public bool IsPaused { get; private set; }

        /// <summary>True whenever the game clock is stopped, for whatever reason.</summary>
        public bool IsStopped => IsPaused || freeze.IsActive;

        public event Action<bool> PauseChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Apply();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;

            // Never leave the project's time scale at zero because a scene was torn down.
            Time.timeScale = 1f;
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;

            IsPaused = paused;
            GameLog.Info(LogCategory.Core, paused ? "PAUSED" : "RESUMED");
            Apply();
            PauseChanged?.Invoke(paused);
        }

        public void TogglePaused() => SetPaused(!IsPaused);

        /// <summary>Requests a brief freeze — hitstop on a connecting hit.</summary>
        public void RequestFreeze(float seconds) => freeze.Request(seconds);

        /// <summary>Requests a stretch of slow motion — the perfect-dodge focus window.</summary>
        public void RequestSlowMotion(float seconds, float scale) => slowMotion.Request(seconds, scale);

        void Update()
        {
            // Frozen while paused: a hitstop that was running when the player opened the menu
            // resumes on unpause rather than silently expiring behind it. Slow motion is held for
            // the same reason — a focus window should not drain away behind a menu.
            if (!IsPaused)
            {
                freeze.Tick(Time.unscaledDeltaTime);
                slowMotion.Tick(Time.unscaledDeltaTime);
            }

            Apply();
        }

        /// <summary>
        /// One owner, one write, in a fixed priority: pause beats hitstop beats slow motion. A
        /// hitstop landing during a focus window still stops the frame dead, which is what a hit
        /// is supposed to feel like.
        /// </summary>
        void Apply() => Time.timeScale = IsStopped ? 0f : slowMotion.Scale;
    }
}
