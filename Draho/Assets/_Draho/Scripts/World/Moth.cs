using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A moth at a night lamp. Moths do not fly straight at a light: they keep their backs turned to the
    /// brightest point (the dorsal light response), so near a lamp they loop around it, stall, turn back and
    /// sometimes drop low (research round 34: Fabian et al. 2024). When a moth drops low, a jumping cat can catch it.
    /// Hidden by day; a caught moth comes back from the dark after a few seconds.
    /// </summary>
    public class Moth : MonoBehaviour
    {
        enum State { Loop, Stall, Dive, Gone }

        [SerializeField] Vector3 lamp;
        [SerializeField] Transform wingLeft;
        [SerializeField] Transform wingRight;
        [SerializeField] float radius = 0.35f;
        [SerializeField] float speed = 1.4f;
        [Tooltip("How far below the lamp a diving moth comes (m).")]
        [SerializeField] float diveDrop = 0.95f;
        [SerializeField] float catchRadius = 0.32f;

        public static int CaughtCount { get; private set; }

        CatMotor cat;
        Renderer[] renderers;
        State state = State.Loop;
        float angle, direction = 1f, timer, phase, height, ring;
        bool visible = true;

        public void Configure(Vector3 lampPoint, Transform left, Transform right)
        {
            lamp = lampPoint;
            wingLeft = left;
            wingRight = right;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            renderers = GetComponentsInChildren<Renderer>();
            angle = Random.value * Mathf.PI * 2f;
            phase = Random.value * 10f;
            direction = Random.value < 0.5f ? -1f : 1f;
            timer = Random.Range(1f, 4f);
            height = lamp.y;
            ring = radius;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            bool night = SkyMood.NightAmount > 0.3f;
            Show(night && state != State.Gone);
            if (!night) return;

            timer -= dt;
            switch (state)
            {
                case State.Gone:
                    if (timer <= 0f) { state = State.Loop; timer = Random.Range(2f, 4f); }
                    return;
                case State.Loop:
                    if (timer <= 0f)
                    {
                        // Stall and turn back, or drop low for a moment.
                        if (Random.value < 0.45f) { state = State.Dive; timer = Random.Range(1.6f, 2.4f); }
                        else { state = State.Stall; timer = Random.Range(0.3f, 0.7f); }
                    }
                    break;
                case State.Stall:
                    if (timer <= 0f) { direction = -direction; state = State.Loop; timer = Random.Range(2f, 5f); }
                    break;
                case State.Dive:
                    if (timer <= 0f) { state = State.Loop; timer = Random.Range(3f, 6f); }
                    break;
            }

            // Fly: a wobbly ring around the lamp, lower and wider when diving.
            float targetHeight = state == State.Dive ? lamp.y - diveDrop : lamp.y + Mathf.Sin(phase * 1.7f) * 0.12f;
            float targetRing = state == State.Dive ? radius * 1.8f : radius * (1f + 0.25f * Mathf.Sin(phase * 2.3f));
            height = Mathf.MoveTowards(height, targetHeight, (state == State.Dive ? 1.4f : 0.9f) * dt);
            ring = Mathf.MoveTowards(ring, targetRing, 0.8f * dt);
            float spin = state == State.Stall ? 0.15f : 1f;
            angle += direction * spin * speed / Mathf.Max(0.1f, ring) * dt;
            Vector3 p = lamp + new Vector3(Mathf.Cos(angle) * ring, height - lamp.y + Mathf.Sin(phase * 9f) * 0.03f, Mathf.Sin(angle) * ring);
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * direction;
            // The back turns to the light.
            Vector3 up = (lamp - p).normalized;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, Vector3.Lerp(Vector3.up, up, 0.7f)));
            float flap = Mathf.Sin(phase * 38f) * 55f;
            if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, 0f, -flap);
            if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, 0f, flap);

            if (cat != null && (cat.IsAirborne || cat.IsRearing || cat.IsClimbing))
            {
                Vector3 paws = cat.transform.position + Vector3.up * 0.22f + cat.transform.forward * 0.15f;
                float d = Vector3.Distance(paws, p);
                if (d < catchRadius) Catch($"at {d:0.00} m");
            }
        }

        void Catch(string how)
        {
            CaughtCount++;
            Debug.Log($"STORY moth caught {how} ({CaughtCount})");
            state = State.Gone;
            timer = Random.Range(4f, 7f);
            height = lamp.y + 0.6f; // it comes back from above, out of the dark
        }

        void Show(bool on)
        {
            if (on == visible) return;
            visible = on;
            foreach (var r in renderers) r.enabled = on;
        }

        /// <summary>Test hook: SendMessage('Moth_1', 'TestCatch').</summary>
        public void TestCatch() { if (state != State.Gone) Catch("(test)"); }

        /// <summary>Test hook: SendMessage('Moth_1', 'TestDive'): drop low now.</summary>
        public void TestDive() { if (state != State.Gone) { state = State.Dive; timer = 3f; } }
    }
}
