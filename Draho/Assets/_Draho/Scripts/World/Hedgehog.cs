using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A hedgehog in the wild garden. It snuffles slowly around. When the cat moves close, it rolls
    /// into a prickly ball. When the cat sits still near it for a while, it trusts the cat
    /// (<see cref="Trusts"/>) and walks to its nest, waiting when the cat falls behind (<see cref="AtNest"/>).
    /// </summary>
    public class Hedgehog : MonoBehaviour
    {
        enum State { Snuffle, Curled, Lead, Home }

        [SerializeField] Transform body;
        [SerializeField] Transform snout;
        [SerializeField] Transform nest;
        [SerializeField] float wanderRadius = 1.5f;
        [SerializeField] float walkSpeed = 0.14f;
        [SerializeField] float curlDistance = 1.1f;
        [SerializeField] float trustDistance = 1.4f;
        [SerializeField] float trustTime = 4f;

        CatMotor cat;
        State state = State.Snuffle;
        Vector3 home, target;
        float timer, trust, phase;
        Vector3 bodyScale;

        public bool Trusts { get; private set; }
        public bool AtNest => state == State.Home;
        public bool IsCurled => state == State.Curled;

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform bodyPart, Transform snoutPart, Transform nestPoint)
        {
            body = bodyPart;
            snout = snoutPart;
            nest = nestPoint;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            home = target = transform.position;
            if (body != null) bodyScale = body.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            float d = cat != null ? Flat(cat.transform.position - transform.position).magnitude : 99f;
            bool catMoves = cat != null && (cat.Speed > 0.05f || cat.IsAirborne);

            switch (state)
            {
                case State.Snuffle:
                    if (d < curlDistance && catMoves) { Curl(); break; }
                    UpdateTrust(d, catMoves, dt);
                    timer -= dt;
                    if (timer <= 0f || Flat(target - transform.position).magnitude < 0.05f)
                    {
                        Vector2 r = Random.insideUnitCircle * wanderRadius;
                        target = home + new Vector3(r.x, 0f, r.y);
                        timer = Random.Range(2f, 5f);
                    }
                    Walk(target, walkSpeed, dt);
                    break;

                case State.Curled:
                    timer -= dt;
                    UpdateTrust(d, catMoves, dt);
                    if (d < curlDistance && catMoves) timer = 4f; // still scared
                    if (timer <= 0f) Uncurl();
                    break;

                case State.Lead:
                    // Walk to the nest; wait for the cat when it is far behind.
                    if (d < 3.5f) Walk(nest.position, walkSpeed * 2.5f, dt); // leads at 0.35 m/s, waits for the cat
                    if (Flat(nest.position - transform.position).magnitude < 0.08f)
                    {
                        state = State.Home;
                        Debug.Log("STORY hedgehog at its nest");
                    }
                    break;

                case State.Home:
                    if (body != null) body.localPosition = new Vector3(0f, Mathf.Sin(phase * 2f) * 0.003f, 0f); // breathing
                    break;
            }
        }

        void UpdateTrust(float d, bool catMoves, float dt)
        {
            if (Trusts) return;
            trust = d < trustDistance && !catMoves ? trust + dt : Mathf.Max(0f, trust - dt * 0.5f);
            if (trust < trustTime) return;
            Trusts = true;
            Debug.Log("STORY hedgehog trusts the cat");
            if (state == State.Curled) Uncurl();
            if (nest != null) state = State.Lead;
        }

        void Curl()
        {
            state = State.Curled;
            timer = 4f;
            if (body != null) body.localScale = new Vector3(bodyScale.x * 0.95f, bodyScale.y * 1.25f, bodyScale.z * 0.7f);
            if (snout != null) snout.gameObject.SetActive(false);
            Debug.Log("STORY hedgehog curls up");
        }

        void Uncurl()
        {
            state = Trusts && nest != null ? State.Lead : State.Snuffle;
            if (body != null) body.localScale = bodyScale;
            if (snout != null) snout.gameObject.SetActive(true);
        }

        void Walk(Vector3 to, float speed, float dt)
        {
            Vector3 dir = Flat(to - transform.position);
            if (dir.sqrMagnitude < 0.0025f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), 180f * dt);
            transform.position += transform.forward * speed * dt;
            // Short legs: a quick waddle.
            if (body != null) body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 18f) * 4f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
