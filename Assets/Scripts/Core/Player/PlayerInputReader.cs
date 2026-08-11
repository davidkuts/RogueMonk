using Game.Core.Timing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.Player
{
    /// <summary>
    /// Thin adapter over the Input System asset. Owns action lookup and enabling so no
    /// gameplay script touches InputAction plumbing. Device/control-scheme differences
    /// stop here — callers only see conditioned values, and so does "the game is not accepting
    /// input right now".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        const string PlayerMapName = "Player";
        const string MoveActionName = "Move";
        const string DashActionName = "Dash";
        const string AttackActionName = "Attack";
        const string RiposteActionName = "Riposte";
        const string VortexActionName = "Vortex";
        const string InteractActionName = "Interact";
        const string StopgapActionName = "Stopgap";

        // Binding groups as named in MonkControls.inputactions. Prompts resolve through these so
        // the glyph matches the device actually in the player's hands.
        const string GamepadSchemeGroup = "Gamepad";
        const string KeyboardSchemeGroup = "Keyboard&Mouse";

        [SerializeField] InputActionAsset actions;

        InputActionMap playerMap;
        InputAction moveAction;
        InputAction dashAction;
        InputAction attackAction;
        InputAction riposteAction;
        InputAction vortexAction;
        InputAction interactAction;
        InputAction stopgapAction;

        /// <summary>
        /// True while a menu owns the screen and gameplay must not read the pad.
        ///
        /// Menus run with the gameplay map still enabled (they read devices directly so a paused
        /// game stays genuinely paused), and they share buttons with it — confirm is Cross, which
        /// is also Dash. Without this gate, choosing a boon buffers a dash that fires the instant
        /// the game resumes, and the player is thrown across the room by a menu press.
        ///
        /// Deliberately keyed on <c>IsPaused</c> rather than <c>IsStopped</c>: hitstop also stops
        /// the clock, and dropping input through every hit would defeat the input buffer, which
        /// exists precisely so a press during hitstop survives.
        /// </summary>
        public static bool GameplayInputSuspended =>
            GameClock.Instance != null && GameClock.Instance.IsPaused;

        /// <summary>Raw (unconditioned) move vector. Deadzone/curve are applied in the simulation.</summary>
        public Vector2 MoveAxis =>
            moveAction != null && !GameplayInputSuspended ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>True on the frame the dash button went down.</summary>
        public bool DashPressedThisFrame =>
            dashAction != null && !GameplayInputSuspended && dashAction.WasPressedThisFrame();

        /// <summary>True on the frame the attack button went down.</summary>
        public bool AttackPressedThisFrame =>
            attackAction != null && !GameplayInputSuspended && attackAction.WasPressedThisFrame();

        /// <summary>True on the frame the riposte button went down. Only does anything while armed.</summary>
        public bool RipostePressedThisFrame =>
            riposteAction != null && !GameplayInputSuspended && riposteAction.WasPressedThisFrame();

        /// <summary>True on the frame the vortex button went down (○ / B, or E).</summary>
        public bool VortexPressedThisFrame =>
            vortexAction != null && !GameplayInputSuspended && vortexAction.WasPressedThisFrame();

        /// <summary>
        /// True on the frame the interact button went down (R1 / RB, or F). Rebinding-safe:
        /// everything downstream references this action, never a physical button.
        /// </summary>
        public bool InteractPressedThisFrame =>
            interactAction != null && !GameplayInputSuspended && interactAction.WasPressedThisFrame();

        /// <summary>
        /// True on the frame the Stopgap button went down (D-pad up, or R).
        ///
        /// <para>The D-pad was reserved for this in M16 and left unbound until the effects
        /// existed. ⚠️ REWARDS.md §11 open decision 2 — WHICH d-pad direction — is answered as Up
        /// here; the other three stay free for the rest of the consumable slots if they arrive.</para>
        /// </summary>
        public bool StopgapPressedThisFrame =>
            stopgapAction != null && !GameplayInputSuspended && stopgapAction.WasPressedThisFrame();

        /// <summary>
        /// True when a pad was the last thing the player touched.
        ///
        /// <para>Decided by comparing device update times rather than by a PlayerInput component,
        /// because the whole input layer here is the raw asset — and it means a player who picks
        /// up the pad mid-run sees pad glyphs on the very next prompt, with no event plumbing.</para>
        /// </summary>
        public static bool UsingGamepad
        {
            get
            {
                Gamepad pad = Gamepad.current;
                if (pad == null)
                    return false;

                Keyboard keyboard = Keyboard.current;
                Mouse mouse = Mouse.current;

                double keyboardTime = keyboard != null ? keyboard.lastUpdateTime : double.NegativeInfinity;
                double mouseTime = mouse != null ? mouse.lastUpdateTime : double.NegativeInfinity;

                return pad.lastUpdateTime >= System.Math.Max(keyboardTime, mouseTime);
            }
        }

        /// <summary>
        /// What the Interact button is actually called on the device in the player's hands.
        ///
        /// <para>The whole Interact path has been rebinding-safe since M16 — everything reads the
        /// action, never a physical button — but the PROMPT was a hardcoded "R1 / F", which made
        /// it the one part of the system that could lie. This asks the binding, so a rebind or a
        /// device swap can no longer put a false instruction on screen.</para>
        /// </summary>
        public string InteractDisplayString
        {
            get
            {
                if (interactAction == null)
                    return string.Empty;

                string group = UsingGamepad ? GamepadSchemeGroup : KeyboardSchemeGroup;
                string display = interactAction.GetBindingDisplayString(
                    InputBinding.DisplayStringOptions.DontUseShortDisplayNames, group);

                // A scheme with no binding for this action would otherwise print an empty box.
                return string.IsNullOrWhiteSpace(display)
                    ? interactAction.GetBindingDisplayString()
                    : display;
            }
        }

        void Awake()
        {
            if (actions == null)
            {
                Debug.LogError($"{nameof(PlayerInputReader)} on '{name}' has no InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            playerMap = actions.FindActionMap(PlayerMapName, throwIfNotFound: true);
            moveAction = playerMap.FindAction(MoveActionName, throwIfNotFound: true);
            dashAction = playerMap.FindAction(DashActionName, throwIfNotFound: true);
            attackAction = playerMap.FindAction(AttackActionName, throwIfNotFound: true);
            riposteAction = playerMap.FindAction(RiposteActionName, throwIfNotFound: true);
            vortexAction = playerMap.FindAction(VortexActionName, throwIfNotFound: true);
            interactAction = playerMap.FindAction(InteractActionName, throwIfNotFound: true);
            stopgapAction = playerMap.FindAction(StopgapActionName, throwIfNotFound: true);
        }

        void OnEnable() => playerMap?.Enable();

        void OnDisable() => playerMap?.Disable();
    }
}
