using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A red fox that walks its night route through the wild garden, sniffing at a few spots, to its den
    /// under the bushes in the far corner. Foxes and cats share the night and mostly ignore each other
    /// (research round 35), so the fox is no danger: the cat follows it. Following is forgiving: the fox
    /// waits when the cat falls far behind, and when the cat comes close without sneaking it stops and
    /// stares, then trots on to its next spot. It never ends the chase.
    /// </summary>
    public class Fox : MonoBehaviour
    {
        enum State { Hidden, Walk, Sniff, Wait, Stare, Trot, Den }

        [SerializeField] Transform head;
        [SerializeField] Transform tail;
        [SerializeField] Transform[] legs;
        [SerializeField] Vector3[] route;
        [SerializeField] bool[] sniffAt;
        [SerializeField] float walkSpeed = 0.75f;
        [SerializeField] float trotSpeed = 1.8f;
        [Tooltip("The fox waits when the cat is further away than this.")]
        [SerializeField] float waitDistance = 7f;
        [Tooltip("A cat that does not sneak makes the fox stop and stare inside this distance.")]
        [SerializeField] float noticeDistance = 1.8f;

        CatMotor cat;
        Renderer[] renderers;
        State state = State.Hidden;
        int next;
        float timer, phase, stare;

        public bool AtDen => state == State.Den;
        public bool Started => state != State.Hidden;
        public int Noticed { get; private set; }

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform headPart, Transform tailPart, Transform[] legParts, Vector3[] path, bool[] sniff)
        {
            head = headPart;
            tail = tailPart;
            legs = legParts;
            route = path;
            sniffAt = sniff;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            renderers = GetComponentsInChildren<Renderer>();
            Show(false);
        }

        /// <summary>The story calls this when the fox comes over the fence.</summary>
        public void Begin()
        {
            if (state != State.Hidden || route == null || route.Length == 0) return;
            transform.position = route[0];
            next = 1;
            state = State.Walk;
            Show(true);
            Debug.Log("STORY fox comes over the fence");
        }

        void Update()
        {
            if (state == State.Hidden || state == State.Den) return;
            float dt = Time.deltaTime;
            phase += dt;
            Vector3 toCat = cat != null ? Flat(cat.transform.position - transform.position) : Vector3.forward * 99f;
            float d = toCat.magnitude;
            bool sneaking = cat != null && (cat.IsCrouching || cat.Speed < 0.05f);
            float speed = 0f;

            switch (state)
            {
                case State.Walk:
                case State.Trot:
                    if (state == State.Walk && d < noticeDistance && !sneaking) { StartStare(); break; }
                    if (state == State.Walk && d > waitDistance) { state = State.Wait; Debug.Log("STORY fox waits and looks back"); break; }
                    speed = state == State.Trot ? trotSpeed : walkSpeed;
                    if (MoveTo(route[next], speed, dt)) Arrive();
                    break;
                case State.Sniff:
                    timer -= dt;
                    if (d < noticeDistance && !sneaking) { StartStare(); break; }
                    if (timer <= 0f) state = State.Walk;
                    break;
                case State.Wait:
                    if (d < waitDistance - 1.5f) state = State.Walk;
                    break;
                case State.Stare:
                    // Too close: watch the cat. Back off or sneak, and it goes on; stay, and it trots away.
                    if (d > noticeDistance + 0.7f || sneaking) stare -= dt * 2f;
                    else stare += dt;
                    Face(toCat, dt);
                    if (stare <= 0f) state = State.Walk;
                    else if (stare > 2.5f) { state = State.Trot; Debug.Log("STORY fox trots away from the cat"); }
                    break;
            }
            Animate(speed, dt);
        }

        void StartStare()
        {
            state = State.Stare;
            stare = 0.01f;
            Noticed++;
            Debug.Log($"STORY fox stops and stares at the cat ({Noticed})");
        }

        bool MoveTo(Vector3 point, float speed, float dt)
        {
            Vector3 step = Flat(point - transform.position);
            if (step.magnitude < 0.05f) return true;
            Face(step, dt);
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(point.x, transform.position.y, point.z), speed * dt);
            return false;
        }

        void Arrive()
        {
            if (next >= route.Length - 1)
            {
                state = State.Den;
                Show(false);
                Debug.Log("STORY fox slips into its den under the bushes");
                return;
            }
            bool sniff = sniffAt != null && next < sniffAt.Length && sniffAt[next];
            next++;
            if (sniff) { state = State.Sniff; timer = Random.Range(2f, 3f); }
            else state = State.Walk;
        }

        void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), 200f * dt);
        }

        void Animate(float speed, float dt)
        {
            float gait = speed > 0f ? phase * (speed > 1f ? 14f : 8f) : 0f;
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    legs[i].localRotation = Quaternion.Euler(speed > 0f ? Mathf.Sin(gait + (i % 2 == 0 ? 0f : Mathf.PI) + (i < 2 ? 0f : Mathf.PI * 0.5f)) * 28f : 0f, 0f, 0f);
            if (head != null)
            {
                float down = state == State.Sniff ? 35f + Mathf.Sin(phase * 9f) * 6f : 0f;
                float look = state == State.Wait ? Mathf.Sin(phase * 0.8f) * 70f : 0f;
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(down, look, 0f), 6f * dt);
            }
            if (tail != null)
                tail.localRotation = Quaternion.Euler(-15f + Mathf.Sin(phase * 3f) * 5f, Mathf.Sin(phase * 2f) * 10f, 0f);
        }

        void Show(bool on)
        {
            foreach (var r in renderers) r.enabled = on;
        }

        /// <summary>Test hook: SendMessage('Fox', 'TestBegin').</summary>
        public void TestBegin() => Begin();

        /// <summary>Test hook: SendMessage('Fox', 'TestToDen'): jump to the last part of the route.</summary>
        public void TestToDen()
        {
            if (route == null || route.Length < 2) return;
            if (state == State.Hidden) Begin();
            next = route.Length - 1;
            transform.position = route[route.Length - 2];
            state = State.Trot;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
