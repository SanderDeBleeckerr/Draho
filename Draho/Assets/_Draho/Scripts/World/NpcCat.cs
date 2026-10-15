using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A cat character that sits and watches Drahomil (it turns to face him). When Drahomil comes close
    /// without running, it accepts him (<see cref="Welcomed"/>); a running cat makes it turn away.
    /// </summary>
    public class NpcCat : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] float welcomeDistance = 2f;
        [SerializeField] float watchDistance = 12f;

        CatMotor cat;
        float grumpy;

        public bool Welcomed { get; private set; }

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Animator catAnimator) => animator = catAnimator;

        void Awake() => cat = FindAnyObjectByType<CatMotor>();

        void Update()
        {
            if (animator != null) animator.SetBool("Grounded", true); // sits (idle), never "falls"
            if (cat == null) return;
            Vector3 to = cat.transform.position - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            bool running = cat.Speed > cat.Stats.walkSpeed * 1.5f;
            if (running && d < 6f) grumpy = 3f;
            grumpy -= Time.deltaTime;
            if (d < watchDistance && d > 0.1f)
            {
                // Watch him; turn the back on a cat that runs around.
                Vector3 look = grumpy > 0f ? -to : to;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look.normalized), 90f * Time.deltaTime);
            }
            if (!Welcomed && d < welcomeDistance && grumpy <= 0f && !running)
            {
                Welcomed = true;
                Debug.Log($"STORY {name} welcomes Drahomil");
            }
        }
    }
}
