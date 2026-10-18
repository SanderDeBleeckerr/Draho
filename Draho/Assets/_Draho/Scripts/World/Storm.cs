using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The night storm over the roofs: rain around the camera, lightning flashes and wind gusts.
    /// A gust is announced (the rain slants, a howl in the HUD) a moment before it pushes, so the player can
    /// react (research round 41); a crouched cat feels only a little of it (<see cref="CatMotor.Wind"/>).
    /// Gusts only push on the roofs; on the ground the storm is just weather.
    /// </summary>
    public class Storm : MonoBehaviour
    {
        enum Phase { Calm, Warning, Gust }

        [SerializeField] ParticleSystem rain;
        [SerializeField] float rainRate = 700f;
        [SerializeField] Vector3 gustDirection = new Vector3(-1f, 0f, 0.25f);
        [SerializeField] float gustSpeed = 2.4f;
        [SerializeField] float warningTime = 1.3f;
        [SerializeField] float gustTime = 1.8f;
        [SerializeField] float roofHeight = 8f;

        static Storm instance;
        CatMotor cat;
        Transform cam;
        Phase phase = Phase.Calm;
        float timer, lightning, slant;
        int gusts;

        public static bool Active { get; private set; }

        public void Configure(ParticleSystem rainSystem) => rain = rainSystem;

        void Awake()
        {
            instance = this;
            Active = false;
            cat = FindAnyObjectByType<CatMotor>();
            if (rain != null)
            {
                var emission = rain.emission;
                emission.rateOverTime = 0f;
            }
        }

        /// <summary>The story starts the storm.</summary>
        public static void Begin()
        {
            if (instance == null || Active) return;
            Active = true;
            instance.timer = 6f;
            instance.lightning = 3f;
            SkyMood.Set(SkyMood.Mood.Storm, 6f);
            if (instance.rain != null)
            {
                instance.rain.Play();
                var emission = instance.rain.emission;
                emission.rateOverTime = instance.rainRate;
            }
            Debug.Log("STORY the storm breaks");
        }

        /// <summary>The story ends the storm.</summary>
        public static void End()
        {
            if (instance == null || !Active) return;
            Active = false;
            instance.SetWind(Vector3.zero);
            if (instance.rain != null)
            {
                var emission = instance.rain.emission;
                emission.rateOverTime = 0f;
            }
            Debug.Log("STORY the storm passes");
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null) transform.position = cam.position + Vector3.up * 5f; // rain falls round the camera
            if (!Active) return;
            float dt = Time.deltaTime;

            lightning -= dt;
            if (lightning <= 0f)
            {
                SkyMood.Flash();
                lightning = Random.Range(7f, 14f);
            }

            timer -= dt;
            bool onRoof = cat != null && cat.transform.position.y > roofHeight;
            switch (phase)
            {
                case Phase.Calm:
                    if (timer <= 0f && onRoof)
                    {
                        phase = Phase.Warning;
                        timer = warningTime;
                        if (gusts < 3) ProgressHud.Toast("The wind howls... DUCK, or it blows you down the roof!");
                        Debug.Log("STORY gust coming");
                    }
                    break;
                case Phase.Warning:
                    if (timer <= 0f)
                    {
                        phase = Phase.Gust;
                        timer = gustTime;
                        gusts++;
                        Debug.Log($"STORY gust ({(cat != null && cat.IsCrouching ? "cat ducks" : "cat stands")})");
                    }
                    break;
                case Phase.Gust:
                    if (timer <= 0f)
                    {
                        phase = Phase.Calm;
                        timer = Random.Range(5f, 9f);
                    }
                    break;
            }
            // The rain slants before and during a gust: the warning you can see.
            float targetSlant = phase == Phase.Calm ? 0f : 1f;
            slant = Mathf.MoveTowards(slant, targetSlant, dt * 2.5f);
            if (rain != null)
            {
                var velocity = rain.velocityOverLifetime;
                velocity.enabled = true;
                Vector3 push = gustDirection.normalized * (slant * 6f);
                velocity.x = new ParticleSystem.MinMaxCurve(push.x);
                velocity.y = new ParticleSystem.MinMaxCurve(0f);
                velocity.z = new ParticleSystem.MinMaxCurve(push.z);
            }
            SetWind(phase == Phase.Gust && onRoof ? gustDirection.normalized * gustSpeed : Vector3.zero);
        }

        void SetWind(Vector3 wind)
        {
            if (cat != null) cat.Wind = wind;
        }

        /// <summary>Test hook: SendMessage('Rain', 'TestStorm').</summary>
        public void TestStorm() => Begin();

        /// <summary>Test hook: SendMessage('Rain', 'TestGust'): a gust warning now.</summary>
        public void TestGust()
        {
            if (!Active) Begin();
            phase = Phase.Calm;
            timer = 0f;
        }
    }
}
