using UnityEngine;

namespace Draho.Cat
{
    /// <summary>Tuning values for the cat. Change them in the asset; no code change needed.</summary>
    [CreateAssetMenu(menuName = "Draho/Cat Stats", fileName = "CatStats")]
    public class CatStats : ScriptableObject
    {
        [Header("Speed (m/s)")]
        public float walkSpeed = 0.5f;
        public float runSpeed = 2.2f;
        public float acceleration = 5f;
        public float deceleration = 8f;

        [Header("Turning")]
        [Tooltip("Degrees per second.")]
        public float turnSpeed = 480f;

        [Header("Jump")]
        [Tooltip("How high the paws rise, in metres. The kitchen counter is 0.9 m.")]
        public float jumpHeight = 1.05f;
        [Tooltip("Part of the ground speed the cat can still steer in the air (0..1).")]
        public float airControl = 0.5f;
        [Tooltip("A jump press this long before landing still counts (seconds).")]
        public float jumpBuffer = 0.15f;
        [Tooltip("A jump just after walking off an edge still counts (seconds).")]
        public float coyoteTime = 0.1f;

        [Header("Crouch")]
        public float crouchSpeed = 0.28f;
        [Tooltip("Height of the collider while crouched (standing is 0.26 m).")]
        public float crouchHeight = 0.16f;

        [Header("Jump, rear up and climb")]
        [Tooltip("A jump keeps the speed the cat had this long after the stick was released.")]
        public float jumpMomentumTime = 0.45f;
        [Tooltip("Slower than this, a jump becomes 'stand up on the hind legs'.")]
        public float rearSpeedThreshold = 0.2f;
        public float rearTime = 1.1f;
        public float climbSpeed = 0.6f;
        [Tooltip("How far in front of the body a climbable surface can be.")]
        public float climbReach = 0.3f;
        [Tooltip("Climb onto the top when the paws are this close to it.")]
        public float mantleReach = 0.35f;
        public float mantleTime = 0.5f;
        [Tooltip("Speed of the jump away from a wall.")]
        public float climbLeapSpeed = 1.2f;

        [Header("Gravity")]
        public float gravity = -20f;
        public float maxFallSpeed = 30f;

        [Header("Animation")]
        [Tooltip("Speed at which the Walk clip's paws do not slide, at playback speed 1.")]
        public float walkClipSpeed = 0.315f; // measured by Tools/Blender/convert_lowpoly_cat.py
        [Tooltip("Speed at which the Run clip's paws do not slide, at playback speed 1.")]
        public float runClipSpeed = 0.666f;
        [Tooltip("Speed at which the CrouchWalk clip's paws do not slide, at playback speed 1.")]
        public float crouchClipSpeed = 0.194f;
        public float climbClipSpeed = 0.24f;
    }
}
