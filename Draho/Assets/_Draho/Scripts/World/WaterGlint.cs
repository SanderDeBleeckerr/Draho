using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The glitter path of a lamp across the river at night. On rippled water each small facet reflects the light at
    /// a slightly different angle, so a point of light becomes a long shimmering streak that points at the viewer
    /// (research round 53). A flat strip on the water from the far bank towards the camera, with a dashed texture
    /// that scrolls and a gentle flicker; additive, on only when the lamps are on.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterGlint : MonoBehaviour
    {
        [SerializeField] Light lamp;
        [SerializeField] float maxLength = 13f;
        [SerializeField] float width = 0.45f;
        [SerializeField] Color tint = new Color(1f, 0.72f, 0.4f);

        static Mesh strip;
        static Texture2D dashes;
        Material material;
        Transform cam;
        float phase;

        public void Configure(Light lampLight) => lamp = lampLight;

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = strip != null ? strip : (strip = MakeStrip());
            var r = GetComponent<MeshRenderer>();
            material = new Material(r.sharedMaterial);
            if (dashes == null) dashes = MakeDashes();
            material.SetTexture("_BaseMap", dashes);
            r.sharedMaterial = material;
            phase = Random.value * 10f;
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            var r = GetComponent<MeshRenderer>();
            float on = lamp != null && lamp.enabled ? Mathf.Clamp01(lamp.intensity / 3f) : 0f;
            r.enabled = on > 0.02f && cam != null;
            if (!r.enabled) return;
            phase += Time.deltaTime;
            // Lie flat on the water and point at the camera.
            Vector3 toCam = cam.position - transform.position;
            toCam.y = 0f;
            float length = Mathf.Min(maxLength, toCam.magnitude * 0.85f);
            if (length < 0.5f) { r.enabled = false; return; }
            transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            transform.localScale = new Vector3(width * (1f + 0.15f * Mathf.Sin(phase * 1.7f)), 1f, length);
            material.SetTextureOffset("_BaseMap", new Vector2(0f, -phase * 0.35f));
            material.SetTextureScale("_BaseMap", new Vector2(1f, length / 2f));
            float flicker = 0.8f + 0.2f * Mathf.Sin(phase * 5.3f) * Mathf.Sin(phase * 2.1f);
            material.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, 0.55f * on * flicker));
        }

        static Mesh MakeStrip()
        {
            // A flat strip from z = 0 to 1, x from -0.5 to 0.5, facing up; v runs along the strip.
            var m = new Mesh { name = "GlintStrip" };
            m.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0f, 1f), new Vector3(-0.5f, 0f, 1f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(1f, 0.1f, 1f));
            return m;
        }

        static Texture2D MakeDashes()
        {
            // Bright dashes across the strip (the ripple facets), soft at the sides.
            const int w = 16, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            var rng = new System.Random(53);
            for (int y = 0; y < h; y++)
            {
                float row = rng.NextDouble() < 0.45 ? 0.15f + 0.85f * (float)rng.NextDouble() : 0.05f;
                int left = rng.Next(0, 5), right = w - rng.Next(0, 5);
                for (int x = 0; x < w; x++)
                {
                    float side = Mathf.Sin(Mathf.PI * (x + 0.5f) / w);
                    float a = x >= left && x < right ? row * side : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
