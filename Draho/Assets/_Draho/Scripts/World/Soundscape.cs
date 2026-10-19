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

        AudioSource rain, thunder, purr, birds, crickets;
        AudioClip[] songs;
        AudioClip scream;
        float nextSong = 4f;
        Cat.CatAnimator catAnimator;
        float thunderIn = -1f;

        void Awake()
        {
            rain = Source(MakeRain(), true, 0f);
            thunder = Source(MakeThunder(), false, 0.9f);
            purr = Source(MakePurr(), true, 0f);
            birds = Source(null, false, 0.22f);
            crickets = Source(MakeCrickets(), true, 0f);
            songs = new[] { MakeSong(1), MakeSong(2), MakeSong(3), MakeSong(4) };
            scream = MakeScream();
            SkyMood.Flashed += OnFlash;
            Debug.Log($"STATE soundscape: rain {rain.clip.length:0.0} s, thunder {thunder.clip.length:0.0} s, purr {purr.clip.length:0.0} s, {songs.Length} songs, crickets {crickets.clip.length:0.0} s, scream {scream.length:0.00} s");
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
            // Birds sing by day (now and then a phrase); crickets chirp at night; swifts scream when they fly low.
            float night = SkyMood.NightAmount;
            bool calm = !Storm.Active;
            crickets.volume = Mathf.MoveTowards(crickets.volume, calm && night > 0.6f ? 0.12f : 0f, dt * 0.1f);
            nextSong -= dt;
            if (nextSong <= 0f)
            {
                nextSong = Random.Range(3f, 9f);
                if (calm && night < 0.4f)
                {
                    birds.pitch = Random.Range(0.9f, 1.1f);
                    bool low = Swifts.PartyLow;
                    birds.PlayOneShot(low && Random.value < 0.6f ? scream : songs[Random.Range(0, songs.Length)], low ? 0.7f : 1f);
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

        static AudioClip MakeSong(int seed)
        {
            // A blackbird-like phrase: 4-7 fluting notes, each a short glide between 1.5 and 3.5 kHz, with gaps.
            var rng = new System.Random(seed * 31);
            int notes = 4 + rng.Next(4);
            int n = (int)(Rate * 1.6f);
            var data = new float[n];
            int at = 0;
            for (int k = 0; k < notes && at < n; k++)
            {
                float f0 = 1500f + (float)rng.NextDouble() * 2000f, f1 = f0 * (0.8f + (float)rng.NextDouble() * 0.5f);
                int len = (int)(Rate * (0.06f + (float)rng.NextDouble() * 0.12f));
                float phase = 0f;
                for (int i = 0; i < len && at + i < n; i++)
                {
                    float u = (float)i / len;
                    phase += Mathf.Lerp(f0, f1, u) / Rate;
                    float env = Mathf.Sin(u * Mathf.PI);
                    data[at + i] += Mathf.Sin(phase * Mathf.PI * 2f) * env * 0.5f + Mathf.Sin(phase * Mathf.PI * 4f) * env * 0.08f;
                }
                at += len + (int)(Rate * (0.03f + (float)rng.NextDouble() * 0.08f));
            }
            var clip = AudioClip.Create($"Song{seed}", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeScream()
        {
            // Swifts: a harsh, buzzing high trill (4-6 kHz with fast rough modulation), about half a second.
            int n = (int)(Rate * 0.55f);
            var data = new float[n];
            var rng = new System.Random(81);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float f = 5200f + 700f * Mathf.Sin(t * 60f) + (float)(rng.NextDouble() - 0.5) * 900f;
                phase += f / Rate;
                float rough = 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 70f));
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.55f) * Mathf.PI);
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f) * rough * env * 0.35f;
            }
            var clip = AudioClip.Create("SwiftScream", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeCrickets()
        {
            // Bush-crickets in the grass: short bursts of a high tone (about 6.5 kHz), several singers out of step.
            int n = Rate * 3;
            var data = new float[n];
            float[] periods = { 0.42f, 0.57f, 0.73f };
            float[] freqs = { 6400f, 6900f, 6100f };
            for (int s = 0; s < periods.Length; s++)
            {
                float vol = 0.18f - s * 0.04f;
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / Rate + s * 0.13f;
                    float inBurst = (t % periods[s]) / 0.06f;
                    if (inBurst >= 1f) continue;
                    float env = Mathf.Sin(inBurst * Mathf.PI);
                    data[i] += Mathf.Sin(2f * Mathf.PI * freqs[s] * t) * env * vol;
                }
            }
            Crossfade(data, Rate / 20);
            var clip = AudioClip.Create("Crickets", n, 1, Rate, false);
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
