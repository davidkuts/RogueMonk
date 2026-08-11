using Game.Combat;
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

        [Header("God mode")]
        [SerializeField, Tooltip("Keyboard toggle for invulnerability, for testing whole runs without health pressure.")]
        Key godModeKey = Key.G;
        [SerializeField, Tooltip("Also bound to the gamepad right trigger (R2).")]
        bool useRightTrigger = true;

        [Header("Debug heal")]
        [SerializeField, Tooltip("Keyboard shortcut for a flat test heal (ignores the Splice ceiling).")]
        Key healKey = Key.H;
        [SerializeField, Tooltip("Also bound to the gamepad left shoulder (L1). R2 was requested but already toggles god mode; L1 was the free button.")]
        bool useLeftShoulder = true;
        [SerializeField, Tooltip("Health restored per press.")]
        float healAmount = 50f;

        bool triggerWasDown;
        bool rightTriggerWasDown;
        PlayerHealth playerHealth;

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

            // Also ahead of the director guard, so god mode works in the lab too.
            if (WasGodTogglePressed())
                ToggleGodMode();

            if (WasHealPressed())
                DebugHeal();

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

        /// <summary>
        /// Flips invulnerability on the player. The reference is re-found on demand because a
        /// scene switch (F9/F10) destroys the player this component last saw; the mode itself
        /// therefore does not survive a scene change, which for a cheat is the safe default.
        /// </summary>
        void ToggleGodMode()
        {
            if (playerHealth == null)
                playerHealth = FindAnyObjectByType<PlayerHealth>();

            if (playerHealth == null)
            {
                GameLog.Warn(LogCategory.Level, "DEBUG: god mode toggle found no player");
                return;
            }

            playerHealth.GodMode = !playerHealth.GodMode;
            GameLog.Warn(LogCategory.Level, $"DEBUG: GOD MODE {(playerHealth.GodMode ? "ON" : "OFF")}");
        }

        bool WasGodTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[godModeKey].wasPressedThisFrame)
                return true;

            if (!useRightTrigger)
                return false;

            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                rightTriggerWasDown = false;
                return false;
            }

            // Analogue triggers have no reliable "pressed this frame", so edge-detect manually.
            bool down = pad.rightTrigger.ReadValue() >= triggerThreshold;
            bool pressed = down && !rightTriggerWasDown;
            rightTriggerWasDown = down;
            return pressed;
        }

        void DebugHeal()
        {
            if (playerHealth == null)
                playerHealth = FindAnyObjectByType<PlayerHealth>();

            if (playerHealth != null)
                playerHealth.DebugHeal(healAmount);
        }

        bool WasHealPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[healKey].wasPressedThisFrame)
                return true;

            if (!useLeftShoulder)
                return false;

            Gamepad pad = Gamepad.current;
            return pad != null && pad.leftShoulder.wasPressedThisFrame;
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
