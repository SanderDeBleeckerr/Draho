using UnityEngine;

namespace Draho.Cat
{
    /// <summary>Sends the motor state to the Animator (locomotion, crouch and air states).</summary>
    public class CatAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int AirborneId = Animator.StringToHash("Airborne");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");

        [SerializeField] CatMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] float speedDampTime = 0.08f;

        void Update()
        {
            animator.SetFloat(SpeedId, motor.Speed, speedDampTime, Time.deltaTime);
            animator.SetBool(GroundedId, motor.IsGrounded);
            animator.SetBool(AirborneId, motor.IsAirborne);
            animator.SetBool(CrouchId, motor.IsCrouching);
            animator.SetFloat(VerticalSpeedId, motor.VerticalVelocity, 0.05f, Time.deltaTime);
        }
    }
}
