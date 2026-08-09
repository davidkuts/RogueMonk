using Game.Core.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Level
{
    /// <summary>
    /// Development shortcuts for reaching parts of a run quickly. Temporary: every cheat here
    /// logs at Warning level so a playtest log can never be mistaken for an honest one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugCheats : MonoBehaviour
    {
        [SerializeField] LevelDirector director;

        [Header("Clear room")]
        [SerializeField, Tooltip("Keyboard shortcut to clear the current room.")]
        Key clearRoomKey = Key.K;
        [SerializeField, Tooltip("Also bound to the gamepad left trigger (L2).")]
        bool useLeftTrigger = true;
        [SerializeField, Range(0.1f, 1f)] float triggerThreshold = 0.6f;

        [Header("Enemy lab")]
        [SerializeField, Tooltip("Loads the Biome 1 enemy lab. CLAUDE.md requires playtesting in a standalone build, and the roster is not in the level generator yet, so this is the only way to reach it from one.")]
        Key enemyLabKey = Key.F9;

        [SerializeField, Tooltip("Scene name of the lab. Must also be listed in Build Settings or the load silently does nothing in a build.")]
        string enemyLabScene = "EnemyLab";

        [SerializeField, Tooltip("Returns to the ordinary game scene from the lab.")]
        Key mainSceneKey = Key.F10;

        [SerializeField] string mainScene = "GrayboxArena";

        bool triggerWasDown;

        void Awake()
        {
            if (director == null)
                director = FindAnyObjectByType<LevelDirector>();
        }

        void Update()
        {
            // Checked before the director guard: the lab has no LevelDirector at all, so anything
            // behind that guard is unreachable from inside it — including the key back out.
            if (TryHandleSceneSwitch())
                return;

            if (director == null || director.CurrentRoom == null)
                return;

            if (!WasClearPressed())
                return;

            // Pressing it again in an already-cleared room used to do nothing at all, which
            // read as the button being broken. Now it moves you on.
            if (director.CurrentRoom.IsCleared)
            {
                GameLog.Warn(LogCategory.Level, "DEBUG: room already cleared - skipping to the next one");
                director.SkipToNextRoom();
            }
            else
            {
                GameLog.Warn(LogCategory.Level, "DEBUG: clear-room requested");
                director.CurrentRoom.ForceClear();
            }
        }

        /// <summary>
        /// Jumps between the ordinary game and the enemy lab.
        ///
        /// <para>Exists because CLAUDE.md requires feel to be judged in a standalone build, and the
        /// Biome 1 roster is deliberately not in the level generator yet — without this the seven
        /// new enemies would be editor-only, which is exactly the situation the build rule exists
        /// to prevent.</para>
        /// </summary>
        /// <returns>True when a scene load was started, so the caller stops processing this frame.</returns>
        bool TryHandleSceneSwitch()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (keyboard[enemyLabKey].wasPressedThisFrame && !IsActiveScene(enemyLabScene))
                return LoadScene(enemyLabScene);

            if (keyboard[mainSceneKey].wasPressedThisFrame && !IsActiveScene(mainScene))
                return LoadScene(mainScene);

            return false;
        }

        static bool IsActiveScene(string sceneName) =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == sceneName;

        bool LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            // No need to restore Time.timeScale here: GameClock already resets it to 1 in
            // OnDestroy precisely so a scene torn down while paused or in hitstop cannot leave the
            // next one frozen.
            GameLog.Warn(LogCategory.Level, $"DEBUG: loading scene '{sceneName}'");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            return true;
        }

        bool WasClearPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[clearRoomKey].wasPressedThisFrame)
                return true;

            if (!useLeftTrigger)
                return false;

            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                triggerWasDown = false;
                return false;
            }

            // Analogue triggers have no reliable "pressed this frame", so edge-detect manually.
            bool down = pad.leftTrigger.ReadValue() >= triggerThreshold;
            bool pressed = down && !triggerWasDown;
            triggerWasDown = down;
            return pressed;
        }
    }
}
