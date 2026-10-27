using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GravBoots.Player
{
    /// <summary>
    /// Owns the GravBoots input actions asset and exposes the player actions by name.
    /// Debug actions are looked up through <see cref="Find"/>.
    /// </summary>
    /// <remarks>
    /// Button presses are latched from the action's performed event and consumed when read,
    /// rather than polled with WasPressedThisFrame. Polling misses a press whose down and up
    /// land in one frame, or that is processed outside the player loop (simulated input from
    /// the CLI, an unfocused Game view). Each press should have exactly one reader.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder.Input)]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;

        InputAction move, look, lookStick, jump, toggleBoots, interact, pause, restart, restartChamber;
        readonly Dictionary<InputAction, bool> latched = new Dictionary<InputAction, bool>();
        readonly List<(InputAction action, Action<InputAction.CallbackContext> handler)> subscriptions =
            new List<(InputAction, Action<InputAction.CallbackContext>)>();

        public Vector2 Move => move?.ReadValue<Vector2>() ?? Vector2.zero;
        /// <summary>Pointer delta in pixels this frame.</summary>
        public Vector2 LookDelta => look?.ReadValue<Vector2>() ?? Vector2.zero;
        /// <summary>Gamepad stick deflection, -1..1; scale by a rate and delta time.</summary>
        public Vector2 LookStick => lookStick?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool JumpPressed => ConsumePress(jump);
        public bool ToggleBootsPressed => ConsumePress(toggleBoots);
        public bool InteractPressed => ConsumePress(interact);
        public bool PausePressed => ConsumePress(pause);
        /// <summary>Back to the last checkpoint.</summary>
        public bool RestartPressed => ConsumePress(restart);
        /// <summary>Start the chamber over from the beginning.</summary>
        public bool RestartChamberPressed => ConsumePress(restartChamber);

        public InputActionAsset Actions
        {
            get => actions;
            set { actions = value; Bind(); }
        }

        void OnEnable() => Bind();

        void OnDisable()
        {
            foreach (var (action, handler) in subscriptions)
                action.performed -= handler;
            subscriptions.Clear();
            latched.Clear();
            actions?.Disable();
        }

        /// <summary>
        /// True once per press of <paramref name="action"/> since the last call. The first call
        /// for an action starts latching it.
        /// </summary>
        public bool ConsumePress(InputAction action)
        {
            if (action == null)
                return false;
            if (!latched.TryGetValue(action, out bool pressed))
            {
                Action<InputAction.CallbackContext> handler = _ => latched[action] = true;
                action.performed += handler;
                subscriptions.Add((action, handler));
                latched[action] = false;
                return action.WasPressedThisFrame();
            }
            latched[action] = false;
            return pressed;
        }

        /// <summary>Finds an action by "Map/Action" path, or null if it doesn't exist.</summary>
        public InputAction Find(string path) => actions != null ? actions.FindAction(path) : null;

        void Bind()
        {
            if (actions == null)
                return;
            foreach (var (action, handler) in subscriptions)
                action.performed -= handler;
            subscriptions.Clear();
            latched.Clear();
            move = Find("Player/Move");
            look = Find("Player/Look");
            lookStick = Find("Player/LookStick");
            jump = Find("Player/Jump");
            toggleBoots = Find("Player/ToggleBoots");
            interact = Find("Player/Interact");
            pause = Find("Player/Pause");
            restart = Find("Player/Restart");
            restartChamber = Find("Player/RestartChamber");
            actions.Enable();
        }
    }
}
