using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A moth at a night lamp. Moths do not fly straight at a light: they keep their backs turned to the
    /// brightest point (the dorsal light response), so near a lamp they loop around it, stall, turn back and
    /// sometimes drop low (research round 34: Fabian et al. 2024). When a moth drops low, a jumping cat can catch it.
    /// Hidden by day; a caught moth comes back from the dark after a few seconds.
    /// A day flier (a butterfly) is the other way round: it shows by day, zigzags (erratic flight makes it hard to
    /// catch) and, when a cat that does not sneak comes close, escapes fast and straight, up and away, before it
    /// comes back to its flowers (research round 45: Eisner &amp; Jantzen 2009; Horisawa, Byrnes &amp; Dudley).
    /// </summary>
    public class Moth : MonoBehaviour
    {
        enum State { Loop, Stall, Dive, Gone, Escape }

        [SerializeField] Vector3 lamp;
        [SerializeField] Transform wingLeft;
        [SerializeField] Transform wingRight;
        [SerializeField] float radius = 0.35f;
        [SerializeField] float speed = 1.4f;
        [Tooltip("How far below the lamp a diving moth comes (m).")]
        [SerializeField] float diveDrop = 0.95f;
        [SerializeField] float catchRadius = 0.32f;
        [Tooltip("A butterfly: by day, zigzags, escapes from a cat that does not sneak.")]
        [SerializeField] bool dayFlier;
        [SerializeField] float escapeDistance = 1.0f;

        public static int CaughtCount { get; private set; }
        public static int ButterfliesCaught { get; private set; }

        CatMotor cat;
        Renderer[] renderers;
        State state = State.Loop;
        float angle, direction = 1f, timer, phase, height, ring;
        Vector3 escape;
        bool visible = true;

        public void Configure(Vector3 lampPoint, Transform left, Transform right, bool butterfly = false)
        {
            dayFlier = butterfly;
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
            bool active = dayFlier ? SkyMood.NightAmount < 0.3f : SkyMood.NightAmount > 0.3f;
            Show(active && state != State.Gone);
            if (!active) return;

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
                case State.Escape:
                    // Fast and straight, up and away; then back to the flowers along the usual ring.
                    transform.position += escape * (2.2f * dt) + Vector3.up * (1.1f * dt);
                    Flap(dt);
                    if (timer <= 0f)
                    {
                        Vector3 off = transform.position - lamp;
                        angle = Mathf.Atan2(off.z, off.x);
                        ring = new Vector2(off.x, off.z).magnitude;
                        height = transform.position.y;
                        state = State.Loop;
                        timer = Random.Range(2f, 4f);
                    }
                    return;
            }
            if (dayFlier && cat != null && state != State.Gone && !cat.IsCrouching && cat.Speed > 0.05f)
            {
                Vector3 away = transform.position - cat.transform.position;
                away.y = 0f;
                if (away.magnitude < escapeDistance)
                {
                    state = State.Escape;
                    timer = 1.6f;
                    escape = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
                    Debug.Log("STORY butterfly escapes");
                    return;
                }
            }

            // Fly: a wobbly ring around the lamp, lower and wider when diving.
            float targetHeight = state == State.Dive ? lamp.y - diveDrop : lamp.y + Mathf.Sin(phase * 1.7f) * 0.12f;
            float targetRing = state == State.Dive ? radius * 1.8f : radius * (1f + 0.25f * Mathf.Sin(phase * 2.3f));
            height = Mathf.MoveTowards(height, targetHeight, (state == State.Dive ? 1.4f : 0.9f) * dt);
            ring = Mathf.MoveTowards(ring, targetRing, 0.8f * dt);
            float spin = state == State.Stall ? 0.15f : 1f;
            if (dayFlier) spin *= 0.6f + 0.9f * Mathf.Abs(Mathf.Sin(phase * 2.9f)); // zigzag: speed and turns change all the time
            angle += direction * spin * speed / Mathf.Max(0.1f, ring) * dt;
            float bob = dayFlier ? Mathf.Sin(phase * 3.1f) * 0.14f + Mathf.Sin(phase * 7.7f) * 0.05f : Mathf.Sin(phase * 9f) * 0.03f;
            float wobble = dayFlier ? 1f + 0.35f * Mathf.Sin(phase * 4.3f) : 1f;
            Vector3 p = lamp + new Vector3(Mathf.Cos(angle) * ring * wobble, height - lamp.y + bob, Mathf.Sin(angle) * ring * wobble);
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * direction;
            // The back turns to the light.
            Vector3 up = (lamp - p).normalized;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, Vector3.Lerp(Vector3.up, up, 0.7f)));
            Flap(dt);

            if (cat != null && (cat.IsAirborne || cat.IsRearing || cat.IsClimbing))
            {
                Vector3 paws = cat.transform.position + Vector3.up * 0.22f + cat.transform.forward * 0.15f;
                float d = Vector3.Distance(paws, p);
                if (d < catchRadius) Catch($"at {d:0.00} m");
            }
        }

        void Flap(float dt)
        {
            float flap = dayFlier ? 35f + Mathf.Sin(phase * 16f) * 45f : Mathf.Sin(phase * 38f) * 55f; // butterflies: slower, bigger strokes
            if (wingLeft != null) wingLeft.localRotation = Quaternion.Euler(0f, 0f, -flap);
            if (wingRight != null) wingRight.localRotation = Quaternion.Euler(0f, 0f, flap);
        }

        void Catch(string how)
        {
            if (dayFlier)
            {
                ButterfliesCaught++;
                Debug.Log($"STORY butterfly caught {how} ({ButterfliesCaught})");
            }
            else
            {
                CaughtCount++;
                Debug.Log($"STORY moth caught {how} ({CaughtCount})");
            }
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
