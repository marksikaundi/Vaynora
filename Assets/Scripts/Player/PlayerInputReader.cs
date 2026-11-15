using UnityEngine;
using UnityEngine.InputSystem;

namespace Vaynora.Player
{
    /// <summary>
    /// Reads the Player action map from an Input Actions asset.
    /// Keyboard, mouse, and gamepad already exist on that map.
    /// Touch controls should implement <see cref="IPlayerInput"/> instead of adding device code here.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Vaynora/Player Input Reader")]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        const string ActionMapName = "Player";
        const float JumpPressLifetime = 0.2f;

        [Tooltip("Assign Assets/InputSystem_Actions. Uses the Player map actions Move, Look, Jump, and Sprint.")]
        [SerializeField] InputActionAsset inputActions;

        InputActionMap _map;
        InputAction _move;
        InputAction _look;
        InputAction _jump;
        InputAction _sprint;
        float _jumpPressedAt = -1f;
        bool _actionsReady;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;

        public Vector2 Look => _look != null ? _look.ReadValue<Vector2>() : Vector2.zero;

        public bool LookIsPointerDelta
        {
            get
            {
                InputControl control = _look != null ? _look.activeControl : null;
                return control != null && control.device is Pointer;
            }
        }

        public bool SprintHeld => _sprint != null && _sprint.IsPressed();

        public bool ConsumeJumpPress()
        {
            if (_jumpPressedAt < 0f)
                return false;

            float age = Time.unscaledTime - _jumpPressedAt;
            _jumpPressedAt = -1f;
            return age <= JumpPressLifetime;
        }

        void OnEnable()
        {
            _actionsReady = BindActions();
            if (!_actionsReady)
                return;

            _jump.performed += OnJumpPerformed;
            _map.Enable();
        }

        void OnDisable()
        {
            if (!_actionsReady)
                return;

            _jump.performed -= OnJumpPerformed;
            _map.Disable();
            _actionsReady = false;
            _jumpPressedAt = -1f;
        }

        void OnJumpPerformed(InputAction.CallbackContext context)
        {
            if (context.performed)
                _jumpPressedAt = Time.unscaledTime;
        }

        bool BindActions()
        {
            _map = null;
            _move = null;
            _look = null;
            _jump = null;
            _sprint = null;

            if (inputActions == null)
            {
                Debug.LogError("Player Input Reader on " + name + " needs the InputSystem_Actions asset.", this);
                return false;
            }

            _map = inputActions.FindActionMap(ActionMapName, false);
            if (_map == null)
            {
                Debug.LogError("Player Input Reader on " + name + " could not find the Player action map.", this);
                return false;
            }

            _move = FindRequired("Move");
            _look = FindRequired("Look");
            _jump = FindRequired("Jump");
            _sprint = FindRequired("Sprint");
            return _move != null && _look != null && _jump != null && _sprint != null;
        }

        InputAction FindRequired(string actionName)
        {
            InputAction action = _map.FindAction(actionName, false);
            if (action == null)
                Debug.LogError("Player Input Reader on " + name + " could not find the " + actionName + " action.", this);

            return action;
        }
    }
}
