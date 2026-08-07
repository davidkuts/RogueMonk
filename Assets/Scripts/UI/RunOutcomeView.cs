using Game.Combat;
using Game.Core.Diagnostics;
using Game.Level;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Tells the player, unmistakably, that the run ended and how. A placeholder for M7's
    /// proper death screen — the run stats shown here are the ones RunContext has been
    /// accumulating all along, so M7 is a visual pass rather than new plumbing.
    ///
    /// Drawn with IMGUI for the same reason as the debug overlay: it must work in any build
    /// with no font assets, and it must not be mistaken for finished UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunOutcomeView : MonoBehaviour
    {
        enum Outcome
        {
            Running,
            Died,
            Completed,
        }

        [SerializeField] LevelDirector director;
        [SerializeField] PlayerHealth health;

        [Header("Input")]
        [SerializeField] Key restartKey = Key.R;
        [SerializeField, Tooltip("Restarts the same seed, for reproducing a run.")]
        Key restartSameSeedKey = Key.T;

        Outcome outcome = Outcome.Running;
        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        Texture2D panelTexture;
        GUIStyle panelStyle;

        void Awake()
        {
            if (director == null) director = FindAnyObjectByType<LevelDirector>();
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();

            if (director != null) director.LevelCompleted += OnLevelCompleted;
            if (health != null) health.Died += OnPlayerDied;
        }

        void OnDestroy()
        {
            if (director != null) director.LevelCompleted -= OnLevelCompleted;
            if (health != null) health.Died -= OnPlayerDied;
            if (panelTexture != null) Destroy(panelTexture);
        }

        void OnLevelCompleted() => outcome = Outcome.Completed;

        void OnPlayerDied() => outcome = Outcome.Died;

        void Update()
        {
            if (outcome == Outcome.Running)
                return;

            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;

            bool restart = (keyboard != null && keyboard[restartKey].wasPressedThisFrame)
                           || (pad != null && pad.startButton.wasPressedThisFrame);
            bool restartSame = keyboard != null && keyboard[restartSameSeedKey].wasPressedThisFrame;

            if (!restart && !restartSame)
                return;

            GameLog.Info(LogCategory.Level, $"restart requested ({(restartSame ? "same seed" : "new seed")})");
            outcome = Outcome.Running;

            if (health != null)
                health.ResetForNewRun();

            if (director != null)
                director.Restart(restartSame);
        }

        void OnGUI()
        {
            if (outcome == Outcome.Running)
                return;

            EnsureStyles();

            bool won = outcome == Outcome.Completed;
            titleStyle.normal.textColor = won ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.35f, 0.32f);

            float width = Mathf.Min(Screen.width - 80f, 620f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.28f, width, 220f);
            GUI.Box(rect, GUIContent.none, panelStyle);

            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, rect.height - 40f));
            GUILayout.Label(won ? "LEVEL COMPLETE" : "YOU DIED", titleStyle);
            GUILayout.Space(10f);

            if (director != null && director.Run != null)
            {
                var run = director.Run;
                GUILayout.Label(
                    $"seed {run.Seed}\n" +
                    $"rooms cleared   {run.RoomsCleared}\n" +
                    $"enemies killed  {run.EnemiesKilled}\n" +
                    $"damage dealt    {run.DamageDealt:0}\n" +
                    $"damage taken    {run.DamageTaken:0}\n" +
                    $"perfect dodges  {run.PerfectDodges}\n" +
                    $"time            {run.ElapsedSeconds:0.0}s",
                    bodyStyle);
            }

            GUILayout.Space(10f);
            GUILayout.Label("R or Start - new run       T - replay this seed", bodyStyle);
            GUILayout.EndArea();
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            bodyStyle.normal.textColor = new Color(0.88f, 0.9f, 0.94f);

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.06f, 0.92f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
        }
    }
}
