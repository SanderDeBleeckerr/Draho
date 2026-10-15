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

        [Header("Gravity")]
        public float gravity = -20f;
        public float maxFallSpeed = 30f;

        [Header("Animation")]
        [Tooltip("Speed at which the Walk clip's paws do not slide, at playback speed 1.")]
        public float walkClipSpeed = 0.30f;
        [Tooltip("Speed at which the Run clip's paws do not slide, at playback speed 1.")]
        public float runClipSpeed = 1.35f;
    }
}
