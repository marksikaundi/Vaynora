using System;
using UnityEngine;

namespace Vaynora.Player
{
    /// <summary>
    /// Moves a CharacterController from an <see cref="IPlayerInput"/>.
    /// Walking, running, jumping, gravity, and camera-relative motion live here.
    /// Input devices do not.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Vaynora/Player Motor")]
    public sealed class PlayerMotor : MonoBehaviour
    {
        const float MoveDeadzone = 0.05f;
        const int GroundHitCapacity = 16;

        [Header("References")]
        [Tooltip("Yaw used to turn move input into a world direction. Assign the camera that follows the player.")]
        [SerializeField] Transform cameraTransform;

        [Tooltip("Optional component that implements IPlayerInput. Leave empty to use a reader on this GameObject. Assign a touch control here later.")]
        [SerializeField] MonoBehaviour inputSource;

        [Header("Speed")]
        [Tooltip("Meters per second at full stick tilt while walking.")]
        [SerializeField] float walkSpeed = 2.4f;

        [Tooltip("Meters per second at full stick tilt while the sprint control is held.")]
        [SerializeField] float runSpeed = 5.5f;

        [Tooltip("How quickly horizontal speed builds, in meters per second squared.")]
        [SerializeField] float acceleration = 24f;

        [Tooltip("How quickly horizontal speed drops or reverses, in meters per second squared.")]
        [SerializeField] float deceleration = 30f;

        [Tooltip("How fast the body turns toward the move direction, in degrees per second.")]
        [SerializeField] float turnSpeed = 720f;

        [Header("Jump")]
        [Tooltip("Peak height of a standing jump, in meters.")]
        [SerializeField] float jumpHeight = 1.2f;

        [Tooltip("Downward acceleration while airborne, in meters per second squared.")]
        [SerializeField] float gravity = 20f;

        [Tooltip("Fastest falling speed, in meters per second.")]
        [SerializeField] float maxFallSpeed = 30f;

        [Tooltip("Seconds after a jump before the character can be grounded again. Stops the ground probe from cancelling the jump.")]
        [SerializeField] float jumpGroundingDelay = 0.08f;

        [Tooltip("How much move input can steer the character in the air. 0 keeps the velocity from the moment they leave the ground.")]
        [SerializeField, Range(0f, 1f)] float airControl = 0f;

        [Header("Grounding")]
        [Tooltip("Layers that count as ground. The player and its children are ignored.")]
        [SerializeField] LayerMask groundLayers = 1;

        [Tooltip("Radius of the ground probe at the feet.")]
        [SerializeField] float groundProbeRadius = 0.25f;

        [Tooltip("How far below the capsule bottom the probe sits.")]
        [SerializeField] float groundProbeDistance = 0.08f;

        [Tooltip("Downward speed applied while grounded so the capsule stays on the floor.")]
        [SerializeField] float groundStickSpeed = 2f;

        CharacterController _controller;
        IPlayerInput _input;
        readonly Collider[] _groundHits = new Collider[GroundHitCapacity];

        Vector3 _planarVelocity;
        float _verticalVelocity;
        float _ungroundedUntil;
        bool _isGrounded;
        bool _hasGroundSample;
        bool _isSprinting;

        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _isSprinting;
        public float PlanarSpeed => _planarVelocity.magnitude;
        public Vector3 PlanarVelocity => _planarVelocity;
        public float VerticalVelocity => _verticalVelocity;

        public event Action Jumped;
        public event Action Landed;

        void Reset()
        {
            CharacterController body = GetComponent<CharacterController>();
            if (body != null)
            {
                body.height = 1.8f;
                body.radius = 0.3f;
                body.center = new Vector3(0f, 0.9f, 0f);
                body.stepOffset = 0.3f;
                body.slopeLimit = 45f;
                body.skinWidth = 0.08f;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                gameObject.layer = playerLayer;

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = ResolveInput();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (_input == null)
            {
                Debug.LogError("Player Motor on " + name + " needs a Player Input Reader, or another IPlayerInput, on this GameObject.", this);
                enabled = false;
            }
        }

        void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            gravity = Mathf.Max(0f, gravity);
            maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
            jumpGroundingDelay = Mathf.Max(0f, jumpGroundingDelay);
            groundProbeRadius = Mathf.Max(0.01f, groundProbeRadius);
            groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
            groundStickSpeed = Mathf.Max(0f, groundStickSpeed);
        }

