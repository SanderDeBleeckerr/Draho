using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Glow-worms in the long grass of the wild garden at night (research round 60). The common glow-worm of
    /// Belgium (Lampyris noctiluca) does not flash: the wingless female sits in the grass and glows a steady
    /// green-yellow, and switches her light off when she is disturbed. A few flying males of the lesser glow-worm
    /// (Lamprohiza splendidula) drift low over the grass with a steady glow too. One particle system draws all the
    /// points; this script places and colours them by hand every frame (a few dozen, cheap).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class GlowWorms : MonoBehaviour
    {
        [SerializeField] Vector3 areaMin = new Vector3(-26f, 0f, 5.3f);
        [SerializeField] Vector3 areaMax = new Vector3(-2.5f, 0f, 9.3f);
        [SerializeField] int grounded = 22;
        [SerializeField] int flying = 7;
        [SerializeField] Color glow = new Color(0.6f, 1f, 0.35f);
        [SerializeField] float shyDistance = 0.7f;

        ParticleSystem system;
        ParticleSystem.Particle[] points;
        Vector3[] homes;
        float[] light, seeds;
        Transform cat;
        float shown;

        public void Configure(Vector3 min, Vector3 max)
        {
            areaMin = min;
            areaMax = max;
        }

        void Awake()
        {
            system = GetComponent<ParticleSystem>();
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = grounded + flying;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.loop = true;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            system.Play(); // nothing is emitted: the points are placed by hand
            var r = GetComponent<ParticleSystemRenderer>();
            if (r.sharedMaterial != null)
            {
                var m = new Material(r.sharedMaterial);
                m.SetTexture("_BaseMap", Dot());
                r.sharedMaterial = m;
            }

            int n = grounded + flying;
            points = new ParticleSystem.Particle[n];
            homes = new Vector3[n];
            light = new float[n];
            seeds = new float[n];
            var rng = new System.Random(60);
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(areaMin.x, areaMax.x, (float)rng.NextDouble());
                float z = Mathf.Lerp(areaMin.z, areaMax.z, (float)rng.NextDouble());
                float y = 0.5f;
                if (Physics.Raycast(new Vector3(x, 3f, z), Vector3.down, out RaycastHit hit, 6f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    y = hit.point.y;
                homes[i] = new Vector3(x, y + (i < grounded ? 0.04f : 0.5f), z);
                seeds[i] = (float)rng.NextDouble() * 100f;
                light[i] = 1f;
                points[i].remainingLifetime = 1e6f;
                points[i].startLifetime = 1e6f;
                points[i].startSize = i < grounded ? 0.05f : 0.04f;
                points[i].position = homes[i];
            }
        }

        void LateUpdate()
        {
            if (cat == null) { var m = FindAnyObjectByType<CatMotor>(); if (m != null) cat = m.transform; }
            shown = Mathf.MoveTowards(shown, SkyMood.NightAmount > 0.5f ? 1f : 0f, Time.deltaTime / 3f);
            if (shown <= 0f) { if (system.particleCount > 0) system.Clear(); return; }
            float t = Time.time, dt = Time.deltaTime;
            Vector3 c = cat != null ? cat.position : Vector3.one * 1e4f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = homes[i];
                if (i >= grounded)
                {
                    // A slow, wandering flight low over the grass.
                    float s = seeds[i];
                    p += new Vector3((Mathf.PerlinNoise(s, t * 0.08f) - 0.5f) * 6f, (Mathf.PerlinNoise(s + 7f, t * 0.2f) - 0.5f) * 0.8f,
                                     (Mathf.PerlinNoise(s + 13f, t * 0.08f) - 0.5f) * 3f);
                    p.x = Mathf.Clamp(p.x, areaMin.x, areaMax.x);
                    p.z = Mathf.Clamp(p.z, areaMin.z, areaMax.z);
                }
                // A female switches off when the cat comes close, and glows again a little later.
                bool shy = i < grounded && (p - c).sqrMagnitude < shyDistance * shyDistance;
                light[i] = Mathf.MoveTowards(light[i], shy ? 0f : 1f, dt * (shy ? 3f : 0.25f));
                float a = shown * light[i] * (0.8f + 0.2f * Mathf.Sin(t * 0.7f + seeds[i]));
                points[i].position = p;
                points[i].startColor = new Color(glow.r, glow.g, glow.b, a);
            }
            system.SetParticles(points, points.Length);
        }

        static Texture2D Dot()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            return tex;
        }
    }
}
