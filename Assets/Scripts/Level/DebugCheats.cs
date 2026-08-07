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

        bool triggerWasDown;

        void Awake()
        {
            if (director == null)
                director = FindAnyObjectByType<LevelDirector>();
        }

        void Update()
        {
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
