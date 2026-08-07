using Game.Core.Diagnostics;
using Game.Core.Timing;
using Game.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// ESC / Start pause menu: resume, restart level, quit (DESIGN.md § UI).
    ///
    /// Pausing goes through <see cref="GameClock"/> rather than touching Time.timeScale, so it
    /// coexists with combat hitstop instead of fighting it — and a hitstop still owing when the
    /// player pauses resumes correctly afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Text[] optionLabels = new Text[0];
        [SerializeField] Text hintLabel;
        [SerializeField] LevelDirector director;
        [SerializeField] DeathScreenView deathScreen;

        [Header("Colours")]
        [SerializeField] Color normalColor = new Color(0.72f, 0.76f, 0.82f);
        [SerializeField] Color selectedColor = new Color(1f, 0.85f, 0.35f);

        static readonly string[] Options = { "Resume", "Restart level", "Quit" };

        MenuSelection selection;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (director == null) director = FindAnyObjectByType<LevelDirector>();
            if (deathScreen == null) deathScreen = FindAnyObjectByType<DeathScreenView>();

            selection = new MenuSelection(Options.Length);
            SetOpen(false);

            for (int i = 0; i < optionLabels.Length && i < Options.Length; i++)
            {
                if (optionLabels[i] != null)
                    optionLabels[i].text = Options[i];
            }

            if (hintLabel != null)
                hintLabel.text = "up / down to choose      Enter or Cross to confirm      Esc to resume";
        }

        void Update()
        {
            // The death screen owns input once the run is over; pausing a finished run is noise.
            if (deathScreen != null && deathScreen.IsShowing)
                return;

            if (!IsOpen)
            {
                if (MenuSelection.PausePressed())
                    SetOpen(true);

                return;
            }

            selection.Tick(Time.unscaledDeltaTime);
            Paint();

            if (MenuSelection.CancelPressed())
            {
                SetOpen(false);
                return;
            }

            if (MenuSelection.ConfirmPressed())
                Activate(selection.Index);
        }

        void Activate(int index)
        {
            switch (index)
            {
                case 0:
                    SetOpen(false);
                    break;

                case 1:
                    GameLog.Info(LogCategory.UI, "pause menu: restart level");
                    SetOpen(false);
                    if (director != null)
                        director.RestartRun(sameSeed: false);
                    break;

                case 2:
                    GameLog.Info(LogCategory.UI, "pause menu: quit");
                    Quit();
                    break;
            }
        }

        void SetOpen(bool open)
        {
            IsOpen = open;

            if (panel != null)
                panel.SetActive(open);

            if (open)
                selection.Reset();

            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(open);

            Paint();
        }

        void Paint()
        {
            for (int i = 0; i < optionLabels.Length; i++)
            {
                if (optionLabels[i] == null)
                    continue;

                bool active = i == selection.Index;
                optionLabels[i].color = active ? selectedColor : normalColor;
                optionLabels[i].text = (active ? "> " : "   ") + (i < Options.Length ? Options[i] : string.Empty);
            }
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
