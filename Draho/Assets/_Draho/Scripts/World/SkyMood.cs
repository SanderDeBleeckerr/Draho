using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Draho.World
{
    /// <summary>
    /// The time of day: sun (or moon), ambient light, fog and sky blend between Day, Evening and Night.
    /// Night uses "Hollywood darkness" (research round 33): it feels dark but stays readable. The moon is a
    /// cool blue key light, the ambient light is a dim blue fill, the fog is closer and dark blue, and the
    /// cat's fill light grows into a moon-blue rim so the black fur keeps its shape.
    /// Night also loses colour, as the eye does in the dark (Purkinje shift): less saturation, blue shadows,
    /// slightly warm highlights (split toning), so lamps still look warm.
    /// The story calls <see cref="Set"/>; the playtest can call SendMessage('SkyMood', 'TestMood', 'Night').
    /// </summary>
    public class SkyMood : MonoBehaviour
    {
        public enum Mood { Day, Evening, Night, Storm, Dawn }

        struct Preset
        {
            public Vector3 sunEuler;
            public Color sun, sky, equator, ground, fog, skyTint, fill;
            public float sunIntensity, exposure, fogStart, fogEnd, fillIntensity, fillRange, night, clouds;
        }

        static SkyMood instance;
        static readonly int NightId = Shader.PropertyToID("_DrahoNight");
        static readonly int MoonDirId = Shader.PropertyToID("_DrahoMoonDir");

        Light sun, fill;
        Material sky;
        Preset day, from, to;
        float blend = 1f, duration = 1f;
        Preset now;
        float flash;
        ColorAdjustments grade;
        SplitToning split;
        float baseSaturation;

        public static Mood Current { get; private set; } = Mood.Day;
        /// <summary>0 by day, 0.5 in the evening, 1 at night (blended): lamps and moths use it.</summary>
        public static float NightAmount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance == null) new GameObject("SkyMood").AddComponent<SkyMood>();
        }

        /// <summary>Change the mood over some seconds (the first second after loading: at once).</summary>
        public static void Set(Mood mood, float seconds = 8f)
        {
            if (instance == null) Create();
            instance.Begin(mood, Time.timeSinceLevelLoad < 1f ? 0f : seconds);
        }

        void Awake()
        {
            instance = this;
            Current = Mood.Day;
            var sunGo = GameObject.Find("Sun");
            if (sunGo != null) sun = sunGo.GetComponent<Light>();
            var fillGo = GameObject.Find("CatFillLight");
            if (fillGo != null) fill = fillGo.GetComponent<Light>();
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_SkyTint"))
            {
                sky = new Material(RenderSettings.skybox); // do not change the shared asset
                RenderSettings.skybox = sky;
            }
            var volume = FindAnyObjectByType<Volume>();
            if (volume != null)
            {
                var profile = volume.profile; // a copy for this run
                if (profile.TryGet(out grade)) baseSaturation = grade.saturation.value;
                if (!profile.TryGet(out split)) split = profile.Add<SplitToning>(true);
            }
            day = Capture();
            from = to = day;
        }

        Preset Capture() => new Preset
        {
            sunEuler = sun != null ? sun.transform.eulerAngles : new Vector3(50f, 30f, 0f),
            sun = sun != null ? sun.color : Color.white,
            sunIntensity = sun != null ? sun.intensity : 1f,
            sky = RenderSettings.ambientSkyColor,
            equator = RenderSettings.ambientEquatorColor,
            ground = RenderSettings.ambientGroundColor,
            fog = RenderSettings.fogColor,
            fogStart = RenderSettings.fogStartDistance,
            fogEnd = RenderSettings.fogEndDistance,
            skyTint = sky != null ? sky.GetColor("_SkyTint") : Color.gray,
            exposure = sky != null ? sky.GetFloat("_Exposure") : 1f,
            fill = fill != null ? fill.color : Color.white,
            fillIntensity = fill != null ? fill.intensity : 0f,
            fillRange = fill != null ? fill.range : 1f,
        };

        Preset For(Mood mood)
        {
            Preset p = day;
            if (mood == Mood.Evening)
            {
                // Golden hour: a low, warm sun from the west, long shadows, a pink-orange horizon.
                p.sunEuler = new Vector3(12f, day.sunEuler.y + 40f, 0f);
                p.sun = new Color(1f, 0.66f, 0.42f);
                p.sunIntensity = 1.05f;
                p.sky = new Color(0.5f, 0.5f, 0.66f);
                p.equator = new Color(0.62f, 0.46f, 0.38f);
                p.ground = new Color(0.24f, 0.17f, 0.14f);
                p.fog = new Color(0.86f, 0.64f, 0.55f);
                p.fogStart = 60f;
                p.fogEnd = 150f;
                p.skyTint = new Color(0.62f, 0.5f, 0.55f);
                p.exposure = 1.0f;
                p.night = 0.5f;
            }
            else if (mood == Mood.Night)
            {
                // Moonlight: a high, cool key light that still casts shadows; a dim blue fill everywhere.
                // Not pure blue and orange: the ambient keeps a little violet as a third colour.
                p.sunEuler = new Vector3(32f, day.sunEuler.y - 70f, 0f); // a low moon: often in view
                p.sun = new Color(0.6f, 0.7f, 1f);
                p.sunIntensity = 0.32f;
                p.sky = new Color(0.17f, 0.2f, 0.34f);
                p.equator = new Color(0.14f, 0.13f, 0.22f);
                p.ground = new Color(0.05f, 0.05f, 0.07f);
                p.fog = new Color(0.08f, 0.1f, 0.18f);
                p.fogStart = 25f;
                p.fogEnd = 95f;
                p.skyTint = new Color(0.22f, 0.26f, 0.42f);
                p.exposure = 0.22f;
                // The fill light near the camera becomes a moon-blue rim on the black fur.
                p.fill = new Color(0.62f, 0.74f, 1f);
                p.fillIntensity = 0.42f;
                p.fillRange = 1.9f;
                p.night = 1f;
            }
            else if (mood == Mood.Storm)
            {
                // A storm at night: clouds hide the moon and the stars, the fog closes in, grey-blue and dark.
                p = For(Mood.Night);
                p.sun = new Color(0.55f, 0.62f, 0.8f);
                p.sunIntensity = 0.16f;
                p.sky = new Color(0.13f, 0.15f, 0.22f);
                p.equator = new Color(0.11f, 0.12f, 0.16f);
                p.fog = new Color(0.07f, 0.08f, 0.11f);
                p.fogStart = 12f;
                p.fogEnd = 70f;
                p.skyTint = new Color(0.2f, 0.22f, 0.28f);
                p.exposure = 0.12f;
                p.clouds = 1f;
            }
            else if (mood == Mood.Dawn)
            {
                // Sunrise after the storm: a low gold sun from the east (opposite the evening sun), long shadows,
                // the sky still cool and blue-pink from the blue hour; the lamps are still dimly on.
                p.sunEuler = new Vector3(6f, day.sunEuler.y - 140f, 0f);
                p.sun = new Color(1f, 0.72f, 0.48f);
                p.sunIntensity = 0.85f;
                p.sky = new Color(0.45f, 0.5f, 0.66f);
                p.equator = new Color(0.7f, 0.56f, 0.52f);
                p.ground = new Color(0.22f, 0.18f, 0.17f);
                p.fog = new Color(0.8f, 0.68f, 0.68f);
                p.fogStart = 40f;
                p.fogEnd = 140f;
                p.skyTint = new Color(0.55f, 0.5f, 0.62f);
                p.exposure = 0.8f;
                p.night = 0.35f;
            }
            return p;
        }

        /// <summary>A lightning flash: a short, bright cold light over everything (the storm calls it).</summary>
        public static void Flash()
        {
            if (instance == null) return;
            instance.flash = 1f;
            Debug.Log("SKY lightning");
        }

        void Begin(Mood mood, float seconds)
        {
            from = Current == mood && blend >= 1f ? For(mood) : Lerp(from, to, Smooth(blend));
            to = For(mood);
            Current = mood;
            duration = Mathf.Max(0.01f, seconds);
            blend = seconds <= 0f ? 1f : 0f;
            Apply(seconds <= 0f ? to : from);
            Debug.Log($"SKY mood {mood} over {seconds:0} s");
        }

        void Update()
        {
            if (blend < 1f)
            {
                blend = Mathf.Min(1f, blend + Time.deltaTime / duration);
                Apply(Lerp(from, to, Smooth(blend)));
            }
            if (flash <= 0f) return;
            // Two quick bright pulses, then the dark again.
            flash = Mathf.Max(0f, flash - Time.deltaTime * 2.5f);
            float pulse = flash > 0.75f || (flash > 0.45f && flash < 0.6f) ? flash : 0f;
            if (sun != null) sun.intensity = now.sunIntensity + pulse * 2.2f;
            RenderSettings.ambientSkyColor = now.sky + new Color(0.45f, 0.5f, 0.65f) * pulse;
            RenderSettings.ambientEquatorColor = now.equator + new Color(0.3f, 0.32f, 0.4f) * pulse;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        static Preset Lerp(Preset a, Preset b, float t) => new Preset
        {
            sunEuler = Quaternion.Slerp(Quaternion.Euler(a.sunEuler), Quaternion.Euler(b.sunEuler), t).eulerAngles,
            sun = Color.Lerp(a.sun, b.sun, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            sky = Color.Lerp(a.sky, b.sky, t),
            equator = Color.Lerp(a.equator, b.equator, t),
            ground = Color.Lerp(a.ground, b.ground, t),
            fog = Color.Lerp(a.fog, b.fog, t),
            fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t),
            fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
            skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
            exposure = Mathf.Lerp(a.exposure, b.exposure, t),
            fill = Color.Lerp(a.fill, b.fill, t),
            fillIntensity = Mathf.Lerp(a.fillIntensity, b.fillIntensity, t),
            fillRange = Mathf.Lerp(a.fillRange, b.fillRange, t),
            night = Mathf.Lerp(a.night, b.night, t),
            clouds = Mathf.Lerp(a.clouds, b.clouds, t),
        };

        void Apply(Preset p)
        {
            now = p;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                sun.color = p.sun;
                sun.intensity = p.sunIntensity;
            }
            RenderSettings.ambientSkyColor = p.sky;
            RenderSettings.ambientEquatorColor = p.equator;
            RenderSettings.ambientGroundColor = p.ground;
            RenderSettings.fogColor = p.fog;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            if (sky != null)
            {
                sky.SetColor("_SkyTint", p.skyTint);
                sky.SetFloat("_Exposure", p.exposure);
            }
            if (fill != null)
            {
                fill.color = p.fill;
                fill.intensity = p.fillIntensity;
                fill.range = p.fillRange;
            }
            NightAmount = p.night;
            float dark = Mathf.Clamp01(p.night * 2f - 1f); // only the real night loses colour
            // Stars and the moon (Draho/NightSky); the moon sits where the night light comes from.
            Shader.SetGlobalFloat(NightId, dark * (1f - p.clouds)); // clouds hide the stars and the moon
            if (sun != null) Shader.SetGlobalVector(MoonDirId, -sun.transform.forward);
            if (grade != null) grade.saturation.Override(Mathf.Lerp(baseSaturation, -35f, dark));
            if (split != null)
            {
                Color grey = new Color(0.5f, 0.5f, 0.5f);
                split.shadows.Override(Color.Lerp(grey, new Color(0.32f, 0.42f, 0.72f), dark));
                split.highlights.Override(Color.Lerp(grey, new Color(0.66f, 0.56f, 0.42f), dark * 0.7f));
            }
        }

        /// <summary>Test hook: SendMessage('SkyMood', 'TestMood', 'Night'), at once.</summary>
        public void TestMood(string mood)
        {
            if (System.Enum.TryParse(mood, out Mood m)) Begin(m, 0f);
        }
    }
}
