using Draho.Cat;
using Draho.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draho.CameraRig
{
    /// <summary>
    /// Third-person camera that orbits the cat. Swipe on the look area (phone) or drag with the
    /// right mouse button (editor). The camera moves closer when a wall is between it and the cat:
    /// at once when something blocks the view, slowly when the view is free again (no popping when
    /// the cat jumps over low furniture).
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] TouchLookArea lookArea;

        [Header("Framing")]
        [SerializeField] float distance = 1.3f;
        [SerializeField] float focusHeight = 0.2f;
        [SerializeField] float followSharpness = 12f;

        [Header("Orbit")]
        [Tooltip("Degrees per screen height of swipe.")]
        [SerializeField] float touchSensitivity = 220f;
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.25f;
        [SerializeField] float pitch = 18f;
        [SerializeField] float minPitch = -5f;
        [SerializeField] float maxPitch = 70f;

        [Header("Collision")]
        [SerializeField] float collisionRadius = 0.06f;
        [SerializeField] LayerMask collisionMask = ~(1 << 2); // everything except Ignore Raycast
        [Tooltip("Metres per second at which the camera moves back out after a collision.")]
        [SerializeField] float returnSpeed = 1.2f;

        float yaw;
        Vector3 focus;
        float currentDistance;

        void Start()
        {
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            if (target != null) focus = target.position + Vector3.up * focusHeight;
            currentDistance = distance;
            var motor = target != null ? target.GetComponent<CatMotor>() : null;
            if (motor != null) motor.Teleported += SnapBehindTarget;
        }

        /// <summary>Puts the camera straight behind the cat (after a teleport or a respawn).</summary>
        public void SnapBehindTarget()
        {
            yaw = target.eulerAngles.y;
            focus = target.position + Vector3.up * focusHeight;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            Vector2 look = Vector2.zero;
            if (lookArea != null)
                look += lookArea.ConsumeDelta() / Mathf.Max(1, Screen.height) * touchSensitivity;
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
                look += mouse.delta.ReadValue() * mouseSensitivity;

            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            Vector3 targetFocus = target.position + Vector3.up * focusHeight;
            focus = Vector3.Lerp(focus, targetFocus, 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float allowed = distance;
            if (Physics.SphereCast(focus, collisionRadius, back, out RaycastHit hit, distance,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                allowed = Mathf.Max(0.1f, hit.distance);
            }

            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.MoveTowards(currentDistance, allowed, returnSpeed * dt);
            transform.SetPositionAndRotation(focus + back * currentDistance, rotation);
        }
    }
}
