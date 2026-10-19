using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Swifts over the roofs (research round 81: common swifts, Apus apus, spend the Belgian summer in the air over
    /// towns; on warm evenings they race in tight "screaming parties" low round the roofs and climb high at dusk;
    /// slim sickle wings, fast flickering beats between glides). Ten dark boomerang shapes on smooth noise paths
    /// round the block, by day and evening; none at night or in the storm.
    /// </summary>
    public class Swifts : MonoBehaviour
    {
        [SerializeField] int count = 10;
        [SerializeField] Vector3 centre = new Vector3(-4f, 0f, -2f);
        [SerializeField] float range = 26f;
        [SerializeField] Material material;

        Transform[] birds;
        float[] seeds;
        Vector3[] last;
        static Mesh shape;

        /// <summary>True while the flock races low over the roofs (their screams are heard then).</summary>
        public static bool PartyLow { get; private set; }

        public void Configure(Material dark) => material = dark;

        void Start()
        {
            if (shape == null) shape = Boomerang();
            birds = new Transform[count];
            seeds = new float[count];
            last = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Swift_{i}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = shape;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                birds[i] = go.transform;
                seeds[i] = i * 7.3f + Random.value;
                last[i] = Position(i, 0f);
            }
        }

        Vector3 Position(int i, float t)
        {
            // A wide loop round the block, with a "party" phase where the group drops low and tight.
            float s = seeds[i];
            float party = Mathf.Clamp01(Mathf.Sin(t * 0.05f) * 2f); // now and then the whole group comes down
            float a = t * (0.32f + 0.04f * Mathf.Sin(s)) + s * 0.6f * (1f - 0.7f * party);
            float r = range * (0.55f + 0.45f * Mathf.PerlinNoise(s, t * 0.07f)) * (1f - 0.45f * party);
            float y = Mathf.Lerp(24f + 10f * Mathf.PerlinNoise(s + 3f, t * 0.1f), 13f + 2f * Mathf.Sin(t * 2f + s), party);
            return centre + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }

        void Update()
        {
            bool flying = SkyMood.NightAmount < 0.5f && !Storm.Active;
            PartyLow = flying && Mathf.Sin(Time.time * 0.05f) > 0.3f;
            for (int i = 0; i < birds.Length; i++)
            {
                birds[i].gameObject.SetActive(flying);
                if (!flying) continue;
                Vector3 p = Position(i, Time.time);
                Vector3 v = p - last[i];
                last[i] = p;
                birds[i].position = p;
                if (v.sqrMagnitude > 1e-6f) birds[i].rotation = Quaternion.LookRotation(v, Vector3.up);
                // Flicker the wings in bursts, then glide.
                float beat = Mathf.Sin(Time.time * 1.3f + seeds[i]) > 0.2f ? Mathf.Sin(Time.time * 40f + seeds[i]) : 0f;
                birds[i].localScale = new Vector3(1.5f, 1.5f + 0.9f * beat, 1.5f); // a little larger than life: they are far away
            }
        }

        static Mesh Boomerang()
        {
            // Two slim swept-back wings (0.42 m span) and a short body, flat, +Z forward.
            var m = new Mesh { name = "Swift" };
            m.vertices = new[]
            {
                new Vector3(0f, 0f, 0.09f), new Vector3(0f, 0f, -0.07f),                      // body nose, tail
                new Vector3(-0.21f, 0f, -0.1f), new Vector3(-0.05f, 0f, 0.02f),               // left wing tip, root
                new Vector3(0.21f, 0f, -0.1f), new Vector3(0.05f, 0f, 0.02f),                 // right wing tip, root
            };
            // Each triangle both ways round: seen from below as well as from above.
            int[] one = { 0, 3, 1, 0, 1, 5, 3, 2, 1, 5, 1, 4 };
            var both = new int[one.Length * 2];
            for (int i = 0; i < one.Length; i += 3)
            {
                both[i] = one[i]; both[i + 1] = one[i + 1]; both[i + 2] = one[i + 2];
                both[one.Length + i] = one[i]; both[one.Length + i + 1] = one[i + 2]; both[one.Length + i + 2] = one[i + 1];
            }
            m.triangles = both;
            // Both sides share the vertices, so computed normals would cancel to zero: point them up (a silhouette).
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            return m;
        }
    }
}
