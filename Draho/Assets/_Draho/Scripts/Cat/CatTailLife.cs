using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Small procedural life on top of the clips, added after the Animator each frame (research round 67, cat body
    /// language): the tail held up while walking (a relaxed, friendly cat), the tip twitching while stalking (the
    /// hunting focus), a lazy sway when standing; now and then an ear flicks; in the rain the ears go flat back.
    /// The turns are made round the cat's own axes in world space, so they do not depend on the bones' local axes.
    /// </summary>
    public class CatTailLife : MonoBehaviour
    {
        [SerializeField] CatMotor motor;
        [SerializeField] CatAnimator catAnimator;
        [SerializeField] float tailUp = 18f;
        [SerializeField] float stalkTwitch = 16f;
        [SerializeField] float sway = 7f;

        Transform[] tail = new Transform[4];
        Transform earL, earR;
        float up, stalk, idle, rain, phase, flick, flickTime = 4f, flickSide;

        void Start()
        {
            if (motor == null) motor = GetComponent<CatMotor>();
            if (catAnimator == null) catAnimator = GetComponent<CatAnimator>();
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                switch (t.name)
                {
                    case "Tail1": tail[0] = t; break;
                    case "Tail2": tail[1] = t; break;
                    case "Tail3": tail[2] = t; break;
                    case "Tail4": tail[3] = t; break;
                    case "Ear.L": earL = t; break;
                    case "Ear.R": earR = t; break;
                }
            }
        }

        void LateUpdate()
        {
            if (motor == null || tail[0] == null) return;
            float dt = Time.deltaTime;
            phase += dt;
            bool grounded = motor.IsGrounded && !motor.IsClimbing && !motor.IsRearing;
            bool sitting = catAnimator != null && catAnimator.IdleTime > 3.5f; // the Sit clip curls the tail round the paws
            float walk = grounded && !motor.IsCrouching ? Mathf.Clamp01(motor.Speed / 0.6f) * Mathf.Clamp01((2.6f - motor.Speed) / 1.2f) : 0f;
            up = Mathf.MoveTowards(up, walk, dt * 2f);
            stalk = Mathf.MoveTowards(stalk, grounded && motor.IsCrouching ? 1f : 0f, dt * 3f);
            idle = Mathf.MoveTowards(idle, grounded && !motor.IsCrouching && motor.Speed < 0.05f && !sitting ? 1f : 0f, dt * 1.5f);
            rain = Mathf.MoveTowards(rain, Draho.World.Storm.Active ? 1f : 0f, dt);

            Vector3 upAxis = transform.up, right = transform.right, forward = transform.forward;
            // Tail up while walking: lift the base, let the rest follow.
            if (up > 0.01f) Turn(tail[0], right, tailUp * up); // a positive turn round "right" lifts a backward-pointing tail
            // The tip twitches in short bursts while stalking; a slow lazy sway when standing.
            float burst = Mathf.Max(0f, Mathf.Sin(phase * 0.9f)); // bursts, then still
            float twitch = Mathf.Sin(phase * 17f) * stalkTwitch * stalk * burst;
            float lazy = Mathf.Sin(phase * 2.2f) * sway * idle;
            Turn(tail[2], upAxis, twitch * 0.5f + lazy * 0.6f);
            Turn(tail[3], upAxis, twitch + lazy);

            // Ears: a quick flick now and then; flat back in the rain.
            flickTime -= dt;
            if (flickTime <= 0f) { flick = 1f; flickSide = Random.value < 0.5f ? -1f : 1f; flickTime = Random.Range(3f, 8f); }
            flick = Mathf.MoveTowards(flick, 0f, dt * 6f);
            float flickAngle = Mathf.Sin(flick * Mathf.PI) * 22f;
            if (earL != null) Turn(earL, forward, (flickSide < 0f ? flickAngle : 0f) + 28f * rain);
            if (earR != null) Turn(earR, forward, -(flickSide > 0f ? flickAngle : 0f) - 28f * rain);
        }

        static void Turn(Transform bone, Vector3 axis, float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) < 0.01f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
