using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// One of Hrozna's rubber toys on the floor of the flat. Step on it and it squeaks: loud enough to wake her from
    /// anywhere in the flat. The squeak is shown as well as heard: the toy squashes and a ring spreads over the
    /// floor (research round 48: readable noise sources, show what made the sound and how far it carries).
    /// </summary>
    [RequireComponent(typeof(AudioSource), typeof(LineRenderer))]
    public class SqueakyToy : MonoBehaviour
    {
        [SerializeField] float stepRadius = 0.2f;
        [SerializeField] float ringRadius = 3.5f;

        static AudioClip squeak;
        static bool told;
        CatMotor cat;
        Hrozna dog;
        AudioSource voice;
        LineRenderer ring;
        Vector3 baseScale;
        float cooldown, ripple = -1f;

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            dog = FindAnyObjectByType<Hrozna>();
            voice = GetComponent<AudioSource>();
            voice.spatialBlend = 0.6f;
            ring = GetComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 32;
            ring.widthMultiplier = 0.03f;
            ring.enabled = false;
            if (squeak == null) squeak = MakeSqueak();
            baseScale = transform.localScale;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (cat != null && cooldown <= 0f && cat.IsGrounded)
            {
                // The body or the front paws (a little ahead of the body) on the toy.
                Vector3 d = cat.transform.position - transform.position;
                Vector3 paws = d + cat.transform.forward * 0.15f;
                d.y = paws.y = 0f;
                if (d.magnitude < stepRadius || paws.magnitude < stepRadius) Squeak();
            }
            // Squash and spring back; the ring spreads and fades.
            float squash = Mathf.Clamp01(cooldown - 1.4f) * 1.6f;
            transform.localScale = new Vector3(baseScale.x * (1f + squash * 0.3f), baseScale.y * (1f - squash * 0.5f), baseScale.z * (1f + squash * 0.3f));
            if (ripple >= 0f)
            {
                ripple += dt / 0.7f;
                if (ripple >= 1f) { ripple = -1f; ring.enabled = false; return; }
                float r = Mathf.Lerp(0.1f, ringRadius, ripple);
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float a = i * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r));
                }
                var c = new Color(1f, 0.85f, 0.3f, 1f - ripple);
                ring.startColor = ring.endColor = c;
            }
        }

        void Squeak()
        {
            cooldown = 2f;
            ripple = 0f;
            ring.enabled = true;
            voice.pitch = Random.Range(0.95f, 1.1f);
            voice.PlayOneShot(squeak, 0.9f);
            Debug.Log($"STORY squeak ({name})");
            if (!told)
            {
                told = true;
                ProgressHud.Toast("SQUEAK! Hrozna's rubber toys are loud. Step around them.");
            }
            dog?.Hear("a squeaky toy");
        }

        static AudioClip MakeSqueak()
        {
            // A rubber squeak: a fast rising whistle with a little buzz, made in code.
            const int rate = 22050;
            int n = rate / 6;
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(900f, 1700f, Mathf.Sqrt(t)) + Mathf.Sin(t * 90f) * 60f;
                phase += 2f * Mathf.PI * f / rate;
                float env = Mathf.Min(1f, t * 25f) * (1f - t);
                data[i] = env * (Mathf.Sin(phase) * 0.7f + Mathf.Sign(Mathf.Sin(phase * 0.5f)) * 0.08f) * 0.5f;
            }
            var clip = AudioClip.Create("Squeak", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
