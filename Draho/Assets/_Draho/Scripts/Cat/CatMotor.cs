using Draho.Controls;
using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Moves the cat with a CharacterController. Input is relative to the camera.
    /// The cat turns towards the input direction and always walks where it faces,
    /// which gives natural curved paths. It can jump and crouch (duck / sneak).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CatMotor : MonoBehaviour
    {
        const float AirborneDelay = 0.08f; // short drops (steps) do not count as a fall

        [SerializeField] CatStats stats;
        [Tooltip("Any component that implements ICatInput.")]
        [SerializeField] MonoBehaviour inputSource;
        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        ICatInput input;
        float speed;
        float verticalVelocity;
        float standHeight;
        float timeSinceGrounded;
        float jumpBufferTimer;

        public CatStats Stats => stats;
        /// <summary>Current horizontal speed in m/s.</summary>
        public float Speed => speed;
        public bool IsGrounded => controller.isGrounded;
        /// <summary>In the air after a jump, or falling for longer than a step.</summary>
        public bool IsAirborne => verticalVelocity > 0f || timeSinceGrounded > AirborneDelay;
        public float VerticalVelocity => verticalVelocity;
        public bool IsCrouching { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            standHeight = controller.height;
            input = inputSource as ICatInput;
            if (input == null)
                Debug.LogError($"{name}: the input source does not implement ICatInput.", this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool grounded = controller.isGrounded;
            timeSinceGrounded = grounded ? 0f : timeSinceGrounded + dt;

            UpdateCrouch();

            if (input != null && input.ConsumeJump()) jumpBufferTimer = stats.jumpBuffer;
            else jumpBufferTimer -= dt;

            Vector3 wish = WishDirection();
            float wishMagnitude = Mathf.Clamp01(wish.magnitude);

            float targetSpeed = 0f;
            if (wishMagnitude > 0f)
            {
                if (IsCrouching) targetSpeed = stats.crouchSpeed * wishMagnitude;
                else targetSpeed = input.Run ? stats.runSpeed : stats.walkSpeed * wishMagnitude;
                Quaternion look = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, stats.turnSpeed * dt);
            }

            float rate = targetSpeed > speed ? stats.acceleration : stats.deceleration;
            if (IsAirborne) rate *= stats.airControl; // keep the take-off speed, steer a little
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

            bool canJump = timeSinceGrounded <= stats.coyoteTime && verticalVelocity <= 0f;
            if (jumpBufferTimer > 0f && canJump && TryStandUp())
            {
                verticalVelocity = Mathf.Sqrt(2f * -stats.gravity * stats.jumpHeight);
                jumpBufferTimer = 0f;
                timeSinceGrounded = stats.coyoteTime + 1f; // no second jump in the air
            }
            else if (grounded && verticalVelocity < 0f)
                verticalVelocity = -1f; // keeps the controller pressed onto the floor
            else
                verticalVelocity = Mathf.Max(verticalVelocity + stats.gravity * dt, -stats.maxFallSpeed);

            Vector3 velocity = transform.forward * speed + Vector3.up * verticalVelocity;
            CollisionFlags hit = controller.Move(velocity * dt);
            if ((hit & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        }

        void UpdateCrouch()
        {
            bool want = input != null && input.Crouch && !IsAirborne;
            if (want && !IsCrouching) SetHeight(stats.crouchHeight, true);
            else if (!want && IsCrouching) TryStandUp();
        }

        /// <summary>Stands up if there is room above the cat. Returns false while it must stay low.</summary>
        bool TryStandUp()
        {
            if (!IsCrouching) return true;
            float r = controller.radius;
            Vector3 bottom = transform.position + Vector3.up * (r + 0.01f);
            Vector3 top = transform.position + Vector3.up * (standHeight - r);
            int mask = ~(1 << gameObject.layer);
            if (Physics.CheckCapsule(bottom, top, r * 0.95f, mask, QueryTriggerInteraction.Ignore))
                return false;
            SetHeight(standHeight, false);
            input?.CancelCrouch();
            return true;
        }

        void SetHeight(float height, bool crouching)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height / 2f, 0f);
            IsCrouching = crouching;
        }

        Vector3 WishDirection()
        {
            if (input == null) return Vector3.zero;
            Vector2 move = input.Move;
            if (move.sqrMagnitude < 0.0001f) return Vector3.zero;

            Transform view = cameraTransform != null ? cameraTransform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return right * move.x + forward * move.y;
        }

        /// <summary>Moves the cat instantly, for example after a fall.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            speed = 0f;
            verticalVelocity = 0f;
            timeSinceGrounded = 0f;
        }
    }
}
