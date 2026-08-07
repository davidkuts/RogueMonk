using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Keyboard and gamepad menu navigation without an EventSystem.
    ///
    /// Deliberately hand-rolled: menus here run while <c>Time.timeScale</c> is zero and the
    /// gameplay action map is still enabled, so the standard UI input module would either need
    /// its own map and an EventSystem, or would let a menu press leak into the game. Reading
    /// devices directly keeps a paused game genuinely paused.
    /// </summary>
    /// <summary>Which way a menu's options are laid out, and therefore which stick axis moves them.</summary>
    public enum MenuAxis
    {
        Vertical = 0,
        Horizontal = 1,
    }

    public sealed class MenuSelection
    {
        readonly int optionCount;
        readonly float repeatDelay;
        readonly MenuAxis axis;

        float repeatCooldown;

        public MenuSelection(int optionCount, float repeatDelay = 0.18f, MenuAxis axis = MenuAxis.Vertical)
        {
            this.optionCount = Mathf.Max(1, optionCount);
            this.repeatDelay = repeatDelay;
            this.axis = axis;
        }

        public int Index { get; private set; }

        public void Reset()
        {
            Index = 0;
            repeatCooldown = 0f;
        }

        /// <summary>Advances the highlight. Returns true if the selection moved this frame.</summary>
        public bool Tick(float unscaledDeltaTime)
        {
            if (repeatCooldown > 0f)
                repeatCooldown -= unscaledDeltaTime;

            int step = axis == MenuAxis.Horizontal ? ReadHorizontalStep() : ReadStep();
            if (step == 0 || repeatCooldown > 0f)
                return false;

            Index = (Index + step + optionCount) % optionCount;
            repeatCooldown = repeatDelay;
            return true;
        }

        /// <summary>True on the frame the player confirms the highlighted option.</summary>
        public static bool ConfirmPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                return true;

            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>True on the frame the player asks to back out.</summary>
        public static bool CancelPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            Gamepad pad = Gamepad.current;
            return pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        /// <summary>True on the frame the player asks to open the pause menu.</summary>
        public static bool PausePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            Gamepad pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        /// <summary>
        /// The same reading for a row of options rather than a column. Cards laid out left to right
        /// have to move left to right, or the on-screen prompt is a lie.
        /// </summary>
        static int ReadHorizontalStep()
        {
            float axis = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) axis += 1f;
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) axis -= 1f;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.right.isPressed) axis += 1f;
                if (pad.dpad.left.isPressed) axis -= 1f;
                axis += pad.leftStick.ReadValue().x;
            }

            if (axis > 0.5f) return 1;
            if (axis < -0.5f) return -1;
            return 0;
        }

        static int ReadStep()
        {
            float axis = 0f;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) axis += 1f;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) axis -= 1f;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.isPressed) axis += 1f;
                if (pad.dpad.down.isPressed) axis -= 1f;
                axis += pad.leftStick.ReadValue().y;
            }

            if (axis > 0.5f) return -1;  // up moves toward the first option
            if (axis < -0.5f) return 1;
            return 0;
        }
    }
}
