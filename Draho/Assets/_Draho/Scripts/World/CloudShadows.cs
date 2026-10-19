using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Draho.World
{
    /// <summary>
    /// Soft shadows of passing clouds on the town by day: a tiling cookie on the sun that slides with the wind
    /// (research round 61: URP lights take a cookie texture, its size and offset are set per light, and a Repeat
    /// texture tiles over the whole scene). The cookie is made here from noise: mostly full sun, with soft darker
    /// patches. Off at night (the moon has no clouds to show) and in the storm (one grey overcast).
    /// </summary>
    public class CloudShadows : MonoBehaviour
    {
        [SerializeField] float size = 60f;
        [SerializeField] Vector2 wind = new Vector2(0.9f, 0.35f);
        [SerializeField] float shade = 0.45f;

        Light sun;
        UniversalAdditionalLightData sunData;
        Texture2D cookie;
        Vector2 offset;

        void Start()
        {
            var go = GameObject.Find("Sun");
            if (go == null) return;
            sun = go.GetComponent<Light>();
            sunData = go.GetComponent<UniversalAdditionalLightData>();
            if (sunData == null) sunData = go.AddComponent<UniversalAdditionalLightData>();
            // The builder puts the cookie on the sun in the scene (so the build keeps the cookie shader variant).
            cookie = sun.cookie as Texture2D;
            if (cookie == null) cookie = MakeCookie(128, shade);
            sunData.lightCookieSize = new Vector2(size, size);
            Debug.Log("STATE cloud shadows on the sun");
        }

        void Update()
        {
            if (sun == null) return;
            bool day = SkyMood.NightAmount < 0.4f;
            sun.cookie = day ? cookie : null;
            if (!day) return;
            offset += wind * Time.deltaTime;
            sunData.lightCookieOffset = offset;
        }

        /// <summary>Test hook: SendMessage('CloudShadows', 'TestDark'): a nearly black cookie (do cookies work at all?).</summary>
        public void TestDark()
        {
            var dark = new Texture2D(4, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = new Color(0.1f, 0.1f, 0.1f, 0.1f);
            dark.SetPixels(px);
            dark.Apply();
            cookie = dark;
            Debug.Log($"STATE cloud test: cookie {sun != null && sun.cookie != null}, size {sunData?.lightCookieSize}, offset {sunData?.lightCookieOffset}, main {RenderSettings.sun?.name}");
        }

        void OnDestroy()
        {
            if (sun != null) sun.cookie = null;
        }

        public static Texture2D MakeCookie(int n, float shade)
        {
            // Tiling noise: sample Perlin noise on a torus-like wrap (blend four shifted copies) so the edges meet.
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = "CloudCookie" };
            const float scale = 3f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float a = Fbm(u * scale, v * scale), b = Fbm((u - 1f) * scale, v * scale);
                    float c = Fbm(u * scale, (v - 1f) * scale), d = Fbm((u - 1f) * scale, (v - 1f) * scale);
                    float noise = Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
                    // Mostly sun; where the noise is high, a soft cloud shadow.
                    float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.68f, noise));
                    float lit = 1f - shade * cloud;
                    tex.SetPixel(x, y, new Color(lit, lit, lit, lit));
                }
            tex.Apply(true);
            return tex;
        }

        static float Fbm(float x, float y) =>
            0.6f * Mathf.PerlinNoise(x + 11.3f, y + 7.1f) + 0.3f * Mathf.PerlinNoise(x * 2f + 3.7f, y * 2f + 1.9f)
            + 0.1f * Mathf.PerlinNoise(x * 4f + 5.5f, y * 4f + 9.2f);
    }
}
