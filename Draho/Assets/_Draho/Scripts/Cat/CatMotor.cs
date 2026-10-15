using Draho.Controls;
using Draho.World;
using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Moves the cat with a CharacterController. Input is relative to the camera.
    /// The cat turns towards the input direction and always walks where it faces,
    /// which gives natural curved paths. It can jump, crouch (duck / sneak), rear up and climb.
    /// A jump keeps the speed the cat had just before (a cat never jumps from standing still):
    /// without speed, the cat stands up on its hind legs instead. Standing up in front of a
    /// <see cref="Climbable"/> starts a climb.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CatMotor : MonoBehaviour
    {
        enum Mode { Normal, Rearing, Climbing, Mantling }

        const float AirborneDelay = 0.08f; // short drops (steps) do not count as a fall
        const float WallProbeHeight = 0.12f;

        [SerializeField] CatStats stats;
        [Tooltip("Any component that implements ICatInput.")]
        [SerializeField] MonoBehaviour inputSource;
        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        ICatInput input;
        Mode mode;
        float modeTimer;
        float speed;
        float verticalVelocity;
        float standHeight;
        float timeSinceGrounded;
        float jumpBufferTimer;
        float lastMovingSpeed;
        float timeSinceMoveInput = 99f;
        Climbable climbable;
        Vector3 wallNormal;
        Vector3 mantleFrom, mantleTo;

        public CatStats Stats => stats;
        /// <summary>Current horizontal speed in m/s.</summary>
        public float Speed => speed;
        public bool IsGrounded => controller.isGrounded;
        /// <summary>In the air after a jump, or falling for longer than a step.</summary>
        public bool IsAirborne => mode == Mode.Normal && (verticalVelocity > 0f || timeSinceGrounded > AirborneDelay);
        public float VerticalVelocity => verticalVelocity;
        public bool IsCrouching { get; private set; }
        public bool IsRearing => mode == Mode.Rearing;
        public bool IsClimbing => mode == Mode.Climbing || mode == Mode.Mantling;
        /// <summary>Up (+) or down (-) speed on the wall, in m/s.</summary>
        public float ClimbVelocity { get; private set; }

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
            switch (mode)
            {
                case Mode.Climbing: UpdateClimb(dt); return;
                case Mode.Mantling: UpdateMantle(dt); return;
            }

            bool grounded = controller.isGrounded;
            timeSinceGrounded = grounded ? 0f : timeSinceGrounded + dt;

            if (mode == Mode.Normal) UpdateCrouch();

            if (input != null && input.ConsumeJump()) jumpBufferTimer = stats.jumpBuffer;
            else jumpBufferTimer -= dt;

            Vector3 wish = WishDirection();
            float wishMagnitude = Mathf.Clamp01(wish.magnitude);

            float targetSpeed = 0f;
            if (mode == Mode.Rearing)
            {
                modeTimer += dt;
                jumpBufferTimer = 0f;
                if (modeTimer > 0.25f && TryFindWall(out Climbable wall, out Vector3 normal))
                {
                    StartClimb(wall, normal);
                    return;
                }
                if (modeTimer > stats.rearTime || (modeTimer > 0.3f && wishMagnitude > 0.5f)) mode = Mode.Normal;
            }
            else if (wishMagnitude > 0f)
            {
                if (IsCrouching) targetSpeed = stats.crouchSpeed * wishMagnitude;
                else targetSpeed = input.Run ? stats.runSpeed : stats.walkSpeed * wishMagnitude;
                Quaternion look = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, stats.turnSpeed * dt);
            }

            // Remember the speed while the stick is pushed: the thumb leaves the stick to tap "jump".
            if (wishMagnitude > 0.1f)
            {
                lastMovingSpeed = speed;
                timeSinceMoveInput = 0f;
            }
            else timeSinceMoveInput += dt;

            float rate = targetSpeed > speed ? stats.acceleration : stats.deceleration;
            if (IsAirborne) rate *= stats.airControl; // keep the take-off speed, steer a little
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

            bool canJump = mode == Mode.Normal && timeSinceGrounded <= stats.coyoteTime && verticalVelocity <= 0f;
            if (jumpBufferTimer > 0f && canJump && TryStandUp())
            {
                jumpBufferTimer = 0f;
                float carried = timeSinceMoveInput <= stats.jumpMomentumTime ? Mathf.Max(speed, lastMovingSpeed) : speed;
                if (carried < stats.rearSpeedThreshold)
                {
                    // No speed: stand up on the hind legs (and climb, when something climbable is in front).
                    mode = Mode.Rearing;
                    modeTimer = 0f;
                    speed = 0f;
                }
                else
                {
                    speed = carried;
                    verticalVelocity = Mathf.Sqrt(2f * -stats.gravity * stats.jumpHeight);
                    timeSinceGrounded = stats.coyoteTime + 1f; // no second jump in the air
                }
            }
            else if (grounded && verticalVelocity < 0f)
                verticalVelocity = -1f; // keeps the controller pressed onto the floor
            else
                verticalVelocity = Mathf.Max(verticalVelocity + stats.gravity * dt, -stats.maxFallSpeed);

            Vector3 velocity = transform.forward * speed + Vector3.up * verticalVelocity;
            CollisionFlags hit = controller.Move(velocity * dt);
            if ((hit & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
        }

        // ------------------------------------------------------------------ climbing
        bool TryFindWall(out Climbable wall, out Vector3 normal)
        {
            wall = null;
            normal = Vector3.zero;
            Vector3 origin = transform.position + Vector3.up * WallProbeHeight;
            int mask = ~(1 << gameObject.layer);
            if (!Physics.Raycast(origin, transform.forward, out RaycastHit hit, controller.radius + stats.climbReach,
                    mask, QueryTriggerInteraction.Ignore))
                return false;
            if (Mathf.Abs(hit.normal.y) > 0.4f) return false; // not a wall
            wall = hit.collider.GetComponentInParent<Climbable>();
            normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            return wall != null;
        }

        void StartClimb(Climbable wall, Vector3 normal)
        {
            mode = Mode.Climbing;
            climbable = wall;
            wallNormal = normal;
            transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            speed = 0f;
            verticalVelocity = 0f;
            ClimbVelocity = 0f;
        }

        void UpdateClimb(float dt)
        {
            if (input != null && input.ConsumeJump())
            {
                // Push off: turn away from the wall and leap.
                transform.rotation = Quaternion.LookRotation(wallNormal, Vector3.up);
                LeaveClimb();
                speed = stats.climbLeapSpeed;
                verticalVelocity = Mathf.Sqrt(2f * -stats.gravity * 0.3f);
                timeSinceGrounded = stats.coyoteTime + 1f;
                return;
            }

            Vector2 move = input != null ? input.Move : Vector2.zero;
            float feet = transform.position.y;
            ClimbVelocity = move.y * stats.climbSpeed;

            if (ClimbVelocity > 0f && feet >= climbable.TopY - stats.mantleReach)
            {
                if (climbable.CanStandOnTop && TryStartMantle()) return;
                ClimbVelocity = 0f; // the top of a tree trunk: hold on
            }

            Vector3 right = Vector3.Cross(Vector3.up, -wallNormal);
            Vector3 motion = Vector3.up * ClimbVelocity + right * (move.x * stats.climbSpeed * 0.5f) - wallNormal * 0.1f;
            controller.Move(motion * dt);

            if (ClimbVelocity < 0f && controller.isGrounded)
            {
                LeaveClimb(); // back on the floor
                timeSinceGrounded = 0f;
                return;
            }
            if (TryFindWall(out Climbable wall, out Vector3 normal))
            {
                // Follow round surfaces (tree trunks).
                climbable = wall;
                wallNormal = normal;
                transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            }
            else LeaveClimb(); // climbed off the side: fall
        }

        bool TryStartMantle()
        {
            Vector3 forward = -wallNormal;
            Vector3 to = new Vector3(transform.position.x, climbable.TopY + 0.02f, transform.position.z)
                         + forward * (controller.radius * 2f + 0.12f);
            float r = controller.radius;
            int mask = ~(1 << gameObject.layer);
            if (Physics.CheckCapsule(to + Vector3.up * (r + 0.03f), to + Vector3.up * (standHeight - r), r * 0.9f, mask,
                    QueryTriggerInteraction.Ignore))
                return false; // no room on top
            mode = Mode.Mantling;
            modeTimer = 0f;
            mantleFrom = transform.position;
            mantleTo = to;
            controller.enabled = false;
            return true;
        }

        void UpdateMantle(float dt)
        {
            modeTimer += dt / stats.mantleTime;
            float t = Mathf.Clamp01(modeTimer);
            // Up first, then forward over the edge.
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.7f));
            float forward = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.65f));
            transform.position = new Vector3(
                Mathf.Lerp(mantleFrom.x, mantleTo.x, forward),
                Mathf.Lerp(mantleFrom.y, mantleTo.y, up),
                Mathf.Lerp(mantleFrom.z, mantleTo.z, forward));
            ClimbVelocity = stats.climbSpeed;
            if (t < 1f) return;
            controller.enabled = true;
            LeaveClimb();
            verticalVelocity = -1f;
            timeSinceGrounded = 0f;
        }

        void LeaveClimb()
        {
            mode = Mode.Normal;
            climbable = null;
            ClimbVelocity = 0f;
            speed = 0f;
            verticalVelocity = 0f;
            timeSinceMoveInput = 99f;
        }

        // ------------------------------------------------------------------ crouch
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
            LeaveClimb();
            timeSinceGrounded = 0f;
        }
    }
}
