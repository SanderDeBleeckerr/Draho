using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Sounds made in code (research round 85, like the bells): rain (filtered noise, fades with the storm), thunder
    /// a moment after each flash (a low rumble of brown noise that dies away; the delay is the distance, about 3 s
    /// per km), and the cat's purr while it sits (a low buzz at about 25 Hz, louder on the out-breath).
    /// </summary>
    public class Soundscape : MonoBehaviour
    {
        const int Rate = 22050;

        AudioSource rain, thunder, purr;
        Cat.CatAnimator catAnimator;
        float thunderIn = -1f;

        void Awake()
        {
            rain = Source(MakeRain(), true, 0f);
            thunder = Source(MakeThunder(), false, 0.9f);
            purr = Source(MakePurr(), true, 0f);
            SkyMood.Flashed += OnFlash;
            Debug.Log($"STATE soundscape: rain {rain.clip.length:0.0} s, thunder {thunder.clip.length:0.0} s, purr {purr.clip.length:0.0} s");
        }

        void Start() => catAnimator = FindAnyObjectByType<Cat.CatAnimator>();

        void OnDestroy() => SkyMood.Flashed -= OnFlash;

        AudioSource Source(AudioClip clip, bool loop, float volume)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.volume = volume;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            if (loop) s.Play();
            return s;
        }

        void OnFlash() => thunderIn = Random.Range(0.4f, 2.5f);

        void Update()
        {
            float dt = Time.deltaTime;
            rain.volume = Mathf.MoveTowards(rain.volume, Storm.Active ? 0.45f : 0f, dt * 0.3f);
            if (thunderIn >= 0f)
            {
                thunderIn -= dt;
                if (thunderIn < 0f)
                {
                    thunder.pitch = Random.Range(0.8f, 1.1f);
                    thunder.PlayOneShot(thunder.clip, Random.Range(0.6f, 1f));
                }
            }
            bool sitting = catAnimator != null && catAnimator.IdleTime > 4f;
            purr.volume = Mathf.MoveTowards(purr.volume, sitting ? 0.28f : 0f, dt * (sitting ? 0.15f : 0.6f));
        }

        static AudioClip MakeRain()
        {
            // Two seconds of noise, lightly low-passed, with tiny random "drops"; the loop seam is crossfaded.
            int n = Rate * 2;
            var data = new float[n];
            var rng = new System.Random(85);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.35f;
                float drop = rng.NextDouble() < 0.0015 ? (float)rng.NextDouble() * 0.8f : 0f;
                data[i] = lp * 0.5f + drop;
            }
            Crossfade(data, Rate / 10);
            var clip = AudioClip.Create("Rain", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeThunder()
        {
            // A crack, then a long rolling rumble: brown noise with slow swells, dying away over 4 s.
            int n = Rate * 4;
            var data = new float[n];
            var rng = new System.Random(66);
            float brown = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                brown = Mathf.Clamp(brown + white * 0.06f, -1f, 1f) * 0.998f;
                lp += (brown - lp) * 0.08f;
                float roll = 0.6f + 0.4f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 1.7f + 1f);
                float env = Mathf.Exp(-t * 0.9f) * Mathf.Clamp01(t * 30f);
                float crack = t < 0.15f ? white * (0.15f - t) * 3f : 0f;
                data[i] = Mathf.Clamp((lp * 2.2f * roll + crack) * env, -1f, 1f);
            }
            var clip = AudioClip.Create("Thunder", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakePurr()
        {
            // A 2 s breath cycle: a low buzz of pulses at about 25 Hz (in-breath softer and a little higher).
            int n = Rate * 2;
            var data = new float[n];
            var rng = new System.Random(25);
            float lp = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                bool outBreath = t >= 0.8f;
                float rate = outBreath ? 24f : 27f;
                phase += rate / Rate;
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f)), 6f);
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white * pulse - lp) * 0.12f;
                float breath = outBreath ? Mathf.Sin((t - 0.8f) / 1.2f * Mathf.PI) : 0.55f * Mathf.Sin(t / 0.8f * Mathf.PI);
                data[i] = lp * 3f * breath;
            }
            Crossfade(data, Rate / 20);
            var clip = AudioClip.Create("Purr", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Crossfade(float[] data, int len)
        {
            // Blend the end into the start, so the loop has no click.
            int n = data.Length;
            for (int i = 0; i < len; i++)
            {
                float k = (float)i / len;
                data[i] = data[i] * k + data[n - len + i] * (1f - k);
            }
        }
    }
}
