using UnityEngine;
using UnityEngine.InputSystem;

namespace Vaynora.Player
{
    /// <summary>
    /// Orbits a follow target from look input and keeps that yaw independent of the body.
    /// PlayerMotor reads this transform so movement stays camera-relative.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Vaynora/Player Camera Rig")]
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        const float MaxPointerDelta = 80f;

        [Header("References")]
        [Tooltip("Player root the camera follows. Look input is read from an IPlayerInput on this object unless Input Source is set.")]
        [SerializeField] Transform followTarget;

        [Tooltip("Optional component that implements IPlayerInput. Leave empty to read input from the follow target.")]
        [SerializeField] MonoBehaviour inputSource;

        [Header("Orbit")]
        [Tooltip("Camera distance from the pivot, in meters.")]
        [SerializeField] float distance = 4.5f;

        [Tooltip("Pivot height above the follow target origin, in meters.")]
        [SerializeField] float pivotHeight = 1.5f;

        [Tooltip("Lowest look angle, in degrees. Negative looks down.")]
        [SerializeField] float minPitch = -35f;

        [Tooltip("Highest look angle, in degrees.")]
        [SerializeField] float maxPitch = 65f;

        [Header("Look")]
        [Tooltip("Degrees of yaw or pitch per pixel of mouse or touch delta.")]
        [SerializeField] float pointerSensitivity = 0.15f;

        [Tooltip("Degrees per second at full stick deflection.")]
        [SerializeField] float stickSensitivity = 150f;

        [Tooltip("Swaps the vertical look direction.")]
        [SerializeField] bool invertY;

        [Tooltip("Locks the mouse while playing on desktop so look deltas stay inside the game view. Escape unlocks it. Click locks it again.")]
        [SerializeField] bool lockCursor = true;

        IPlayerInput _input;
        float _yaw;
        float _pitch;
        bool _warnedMissingInput;
        bool _skipLook;

        void Reset()
        {
            if (followTarget == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                if (motor != null)
                    followTarget = motor.transform;
            }
        }

        void Awake()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            _input = ResolveInput();

            if (followTarget == null)
                Debug.LogError("Player Camera Rig on " + name + " needs a Follow Target.", this);
            else if (_input == null && !_warnedMissingInput)
            {
                _warnedMissingInput = true;
                Debug.LogWarning("Player Camera Rig on " + name + " found no IPlayerInput. The camera will follow, but it will not orbit.", this);
            }
        }

        void OnEnable()
        {
            _skipLook = true;
            if (lockCursor && Application.isPlaying && !Application.isMobilePlatform)
                SetCursorLocked(true);
        }

        void OnDisable()
        {
            if (lockCursor)
                SetCursorLocked(false);
        }

        void OnValidate()
        {
            distance = Mathf.Max(0.5f, distance);
            if (maxPitch < minPitch)
                maxPitch = minPitch;

            pointerSensitivity = Mathf.Max(0f, pointerSensitivity);
            stickSensitivity = Mathf.Max(0f, stickSensitivity);
        }

        void Update()
        {
            if (followTarget == null)
                return;

            if (_input == null)
                _input = ResolveInput();

            // Pointer deltas are only valid during this frame's input update.
            // Sample them here, then place the camera from LateUpdate after the body has moved.
            if (_skipLook)
            {
                _skipLook = false;
                return;
            }

            ApplyLook(Time.deltaTime, UpdateCursorLock());
        }

        void LateUpdate()
        {
            if (followTarget == null)
                return;

            ApplyOrbit();
        }

        public void SetInputSource(IPlayerInput input)
        {
            if (input != null)
                _input = input;
        }

        IPlayerInput ResolveInput()
        {
            if (inputSource != null)
            {
                if (inputSource is IPlayerInput input)
                    return input;

                Debug.LogError("Player Camera Rig on " + name + " has an Input Source that does not implement IPlayerInput.", this);
                return null;
            }

            if (followTarget == null)
                return null;

            return followTarget.GetComponent<IPlayerInput>();
        }

        bool UpdateCursorLock()
        {
            if (!lockCursor || Application.isMobilePlatform)
                return false;

            bool justLocked = false;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLocked(true);
                justLocked = true;
            }

            return justLocked;
        }

        void ApplyLook(float deltaTime, bool skipPointerLook)
        {
            if (_input == null)
                return;

            Vector2 look = _input.Look;
            if (look.sqrMagnitude < 0.0001f)
                return;

            bool pointerDelta = _input.LookIsPointerDelta;
            if (pointerDelta)
            {
                bool pointerAllowed = !lockCursor || Application.isMobilePlatform || Cursor.lockState == CursorLockMode.Locked;
                if (skipPointerLook || !pointerAllowed)
                    return;

                look.x = Mathf.Clamp(look.x, -MaxPointerDelta, MaxPointerDelta);
                look.y = Mathf.Clamp(look.y, -MaxPointerDelta, MaxPointerDelta);
                _yaw += look.x * pointerSensitivity;
                _pitch += (invertY ? look.y : -look.y) * pointerSensitivity;
            }
            else
            {
                _yaw += look.x * stickSensitivity * deltaTime;
                _pitch += (invertY ? look.y : -look.y) * stickSensitivity * deltaTime;
            }

            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        void ApplyOrbit()
        {
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = followTarget.position + Vector3.up * pivotHeight;
            transform.SetPositionAndRotation(focus - (orbit * Vector3.forward * distance), orbit);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
                pitch -= 360f;

            return pitch;
        }

        void OnDrawGizmosSelected()
        {
            if (followTarget == null)
                return;

            Vector3 focus = followTarget.position + Vector3.up * pivotHeight;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(focus, 0.08f);
            Gizmos.DrawLine(focus, transform.position);
        }
    }
}
