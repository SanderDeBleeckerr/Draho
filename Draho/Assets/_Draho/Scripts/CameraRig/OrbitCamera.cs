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
    /// the cat jumps over low furniture). When the view stays blocked in a tight place (a light well, under a roof
    /// edge), the camera also looks for a free viewpoint at the same distance nearby (left, right, a little higher)
    /// and turns slowly towards it, like Cinemachine's "preserve camera distance" with a minimum occlusion time
    /// (research round 50). The player's own swipe always wins.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] TouchLookArea lookArea;

        [Header("Framing")]
        [SerializeField] float distance = 0.6f;
        [SerializeField] float focusHeight = 0.22f;
        [SerializeField] float followSharpness = 12f;
        [Tooltip("How fast the camera follows height changes (lower: calmer during jumps).")]
        [SerializeField] float verticalSharpness = 5f;
        [Tooltip("Seconds of movement the camera looks ahead of a running cat.")]
        [SerializeField] float lookaheadTime = 0.18f;

        [Header("Orbit")]
        [Tooltip("Degrees per screen height of swipe.")]
        [SerializeField] float touchSensitivity = 220f;
        [Tooltip("Degrees per pixel of mouse movement.")]
        [SerializeField] float mouseSensitivity = 0.25f;
        [SerializeField] float pitch = 10f;
        [SerializeField] float minPitch = -5f;
        [SerializeField] float maxPitch = 70f;

        [Header("Collision")]
        [SerializeField] float collisionRadius = 0.06f;
        [SerializeField] LayerMask collisionMask = ~(1 << 2); // everything except Ignore Raycast
        [Tooltip("Metres per second at which the camera moves back out after a collision.")]
        [SerializeField] float returnSpeed = 1.2f;
        [Tooltip("Seconds the view must stay blocked before the camera looks for another viewpoint (thin posts pass by).")]
        [SerializeField] float minOcclusionTime = 0.3f;
        [Tooltip("Degrees per second at which the camera turns to a free viewpoint.")]
        [SerializeField] float avoidSpeed = 70f;

        [Header("Climbing")]
        [Tooltip("While the cat climbs, the camera moves back and looks more level, so the wall does not fill the view.")]
        [SerializeField] float climbDistance = 0.95f;
        [SerializeField] float climbPitch = 6f;
        [SerializeField] float climbFocusHeight = 0.35f;

        float yaw;
        CatMotor motor;
        float climbBlend;
        Vector3 lookahead;
        Vector3 focus;
        float currentDistance;
        float occludedTime;
        float sweepTime, sweepBlend;
        static readonly Vector2[] Probes =
        {
            new Vector2(25f, 0f), new Vector2(-25f, 0f), new Vector2(0f, 20f), new Vector2(25f, 15f),
            new Vector2(-25f, 15f), new Vector2(50f, 0f), new Vector2(-50f, 0f),
        };

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

        /// <summary>A slow turn round the cat, pulled back, to show a view (a viewpoint). Any swipe by the player ends it.</summary>
        public void Sweep(float seconds) => sweepTime = seconds;

        /// <summary>Test hook: SendMessage('Main Camera', 'TestView', "pitch,distance"), e.g. "-5,1.2" to look up.</summary>
        public void TestView(string args)
        {
            var parts = args.Split(',');
            pitch = Mathf.Clamp(float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), minPitch, maxPitch);
            if (parts.Length > 1) distance = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
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
            // A viewpoint sweep: ease out to a wide view, turn slowly, ease back in at the end.
            if (sweepTime > 0f)
            {
                sweepTime = look.sqrMagnitude > 0.01f ? 0f : sweepTime - dt;
                yaw += 30f * sweepBlend * dt;
                pitch = Mathf.MoveTowards(pitch, 16f, 10f * dt);
            }
            sweepBlend = Mathf.MoveTowards(sweepBlend, sweepTime > 1.5f ? 1f : 0f, dt / 1.5f);
            wantDistance = Mathf.Lerp(wantDistance, 2.4f, Mathf.SmoothStep(0f, 1f, sweepBlend));
            // On the wall, swing slowly round to behind the cat (unless the player turns the view).
            if (blend > 0f && look.sqrMagnitude < 0.01f)
                yaw = Mathf.MoveTowardsAngle(yaw, target.eulerAngles.y, 50f * blend * dt);

            // Look a little ahead of a running cat; follow height changes more calmly than sideways moves,
            // so the view does not bob with every jump (research: damping, lookahead, vertical dead zone).
            Vector3 velocity = motor != null ? Vector3.ProjectOnPlane(target.forward, Vector3.up) * motor.Speed : Vector3.zero;
            lookahead = Vector3.Lerp(lookahead, velocity * lookaheadTime, 1f - Mathf.Exp(-3f * dt));
            Vector3 targetFocus = target.position + lookahead + Vector3.up * Mathf.Lerp(focusHeight, climbFocusHeight, blend);
            float k = 1f - Mathf.Exp(-followSharpness * dt);
            float ky = 1f - Mathf.Exp(-(blend > 0.5f ? followSharpness : verticalSharpness) * dt);
            focus = new Vector3(Mathf.Lerp(focus.x, targetFocus.x, k), Mathf.Lerp(focus.y, targetFocus.y, ky), Mathf.Lerp(focus.z, targetFocus.z, k));

            Quaternion rotation = Quaternion.Euler(Mathf.Lerp(pitch, climbPitch, blend), yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float allowed = wantDistance;
            if (Physics.SphereCast(focus, collisionRadius, back, out RaycastHit hit, wantDistance,
                    collisionMask, QueryTriggerInteraction.Ignore))
            {
                allowed = Mathf.Max(0.1f, hit.distance);
            }

            // Blocked for a while (and the player is not turning the view): look for a freer viewpoint nearby.
            occludedTime = allowed < wantDistance * 0.6f ? occludedTime + dt : 0f;
            if (occludedTime > minOcclusionTime && look.sqrMagnitude < 0.01f && blend < 0.1f) // climbing has its own framing
            {
                float best = allowed * 1.3f;
                Vector2 turn = Vector2.zero;
                foreach (var probe in Probes)
                {
                    float probePitch = Mathf.Clamp(pitch + probe.y, minPitch, maxPitch);
                    Vector3 dir = Quaternion.Euler(Mathf.Lerp(probePitch, climbPitch, blend), yaw + probe.x, 0f) * Vector3.back;
                    float free = Physics.SphereCast(focus, collisionRadius, dir, out RaycastHit h, wantDistance, collisionMask, QueryTriggerInteraction.Ignore)
                        ? h.distance : wantDistance;
                    free -= (Mathf.Abs(probe.x) + probe.y) * 0.002f; // prefer the smallest change
                    if (free > best) { best = free; turn = probe; }
                }
                yaw = Mathf.MoveTowardsAngle(yaw, yaw + turn.x, avoidSpeed * dt);
                pitch = Mathf.Clamp(Mathf.MoveTowards(pitch, pitch + turn.y, avoidSpeed * 0.6f * dt), minPitch, maxPitch);
            }

            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.MoveTowards(currentDistance, allowed, returnSpeed * dt);
            transform.SetPositionAndRotation(focus + back * currentDistance, rotation);
        }
    }
}
