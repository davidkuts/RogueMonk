using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.Player
{
    /// <summary>
    /// Thin adapter over the Input System asset. Owns action lookup and enabling so no
    /// gameplay script touches InputAction plumbing. Device/control-scheme differences
    /// stop here — callers only see conditioned values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        const string PlayerMapName = "Player";
        const string MoveActionName = "Move";

        [SerializeField] InputActionAsset actions;

        InputActionMap playerMap;
        InputAction moveAction;

        /// <summary>Raw (unconditioned) move vector. Deadzone/curve are applied in the simulation.</summary>
        public Vector2 MoveAxis => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

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
        }

        void OnEnable() => playerMap?.Enable();

        void OnDisable() => playerMap?.Disable();
    }
}
