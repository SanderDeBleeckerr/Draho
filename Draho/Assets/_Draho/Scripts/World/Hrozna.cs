using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Hrozna, the Cardigan corgi next door: loud, but not a real enemy. She sleeps in her basket. A cat she
    /// hears (running is loud, sneaking is quiet) wakes her: she barks and chases the cat out of the window.
    /// Then she trots back to her basket and falls asleep again.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Hrozna : MonoBehaviour
    {
        enum State { Sleep, Bark, Chase, Return }

        [SerializeField] Transform body;
        [SerializeField] Transform tail;
        [SerializeField] Transform basket;
        [SerializeField] Vector3 throwOutTo = new Vector3(-3f, 0.05f, 7.1f);
        [SerializeField] Rect flat = new Rect(-2f, 5.25f, 9.6f, 3.75f);
        [Header("Hearing (m)")]
        [SerializeField] float hearRunning = 4f;
        [SerializeField] float hearWalking = 1.8f;
        [SerializeField] float hearSneaking = 0.45f;
        [SerializeField] float chaseSpeed = 2.0f;

        CatMotor cat;
        AudioSource voice;
        AudioClip bark;
        State state = State.Sleep;
        float timer, phase, nextBark;

        public bool Asleep => state == State.Sleep;

        public void Configure(Transform bodyPart, Transform tailPart, Transform basketSpot)
        {
            body = bodyPart;
            tail = tailPart;
            basket = basketSpot;
        }

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            voice = GetComponent<AudioSource>();
            voice.spatialBlend = 0.5f;
            bark = MakeBark();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            Vector3 p = transform.position;
            Vector3 catPos = cat.transform.position;
            bool catInside = flat.Contains(new Vector2(catPos.x, catPos.z)) && catPos.y < 1.5f;
            float d = Vector3.Distance(new Vector3(catPos.x, 0f, catPos.z), new Vector3(p.x, 0f, p.z));

            switch (state)
            {
                case State.Sleep:
                    if (body != null) body.localScale = new Vector3(1f, 1f + Mathf.Sin(phase * 1.5f) * 0.03f, 1f); // breathing
                    if (catInside && d < Hearing())
                    {
                        state = State.Bark;
                        timer = 0.8f;
                        ProgressHud.Toast("WOOF! WOOF! Hrozna is awake!");
                        Debug.Log("STORY Hrozna wakes up");
                    }
                    break;
                case State.Bark:
                    Face(catPos - p);
                    Woof();
                    timer -= dt;
                    if (timer <= 0f) state = State.Chase;
                    break;
                case State.Chase:
                    Woof();
                    if (!catInside) { state = State.Return; break; }
                    Face(catPos - p);
                    transform.position = Vector3.MoveTowards(p, new Vector3(catPos.x, 0f, catPos.z), chaseSpeed * dt);
                    if (d < 0.45f)
                    {
                        cat.Teleport(throwOutTo, Quaternion.Euler(0f, 270f, 0f));
                        ProgressHud.Toast("Hrozna chases Drahomil out of the window. Sneak in more quietly!");
                        Debug.Log("STORY Hrozna chases the cat out");
                        state = State.Return;
                    }
                    break;
                case State.Return:
                    Vector3 home = basket != null ? basket.position : p;
                    Face(home - p);
                    transform.position = Vector3.MoveTowards(p, new Vector3(home.x, 0f, home.z), chaseSpeed * 0.4f * dt);
                    if (Vector3.Distance(transform.position, new Vector3(home.x, 0f, home.z)) < 0.05f)
                    {
                        timer -= dt;
                        if (timer < -6f) { state = State.Sleep; timer = 0f; Debug.Log("STORY Hrozna sleeps again"); }
                    }
                    break;
            }
            if (tail != null) tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(phase * (state == State.Sleep ? 1f : 14f)) * (state == State.Sleep ? 4f : 35f), 0f);
        }

        float Hearing()
        {
            if (cat.IsCrouching) return hearSneaking;
            if (cat.Speed > cat.Stats.walkSpeed * 1.5f || cat.IsAirborne) return hearRunning;
            return cat.Speed < 0.05f ? 0.3f : hearWalking;
        }

        void Woof()
        {
            if (Time.time < nextBark) return;
            nextBark = Time.time + Random.Range(0.45f, 0.8f);
            voice.pitch = Random.Range(0.9f, 1.15f);
            voice.PlayOneShot(bark, 0.8f);
        }

        void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized), 360f * Time.deltaTime);
        }

        static AudioClip MakeBark()
        {
            // A short, rough "woof": a falling tone with noise, made in code.
            const int rate = 22050;
            int n = rate / 5;
            var data = new float[n];
            var rng = new System.Random(5);
            float phaseAcc = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(520f, 260f, t);
                phaseAcc += 2f * Mathf.PI * f / rate;
                float env = Mathf.Min(1f, t * 30f) * Mathf.Pow(1f - t, 1.5f);
                float tone = Mathf.Sin(phaseAcc) + 0.5f * Mathf.Sin(phaseAcc * 2f) + 0.25f * Mathf.Sin(phaseAcc * 3f);
                data[i] = env * (0.5f * tone + 0.35f * ((float)rng.NextDouble() * 2f - 1f)) * 0.5f;
            }
            var clip = AudioClip.Create("Woof", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
