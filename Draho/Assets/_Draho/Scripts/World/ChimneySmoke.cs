using System.Collections.Generic;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Smoke from the chimneys (research round 76: chimney smoke rises in a narrow plume, widens and fades as it cools
    /// and mixes, and leans with the wind; in a strong wind it is pressed down along the roof). Grey puffs that grow
    /// and fade, a few a second; thicker in the evening and at night (fires are lit), flat and fast in the storm.
    /// </summary>
    public class ChimneySmoke : MonoBehaviour
    {
        [SerializeField] Material puff;
        [SerializeField] Vector3 wind = new Vector3(0.35f, 0f, 0.15f);

        readonly List<ParticleSystem> plumes = new List<ParticleSystem>();

        public void Configure(Material puffMaterial) => puff = puffMaterial;

        void Start()
        {
            if (puff == null) return;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("chimney") || r.name.EndsWith("_cap")) continue; // one plume each, not one for the cap too
                Bounds b = r.bounds;
                var go = new GameObject($"Smoke_{r.name}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
                plumes.Add(MakePlume(go));
            }
            Debug.Log($"STATE chimney smoke: {plumes.Count} plumes");
        }

        ParticleSystem MakePlume(GameObject go)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.42f, 0.42f, 0.45f, 0.6f); // darker than the sky, or it vanishes against it
            main.maxParticles = 60;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // up
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 3.5f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = puff;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        void Update()
        {
            if (plumes.Count == 0) return;
            bool storm = Storm.Active;
            float evening = Mathf.Clamp01(SkyMood.NightAmount * 1.5f);
            float rate = storm ? 6f : Mathf.Lerp(1.2f, 3f, evening);
            Vector3 drift = storm ? new Vector3(-2.2f, -0.15f, 0.6f) : wind * (1f + 0.4f * Mathf.Sin(Time.time * 0.13f));
            foreach (var ps in plumes)
            {
                var emission = ps.emission;
                emission.rateOverTime = rate;
                var force = ps.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = new ParticleSystem.MinMaxCurve(drift.x * 0.4f);
                force.y = new ParticleSystem.MinMaxCurve(drift.y);
                force.z = new ParticleSystem.MinMaxCurve(drift.z * 0.4f);
            }
        }
    }
}
