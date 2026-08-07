using System.Text;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Rng;
using Game.Core.Timing;
using Game.Level;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// End-of-run screen with stats and hold-to-retry (DESIGN.md § RNG &amp; death flow).
    ///
    /// The hold is deliberate rather than a tap: a run ends at the exact moment the player is
    /// mashing attack and dash, and a single-press retry would be triggered by the input that
    /// killed them before they had read anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeathScreenView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text titleLabel;
        [SerializeField] Text statsLabel;
        [SerializeField] Text hintLabel;
        [SerializeField] Image holdFill;
        [SerializeField] LevelDirector director;
        [SerializeField] PlayerHealth health;

        [Header("Colours")]
        [SerializeField] Color deathColor = new Color(1f, 0.36f, 0.33f);
        [SerializeField] Color victoryColor = new Color(1f, 0.84f, 0.36f);

        [Header("Feel")]
        [SerializeField, Tooltip("How long retry must be held. Long enough that the input that killed you cannot trigger it.")]
        float holdSeconds = 0.7f;
        [SerializeField, Tooltip("Grace period before input is accepted at all, so the stats can be read.")]
        float inputLockoutSeconds = 0.6f;

        readonly StringBuilder builder = new StringBuilder(256);

        bool won;
        float holdProgress;
        float lockout;

        public bool IsShowing { get; private set; }

        void Awake()
        {
            if (director == null) director = FindAnyObjectByType<LevelDirector>();
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();

            if (director != null)
                director.LevelCompleted += OnLevelCompleted;

            if (health != null)
                health.Died += OnPlayerDied;

            Hide();
        }

        void OnDestroy()
        {
            if (director != null) director.LevelCompleted -= OnLevelCompleted;
            if (health != null) health.Died -= OnPlayerDied;
        }

        void OnLevelCompleted() => Show(true);

        void OnPlayerDied() => Show(false);

        void Show(bool victory)
        {
            if (IsShowing)
                return;

            won = victory;
            IsShowing = true;
            holdProgress = 0f;
            lockout = inputLockoutSeconds;

            if (panel != null)
                panel.SetActive(true);

            if (titleLabel != null)
            {
                titleLabel.text = victory ? "LEVEL COMPLETE" : "YOU DIED";
                titleLabel.color = victory ? victoryColor : deathColor;
            }

            if (statsLabel != null)
                statsLabel.text = BuildStats();

            if (hintLabel != null)
                hintLabel.text = "hold  Enter / Cross  to run again        R  replay this seed";

            // The run is over; stop the world behind the screen.
            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(true);

            GameLog.Info(LogCategory.UI, victory ? "death screen: LEVEL COMPLETE" : "death screen: YOU DIED");
        }

        void Hide()
        {
            IsShowing = false;
            holdProgress = 0f;

            if (panel != null)
                panel.SetActive(false);

            if (holdFill != null)
                holdFill.fillAmount = 0f;
        }

        string BuildStats()
        {
            if (director == null || director.Run == null)
                return string.Empty;

            RunContext run = director.Run;
            builder.Clear();
            builder.AppendLine($"seed              {run.Seed}");
            builder.AppendLine($"rooms cleared     {run.RoomsCleared} / {(director.Plan != null ? director.Plan.RoomCount : 0)}");
            builder.AppendLine($"enemies killed    {run.EnemiesKilled}");
            builder.AppendLine($"damage dealt      {run.DamageDealt:0}");
            builder.AppendLine($"damage taken      {run.DamageTaken:0}");
            builder.AppendLine($"perfect dodges    {run.PerfectDodges}");
            builder.Append($"time              {run.ElapsedSeconds:0.0}s");
            return builder.ToString();
        }

        void Update()
        {
            if (!IsShowing)
                return;

            float deltaTime = Time.unscaledDeltaTime;

            if (lockout > 0f)
            {
                lockout -= deltaTime;
                return;
            }

            // Same seed is a single press: it is a debug aid, not the main path.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Restart(sameSeed: true);
                return;
            }

            bool holding = IsRetryHeld();
            holdProgress = holding
                ? Mathf.Min(holdSeconds, holdProgress + deltaTime)
                : Mathf.Max(0f, holdProgress - deltaTime * 2f);

            if (holdFill != null)
                holdFill.fillAmount = holdSeconds > 0f ? holdProgress / holdSeconds : 1f;

            if (holdProgress >= holdSeconds)
                Restart(sameSeed: false);
        }

        static bool IsRetryHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed))
                return true;

            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonSouth.isPressed;
        }

        void Restart(bool sameSeed)
        {
            GameLog.Info(LogCategory.UI, $"death screen: restart ({(sameSeed ? "same" : "new")} seed)");
            Hide();

            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(false);

            if (health != null)
                health.ResetForNewRun();

            if (director != null)
                director.RestartRun(sameSeed);
        }
    }
}
