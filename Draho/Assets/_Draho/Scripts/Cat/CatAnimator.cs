using UnityEngine;

namespace Draho.Cat
{
    /// <summary>Sends the motor state to the Animator (blend tree on "Speed").</summary>
    public class CatAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");

        [SerializeField] CatMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] float speedDampTime = 0.08f;

        void Update()
        {
            animator.SetFloat(SpeedId, motor.Speed, speedDampTime, Time.deltaTime);
            animator.SetBool(GroundedId, motor.IsGrounded);
        }
    }
}
