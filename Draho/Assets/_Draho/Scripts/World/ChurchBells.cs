using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Church bells, made in code (no sound file): a few strokes of a bell tone with overtones that die away.
    /// They ring when the cat first comes near the church, then every few minutes.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ChurchBells : MonoBehaviour
    {
        [SerializeField] float hearDistance = 70f;
        [SerializeField] float interval = 180f;
        [SerializeField] int strokes = 4;

        AudioSource source;
        AudioClip bell;
        Transform cat;
        float nextRing = 3f;
        int left;
        float strokeTimer;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.spatialBlend = 0.6f;
            source.maxDistance = 120f;
            source.rolloffMode = AudioRolloffMode.Linear;
            bell = MakeBell(392f, 2.6f);
            cat = FindAnyObjectByType<Cat.CatMotor>()?.transform;
        }

        void Update()
        {
            if (cat == null) return;
            if (left > 0)
            {
                strokeTimer -= Time.deltaTime;
                if (strokeTimer <= 0f) { source.PlayOneShot(bell); left--; strokeTimer = 1.4f; }
                return;
            }
            nextRing -= Time.deltaTime;
            if (nextRing > 0f || Vector3.Distance(cat.position, transform.position) > hearDistance) return;
            left = strokes;
            nextRing = interval;
            Debug.Log("STORY the begijnhof bells ring");
        }

        static AudioClip MakeBell(float freq, float seconds)
        {
            const int rate = 22050;
            int n = (int)(rate * seconds);
            var data = new float[n];
            // Bell partials (hum, prime, tierce, quint, nominal) with their own decay.
            float[] ratio = { 0.5f, 1f, 1.2f, 1.5f, 2f, 2.6f };
            float[] amp = { 0.35f, 0.5f, 0.3f, 0.2f, 0.25f, 0.1f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, v = 0f;
                for (int k = 0; k < ratio.Length; k++)
                    v += amp[k] * Mathf.Sin(2f * Mathf.PI * freq * ratio[k] * t) * Mathf.Exp(-t * (0.9f + k * 0.7f));
                data[i] = v * 0.35f * Mathf.Min(1f, t * 200f);
            }
            var clip = AudioClip.Create("Bell", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
