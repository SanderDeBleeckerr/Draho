using System.Collections.Generic;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Rain makes the town wet: outdoor surfaces get darker and glossier while the storm lasts, so the lamps and the
    /// lightning shine on the roofs and the stones, and they dry slowly after. No new shader: like Lagarde's wet
    /// surfaces (research round 57), only the dry material values change: water fills the pores, so a rough, porous
    /// surface darkens most, and every surface gets a smooth film of water on top.
    /// Only materials that are mostly under the open sky change (a raycast up from each renderer), so the rooms stay dry.
    /// </summary>
    public class WetWeather : MonoBehaviour
    {
        [SerializeField] float wetSmoothness = 0.82f;
        [SerializeField] float maxDarken = 0.45f;
        [SerializeField] float wettingTime = 20f;
        [SerializeField] float dryingTime = 150f;

        struct Entry { public Material material; public Color color; public float smoothness; }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        readonly List<Entry> entries = new List<Entry>();
        float wet, applied = -1f;
        bool testWet;

        public static float Wetness { get; private set; }

        void Start()
        {
            // Count, for every opaque Lit material, how many of its renderers are under the open sky.
            var votes = new Dictionary<Material, int>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponentInParent<Draho.Cat.CatMotor>() != null) continue;
                int vote = Open(r.bounds) ? 1 : -1;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.HasProperty(Smoothness) || !m.HasProperty(BaseColor)) continue;
                    if (m.GetFloat("_Surface") > 0.5f || m.IsKeywordEnabled("_EMISSION")) continue; // glass, glowing lamps
                    votes.TryGetValue(m, out int v);
                    votes[m] = v + vote;
                }
            }
            foreach (var kv in votes)
                if (kv.Value > 0)
                    entries.Add(new Entry { material = kv.Key, color = kv.Key.GetColor(BaseColor), smoothness = kv.Key.GetFloat(Smoothness) });
            Debug.Log($"STATE wet weather: {entries.Count} outdoor materials");
        }

        static bool Open(Bounds b)
        {
            // Five points on top of the bounds; open when most have nothing above them.
            Vector3 c = b.center, e = b.extents * 0.6f;
            float top = b.max.y + 0.05f;
            int open = 0;
            foreach (var o in new[] { Vector2.zero, new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
                if (!Physics.Raycast(new Vector3(c.x + o.x * e.x, top, c.z + o.y * e.z), Vector3.up, 40f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    open++;
            return open >= 3;
        }

        void Update()
        {
            bool raining = Storm.Active || testWet;
            wet = raining ? Mathf.MoveTowards(wet, 1f, Time.deltaTime / wettingTime)
                          : Mathf.MoveTowards(wet, 0f, Time.deltaTime / dryingTime);
            Wetness = wet;
            if (Mathf.Abs(wet - applied) < 0.01f && !(wet == 0f && applied != 0f)) return;
            applied = wet;
            Apply(wet);
        }

        void Apply(float w)
        {
            foreach (var e in entries)
            {
                float porous = 1f - e.smoothness; // rough stone, plaster and gravel soak up the most water
                float dark = 1f - maxDarken * porous * w;
                var c = e.color;
                e.material.SetColor(BaseColor, new Color(c.r * dark, c.g * dark, c.b * dark, c.a));
                e.material.SetFloat(Smoothness, Mathf.Lerp(e.smoothness, Mathf.Max(e.smoothness, wetSmoothness), w));
            }
        }

        void OnDestroy() => Apply(0f); // the shared materials are assets in the editor

        /// <summary>Test hook: SendMessage('WetWeather', 'TestWet'): soak everything at once.</summary>
        public void TestWet() { testWet = true; wet = 1f; }

        /// <summary>Test hook: SendMessage('WetWeather', 'TestDry').</summary>
        public void TestDry() { testWet = false; wet = 0f; }
    }
}
