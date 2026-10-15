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

        [Header("Climbing")]
        [Tooltip("While the cat climbs, the camera moves back and looks more level, so the wall does not fill the view.")]
        [SerializeField] float climbDistance = 2.1f;
        [SerializeField] float climbPitch = 6f;
        [SerializeField] float climbFocusHeight = 0.35f;

        float yaw;
        CatMotor motor;
        float climbBlend;
        Vector3 focus;
        float currentDistance;

        void Start()
        {
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            if (target != null) focus = target.position + Vector3.up * focusHeight;
            currentDistance = distance;
            motor = target != null ? target.GetComponent<CatMotor>() : null;
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

            climbBlend = Mathf.MoveTowards(climbBlend, motor != null && motor.IsClimbing ? 1f : 0f, 1.5f * dt);
            float blend = Mathf.SmoothStep(0f, 1f, climbBlend);
            float wantDistance = Mathf.Lerp(distance, climbDistance, blend);
            // On the wall, swing slowly round to behind the cat (unless the player turns the view).
            if (blend > 0f && look.sqrMagnitude < 0.01f)
                yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, 50f * blend * dt);

            Vector3 targetFocus = target.position + Vector3.up * Mathf.Lerp(focusHeight, climbFocusHeight, blend);
            focus = Vector3.Lerp(focus, targetFocus, 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rotation = Quaternion.Euler(Mathf.Lerp(pitch, climbPitch, blend), yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float allowed = wantDistance;
            if (Physics.SphereCast(focus, collisionRadius, back, out RaycastHit hit, wantDistance,
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
