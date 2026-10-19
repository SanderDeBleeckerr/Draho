using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// The cat's own sounds, made in code (research round 88): a soft thump of the pads after a hard landing, and a
    /// meow for the humans ("mi-a-ow": the pitch rises, then falls, while the mouth opens and closes, so the vowel
    /// moves from i through a to u; adult cats meow mostly at people, not at other cats).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CatVoice : MonoBehaviour
    {
        const int Rate = 22050;

        static CatVoice instance;
        AudioSource voice;
        CatMotor motor;
        AudioClip meow, thump;
        float lowestFall;
        bool wasAirborne;

        void Awake()
        {
            instance = this;
            voice = GetComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.playOnAwake = false;
            motor = GetComponent<CatMotor>();
            meow = MakeMeow();
            thump = MakeThump();
        }

        /// <summary>A meow for the humans (greeting, a gift).</summary>
        public static void Meow()
        {
            if (instance == null) return;
            instance.voice.pitch = Random.Range(0.92f, 1.08f);
            instance.voice.PlayOneShot(instance.meow, 0.55f);
            Debug.Log("CAT meows");
        }

        void Update()
        {
            if (motor == null) return;
            bool airborne = motor.IsAirborne;
            if (airborne) lowestFall = Mathf.Min(lowestFall, motor.VerticalVelocity);
            if (wasAirborne && !airborne && lowestFall < -3f)
            {
                voice.pitch = Random.Range(0.9f, 1.1f);
                voice.PlayOneShot(thump, Mathf.Clamp01(-lowestFall / 9f) * 0.5f);
            }
            if (!airborne) lowestFall = 0f;
            wasAirborne = airborne;
        }

        static AudioClip MakeMeow()
        {
            int n = (int)(Rate * 0.7f);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                // Pitch: 550 Hz up to 820 at a third, down to 440 at the end.
                float f0 = u < 0.33f ? Mathf.Lerp(550f, 820f, u / 0.33f) : Mathf.Lerp(820f, 440f, (u - 0.33f) / 0.67f);
                phase += f0 / Rate;
                // Formants move i -> a -> u.
                float f1 = u < 0.4f ? Mathf.Lerp(350f, 850f, u / 0.4f) : Mathf.Lerp(850f, 380f, (u - 0.4f) / 0.6f);
                float f2 = u < 0.4f ? Mathf.Lerp(2300f, 1250f, u / 0.4f) : Mathf.Lerp(1250f, 850f, (u - 0.4f) / 0.6f);
                float sum = 0f;
                for (int k = 1; k <= 10; k++)
                {
                    float fk = f0 * k;
                    float a = Mathf.Exp(-Mathf.Pow((fk - f1) / 260f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((fk - f2) / 380f, 2f)) + 0.05f;
                    sum += a * Mathf.Sin(phase * k * Mathf.PI * 2f) / k;
                }
                float env = Mathf.Clamp01(u / 0.07f) * Mathf.Clamp01((1f - u) / 0.25f);
                data[i] = sum * env * 0.45f;
            }
            var clip = AudioClip.Create("Meow", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeThump()
        {
            // Soft pads on a hard floor: a short low knock with a little noise.
            int n = (int)(Rate * 0.12f);
            var data = new float[n];
            var rng = new System.Random(88);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.1f;
                float env = Mathf.Exp(-t * 45f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.7f + lp * 0.8f) * env;
            }
            var clip = AudioClip.Create("Thump", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Test hook: SendMessage('Drahomil', 'TestMeow').</summary>
        public void TestMeow() => Meow();
    }
}
