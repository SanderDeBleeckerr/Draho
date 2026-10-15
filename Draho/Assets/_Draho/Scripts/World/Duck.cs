using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A duck on the river. It paddles around its group's spot and bobs on the water.
    /// A cat that runs along the quay scares it to the far bank. A cat that sits still at the
    /// water's edge makes it curious: it paddles close, hoping for bread (<see cref="IsClose"/>).
    /// </summary>
    public class Duck : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Rect river = new Rect(-48f, -420f, 13f, 440f); // x, z of the water
        [SerializeField] float waterY = -1f;
        [SerializeField] float paddleSpeed = 0.35f;
        [SerializeField] float fleeSpeed = 1.6f;
        [SerializeField] float scareDistance = 3.5f;
        [SerializeField] float curiousDistance = 6f;
        [SerializeField] float curiousTime = 2.5f;

        CatMotor cat;
        Vector3 home, target;
        float timer, phase, stillTime;
        bool fleeing;

        /// <summary>True when the duck paddles near the cat (within 1.6 m).</summary>
        public bool IsClose { get; private set; }

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform bodyPart, Transform headPart, Rect water, float surface)
        {
            body = bodyPart;
            head = headPart;
            river = water;
            waterY = surface;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            home = target = transform.position;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            Vector3 p = transform.position;
            Vector3 catPos = cat != null ? cat.transform.position : new Vector3(999f, 0f, 999f);
            float d = Flat(catPos - p).magnitude;
            bool catOnEdge = cat != null && catPos.x > river.xMax - 0.1f && catPos.x < river.xMax + 1.2f && catPos.y > -0.3f && catPos.y < 1f;
            bool catLoud = cat != null && (cat.Speed > cat.Stats.walkSpeed * 1.5f || cat.IsAirborne);

            float speed = paddleSpeed;
            if (catLoud && d < scareDistance)
            {
                // Away to the far bank, quacking.
                if (!fleeing) Debug.Log("STORY ducks flee to the far bank");
                fleeing = true;
                stillTime = 0f;
                target = new Vector3(river.xMin + 1.2f, 0f, p.z + Random.Range(-2f, 2f));
                timer = 6f;
            }
            else if (catOnEdge && cat.Speed < 0.05f && d < curiousDistance && !fleeing)
            {
                stillTime += dt;
                if (stillTime > curiousTime)
                    target = new Vector3(river.xMax - 0.5f, 0f, catPos.z + (transform.GetSiblingIndex() % 3 - 1) * 0.5f); // come closer
            }
            else stillTime = Mathf.Max(0f, stillTime - dt);

            if (fleeing) speed = fleeSpeed;
            timer -= dt;
            if (timer <= 0f && stillTime < curiousTime)
            {
                fleeing = false;
                Vector2 r = Random.insideUnitCircle * 2.5f;
                target = home + new Vector3(r.x, 0f, r.y);
                timer = Random.Range(3f, 7f);
            }

            Vector3 to = Flat(target - p);
            if (to.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), 120f * dt);
                p += transform.forward * Mathf.Min(speed * dt, to.magnitude);
            }
            p.x = Mathf.Clamp(p.x, river.xMin + 0.4f, river.xMax - 0.35f);
            p.y = waterY + Mathf.Sin(phase * 2.2f) * 0.01f; // bobbing
            transform.position = p;
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(phase * 1.7f) * 8f, Mathf.Sin(phase * 0.9f) * 25f, 0f);

            bool close = d < 1.6f;
            if (close && !IsClose) Debug.Log("STORY a duck comes close to the cat");
            IsClose = close;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
