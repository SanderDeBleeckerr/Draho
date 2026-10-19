using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Sends the motor state to the Animator (locomotion, crouch and air states), and adds "game feel":
    /// a short stretch on take-off, a squash on landing that grows with the fall speed, and a dust puff
    /// (research: landing squash scaled by fall height; keep anticipation very short).
    /// </summary>
    public class CatAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int AirborneId = Animator.StringToHash("Airborne");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int RearId = Animator.StringToHash("Rear");
        static readonly int ClimbId = Animator.StringToHash("Climb");
        static readonly int ClimbSpeedId = Animator.StringToHash("ClimbSpeed");
        static readonly int IdleTimeId = Animator.StringToHash("IdleTime");

        [SerializeField] CatMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] float speedDampTime = 0.08f;
        [Header("Game feel")]
        [SerializeField] ParticleSystem dust;
        [SerializeField] float maxSquash = 0.22f;
        [SerializeField] float jumpStretch = 0.1f;
        [SerializeField] float recoverSpeed = 9f;

        Vector3 baseScale;
        float squash;          // > 0 squashed (landing), < 0 stretched (take-off)
        bool wasAirborne;
        float lowestFall;      // most negative vertical speed during this jump
        float idle;            // seconds standing still (the cat sits down after a while)

        void Start() => baseScale = animator.transform.localScale;

        void Update()
        {
            animator.SetFloat(SpeedId, motor.Speed, speedDampTime, Time.deltaTime);
            animator.SetBool(GroundedId, motor.IsGrounded);
            animator.SetBool(AirborneId, motor.IsAirborne);
            animator.SetBool(CrouchId, motor.IsCrouching);
            animator.SetFloat(VerticalSpeedId, motor.VerticalVelocity, 0.05f, Time.deltaTime);
            animator.SetBool(RearId, motor.IsRearing);
            animator.SetBool(ClimbId, motor.IsClimbing);
            // The Climb clip plays at the speed that matches the paws; negative plays it backwards (down).
            animator.SetFloat(ClimbSpeedId, motor.ClimbVelocity / motor.Stats.climbClipSpeed, 0.05f, Time.deltaTime);
            bool still = motor.Speed < 0.05f && motor.IsGrounded && !motor.IsCrouching && !motor.IsRearing && !motor.IsClimbing;
            idle = still ? idle + Time.deltaTime : 0f;
            animator.SetFloat(IdleTimeId, idle);
            GameFeel();
        }

        void GameFeel()
        {
            bool airborne = motor.IsAirborne && !motor.IsClimbing;
            if (airborne)
            {
                if (!wasAirborne && motor.VerticalVelocity > 0.5f) squash = -jumpStretch; // take-off stretch
                lowestFall = Mathf.Min(lowestFall, motor.VerticalVelocity);
            }
            else if (wasAirborne)
            {
                // Landing: the harder the fall, the bigger the squash (and the dust).
                float impact = Mathf.Clamp01(-lowestFall / 9f);
                squash = Mathf.Lerp(0.06f, maxSquash, impact);
                if (impact > 0.15f) Debug.Log($"CAT lands, impact {impact:0.00}, squash {squash:0.00}");
                if (dust != null && impact > 0.15f)
                {
                    dust.transform.position = motor.transform.position + Vector3.up * 0.02f;
                    dust.Emit(Mathf.RoundToInt(Mathf.Lerp(4f, 14f, impact)));
                }
                lowestFall = 0f;
            }
            wasAirborne = airborne;
            squash = Mathf.MoveTowards(squash, 0f, recoverSpeed * Mathf.Max(Mathf.Abs(squash), 0.02f) * Time.deltaTime);
            // Keep the volume: squashed is lower and wider, stretched is taller and thinner.
            float y = 1f - squash, xz = 1f / Mathf.Sqrt(Mathf.Max(0.5f, y));
            animator.transform.localScale = new Vector3(baseScale.x * xz, baseScale.y * y, baseScale.z * xz);
        }
    }
}
