using Draho.Controls;
using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Moves the cat with a CharacterController. Input is relative to the camera.
    /// The cat turns towards the input direction and always walks where it faces,
    /// which gives natural curved paths.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CatMotor : MonoBehaviour
    {
        [SerializeField] CatStats stats;
        [Tooltip("Any component that implements ICatInput.")]
        [SerializeField] MonoBehaviour inputSource;
        [SerializeField] Transform cameraTransform;

        CharacterController controller;
        ICatInput input;
        float speed;
        float verticalVelocity;

        public CatStats Stats => stats;
        /// <summary>Current horizontal speed in m/s.</summary>
        public float Speed => speed;
        public bool IsGrounded => controller.isGrounded;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = inputSource as ICatInput;
            if (input == null)
                Debug.LogError($"{name}: the input source does not implement ICatInput.", this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 wish = WishDirection();
            float wishMagnitude = Mathf.Clamp01(wish.magnitude);

            float targetSpeed = 0f;
            if (wishMagnitude > 0f)
            {
                targetSpeed = input.Run ? stats.runSpeed : stats.walkSpeed * wishMagnitude;
                Quaternion look = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, stats.turnSpeed * dt);
            }

            float rate = targetSpeed > speed ? stats.acceleration : stats.deceleration;
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f; // keeps the controller pressed onto the floor
            else
                verticalVelocity = Mathf.Max(verticalVelocity + stats.gravity * dt, -stats.maxFallSpeed);

            Vector3 velocity = transform.forward * speed + Vector3.up * verticalVelocity;
            controller.Move(velocity * dt);
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
        }
    }
}