        void Update()
        {
            if (_input == null || _controller == null || !_controller.enabled)
                return;

            float deltaTime = Time.deltaTime;
            Vector2 move = ReadMove();
            Vector3 moveDirection = CameraRelativeDirection(move);
            float moveAmount = moveDirection.sqrMagnitude > 0.0001f ? Mathf.Clamp01(move.magnitude) : 0f;
            if (moveAmount > 0f)
                moveDirection.Normalize();

            bool wasGrounded = _isGrounded;
            bool grounded = Time.time >= _ungroundedUntil && ProbeGrounded();
            bool jumpPressed = _input.ConsumeJumpPress();
            _isSprinting = grounded && _input.SprintHeld && moveAmount > MoveDeadzone;

            Vector3 desiredPlanar = moveDirection * ((_isSprinting ? runSpeed : walkSpeed) * moveAmount);
            if (grounded)
                Accelerate(desiredPlanar, deltaTime);
            else
                ApplyAirControl(desiredPlanar, deltaTime);

            if (grounded && jumpPressed)
            {
                _verticalVelocity = gravity > 0f ? Mathf.Sqrt(2f * gravity * jumpHeight) : 0f;
                _ungroundedUntil = Time.time + jumpGroundingDelay;
                grounded = false;
                _isSprinting = false;
                Jumped?.Invoke();
            }
            else if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -groundStickSpeed;
            }
            else if (!grounded)
            {
                _verticalVelocity = Mathf.Max(_verticalVelocity - gravity * deltaTime, -maxFallSpeed);
            }

            _isGrounded = grounded;
            if (_hasGroundSample && _isGrounded && !wasGrounded)
                Landed?.Invoke();

            _hasGroundSample = true;

            Vector3 requestedPlanar = _planarVelocity;
            Vector3 positionBefore = transform.position;
            Vector3 motion = (requestedPlanar + Vector3.up * _verticalVelocity) * deltaTime;
            CollisionFlags flags = _controller.Move(motion);
            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f)
                _verticalVelocity = 0f;

            // A blocked capsule still reports the speed we asked for. Keep the speed that
            // actually happened so a jump into a wall does not store a launch for later.
            if ((flags & CollisionFlags.Sides) != 0 && deltaTime > 0f)
            {
                Vector3 movedPlanar = transform.position - positionBefore;
                movedPlanar.y = 0f;
                float requestedDistance = requestedPlanar.magnitude * deltaTime;
                if (requestedDistance > 0.0001f && movedPlanar.magnitude < requestedDistance * 0.99f)
                    _planarVelocity = movedPlanar / deltaTime;
            }

            if (_planarVelocity.sqrMagnitude < 0.0001f)
                _planarVelocity = Vector3.zero;

            bool canTurn = grounded || airControl > 0f;
            if (canTurn && moveDirection.sqrMagnitude > 0.0001f)
                TurnToward(moveDirection, deltaTime);
        }

        public void SetInputSource(IPlayerInput input)
        {
            if (input != null)
                _input = input;
        }

        IPlayerInput ResolveInput()
        {
            if (inputSource == null)
                return GetComponent<IPlayerInput>();

            if (inputSource is IPlayerInput input)
                return input;

            Debug.LogError("Player Motor on " + name + " has an Input Source that does not implement IPlayerInput.", this);
            return null;
        }

        Vector2 ReadMove()
        {
            Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
            if (move.sqrMagnitude < MoveDeadzone * MoveDeadzone)
                return Vector2.zero;

            return move;
        }

        Vector3 CameraRelativeDirection(Vector2 move)
        {
            if (move.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            Transform basis = cameraTransform != null ? cameraTransform : transform;
            Vector3 forward = basis.forward;
            forward.y = 0f;
            Vector3 right = basis.right;
            right.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            else
                forward.Normalize();

            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.right;
            else
                right.Normalize();

            return forward * move.y + right * move.x;
        }

        void Accelerate(Vector3 desiredPlanar, float deltaTime)
        {
            bool braking = desiredPlanar.sqrMagnitude < _planarVelocity.sqrMagnitude
                || Vector3.Dot(_planarVelocity, desiredPlanar) < 0f;
            float rate = braking ? deceleration : acceleration;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredPlanar, rate * deltaTime);
        }

        void ApplyAirControl(Vector3 desiredPlanar, float deltaTime)
        {
            if (airControl <= 0f)
                return;

            bool braking = desiredPlanar.sqrMagnitude < _planarVelocity.sqrMagnitude
                || Vector3.Dot(_planarVelocity, desiredPlanar) < 0f;
            float rate = (braking ? deceleration : acceleration) * airControl;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, desiredPlanar, rate * deltaTime);
        }

        void TurnToward(Vector3 direction, float deltaTime)
        {
            if (turnSpeed <= 0f)
                return;

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * deltaTime);
        }

        bool ProbeGrounded()
        {
            Vector3 center = GetProbeCenter(_controller);
            int count = Physics.OverlapSphereNonAlloc(
                center,
                groundProbeRadius,
                _groundHits,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _groundHits[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                return true;
            }

            return false;
        }

        Vector3 GetProbeCenter(CharacterController body)
        {
            float bottom = body.center.y - (body.height * 0.5f);
            return transform.position + Vector3.up * bottom + Vector3.down * groundProbeDistance;
        }

        void OnDrawGizmosSelected()
        {
            CharacterController body = _controller != null ? _controller : GetComponent<CharacterController>();
            if (body == null)
                return;

            Gizmos.color = Application.isPlaying && _isGrounded
                ? new Color(0.2f, 0.85f, 0.35f, 0.9f)
                : new Color(1f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(GetProbeCenter(body), groundProbeRadius);
        }
    }
}
