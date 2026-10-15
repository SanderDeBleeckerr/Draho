using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A blackbird on the lawn. It hops and pecks, and it flies up to a perch (the fence or the wall)
    /// when it hears or sees the cat. After a while it comes back down, far from the cat.
    /// Like the mouse: a running cat is loud, a sneaking (crouching) cat is almost silent.
    /// <see cref="WasStalked"/> becomes true when the cat sneaks close to the bird on the ground.
    /// </summary>
    public class Bird : MonoBehaviour
    {
        enum State { Ground, Flying, Perched }

        [SerializeField] Transform body;
        [SerializeField] Transform head;
        [SerializeField] Transform wingLeft;
        [SerializeField] Transform wingRight;
        [SerializeField] Vector3[] perches;
        [Tooltip("Where the bird can land: x0, z0, x1, z1 (the lawn).")]
        [SerializeField] Vector4 lawn = new Vector4(-20f, 0.8f, -4f, 4.2f);

        [Header("Senses (m)")]
        [SerializeField] float hearRunning = 4f;
        [SerializeField] float hearWalking = 2.2f;
        [SerializeField] float hearSneaking = 0.6f;
        [SerializeField] float seeAlways = 0.45f;
        [SerializeField] float stalkRadius = 1.3f;

        [Header("Moves")]
        [SerializeField] float flySpeed = 3.5f;
        [SerializeField] float hopDistance = 0.12f;
        [SerializeField] float homeRadius = 1.2f;

        CatMotor cat;
        State state = State.Ground;
        Vector3 home;
        float timer;
        // Hop or flight: from, to, time.
        Vector3 from, to;
        float moveTime, moveDuration, arc;
        bool landing;
        bool stayLong;
        float phase;

        public bool OnGround => state == State.Ground;
        public bool WasStalked { get; private set; }

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform bodyPart, Transform headPart, Transform left, Transform right, Vector3[] perchPoints, Vector4 lawnRect)
        {
            body = bodyPart;
            head = headPart;
            wingLeft = left;
            wingRight = right;
            perches = perchPoints;
            lawn = lawnRect;
        }

        void Awake() // not Start: the story may send the bird somewhere in its own Start
        {
            cat = FindAnyObjectByType<CatMotor>();
            home = transform.position;
            from = to = home;
            timer = Random.Range(0.5f, 2f);
            phase = Random.value * 10f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            switch (state)
            {
                case State.Ground: UpdateGround(dt); break;
                case State.Flying: UpdateFlight(dt); break;
                case State.Perched: UpdatePerched(dt); break;
            }
        }

        void UpdateGround(float dt)
        {
            if (cat != null)
            {
                float d = Flat(cat.transform.position - transform.position).magnitude;
                if (cat.IsCrouching && d < stalkRadius && !WasStalked)
                {
                    WasStalked = true;
                    Debug.Log($"STORY bird stalked at {d:0.00} m");
                }
                if (Scared(d))
                {
                    Debug.Log($"STORY bird flies up, cat at {d:0.00} m");
                    FlyTo(PickPerch(), landing: false);
                    return;
                }
            }

            if (moveTime < moveDuration)
            {
                // Hop: a short jump with both feet.
                moveTime += dt;
                float t = Mathf.Clamp01(moveTime / moveDuration);
                transform.position = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.04f;
                return;
            }

            timer -= dt;
            // Peck: dip the head now and then while standing.
            if (head != null)
                head.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(phase * 7f)) * 55f * (timer % 2f > 1.3f ? 1f : 0f), 0f, 0f);
            if (timer > 0f) return;

            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 next = transform.position + dir * hopDistance * Random.Range(1f, 2.5f);
            if (Flat(next - home).magnitude > homeRadius) next = transform.position + Flat(home - transform.position).normalized * hopDistance;
            next.y = 0f;
            transform.rotation = Quaternion.LookRotation(Flat(next - transform.position).normalized + transform.forward * 0.01f);
            from = transform.position;
            to = next;
            moveTime = 0f;
            moveDuration = 0.18f;
            timer = Random.Range(0.4f, 1.6f);
        }

        void UpdateFlight(float dt)
        {
            moveTime += dt;
            float t = Mathf.Clamp01(moveTime / moveDuration);
            Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arc;
            Vector3 step = p - transform.position;
            if (step.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Flat(step).normalized + Vector3.forward * 1e-4f), 8f * dt);
            transform.position = p;
            Flap(t < 0.9f ? 1f : 0.3f);
            if (t < 1f) return;

            Flap(0f);
            if (landing)
            {
                state = State.Ground;
                home = transform.position;
                timer = Random.Range(0.5f, 1.5f);
                moveTime = moveDuration = 0f;
            }
            else
            {
                state = State.Perched;
                timer = stayLong ? 40f : Random.Range(5f, 9f);
                stayLong = false;
            }
        }

        void UpdatePerched(float dt)
        {
            timer -= dt;
            if (head != null) head.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * 1.3f) * 40f, 0f); // looks around
            if (timer > 0f) return;
            // Come back down to a spot far from the cat.
            Vector3 best = home;
            float bestScore = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var spot = new Vector3(Random.Range(lawn.x, lawn.z), 0f, Random.Range(lawn.y, lawn.w));
                float fromCat = cat != null ? Flat(spot - cat.transform.position).magnitude : 10f;
                float score = Mathf.Min(fromCat, 8f) - 0.2f * Flat(spot - transform.position).magnitude;
                if (score > bestScore) { bestScore = score; best = spot; }
            }
            if (cat != null && Flat(best - cat.transform.position).magnitude < 3f) { timer = 2f; return; }
            FlyTo(best, landing: true);
        }

        /// <summary>The story sends a bird to a perch (for example a branch the cat must climb to).</summary>
        public void FlyToPerch(Vector3 perch)
        {
            FlyTo(perch, landing: false);
            stayLong = true; // wait there for the cat
        }

        bool Scared(float distance)
        {
            float radius = cat.IsCrouching ? hearSneaking
                : cat.Speed < 0.05f ? seeAlways
                : cat.Speed > cat.Stats.walkSpeed * 1.5f ? hearRunning
                : hearWalking;
            // A landing is loud (not the small drop when the cat stands up from a crouch).
            if ((cat.IsAirborne && cat.VerticalVelocity < -1.5f) || cat.IsClimbing) radius = Mathf.Max(radius, hearWalking);
            return distance < Mathf.Max(radius, seeAlways);
        }

        Vector3 PickPerch()
        {
            if (perches == null || perches.Length == 0) return transform.position + Vector3.up * 2f;
            // A perch away from the cat, not too far to fly.
            Vector3 best = perches[0];
            float bestScore = float.MinValue;
            foreach (var p in perches)
            {
                float fromCat = cat != null ? Flat(p - cat.transform.position).magnitude : 0f;
                float score = Mathf.Min(fromCat, 6f) - 0.3f * Flat(p - transform.position).magnitude + Random.Range(0f, 1f);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        void FlyTo(Vector3 point, bool landing)
        {
            state = State.Flying;
            this.landing = landing;
            from = transform.position;
            to = point;
            moveTime = 0f;
            moveDuration = Mathf.Max(0.6f, Vector3.Distance(from, to) / flySpeed);
            arc = landing ? 0.6f : 0.4f;
            if (head != null) head.localRotation = Quaternion.identity;
        }

        void Flap(float amount)
        {
            // The wings hang folded along the body at 0 degrees; flying beats them between 0 and 100.
            float angle = amount > 0f ? (50f + Mathf.Sin(phase * 38f) * 50f) * amount : 0f;
            if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, 0f, -angle);
            if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (body != null) body.localRotation = Quaternion.Euler(amount > 0f ? -20f : 0f, 0f, 0f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
