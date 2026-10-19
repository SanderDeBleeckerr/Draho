using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The soft cone of light under a lamp at night, the way lamplight shows in damp air and in the rain. A fake
    /// volume (research round 58, the same trick as the sunbeam): an open cone of additive glow, bright at the lamp
    /// and fading to nothing at the ground, with the edges faded where the surface turns away from the camera
    /// (a "fresnel" fade per vertex) so there is no hard outline. Brighter in the storm, off by day.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LampCone : MonoBehaviour
    {
        const int Segments = 16;
        static readonly float[] Rings = { 0f, 0.3f, 1f };   // along the cone, 0 = the lamp
        static readonly float[] Fade = { 1f, 0.6f, 0f };   // brightness at those rings

        [SerializeField] Light lamp;
        [SerializeField] float height = 4f;
        [SerializeField] float radius = 1.6f;
        [SerializeField] float strength = 0.24f;
        [SerializeField] float rainBoost = 1.8f;
        [SerializeField] Color tint = new Color(1f, 0.75f, 0.45f);

        Mesh mesh;
        Vector3[] normals;
        Color[] colors;
        MeshRenderer view;
        Transform cam;
        float rain;

        public void Configure(Light lampLight, float coneHeight, float coneRadius)
        {
            lamp = lampLight;
            height = coneHeight;
            radius = coneRadius;
        }

        void Awake()
        {
            view = GetComponent<MeshRenderer>();
            gameObject.layer = 0; // the builder saw no mesh (made here) and put it on the near-only Detail layer
            mesh = new Mesh { name = "LampCone" };
            int n = Segments + 1;
            var verts = new Vector3[n * Rings.Length];
            normals = new Vector3[verts.Length];
            colors = new Color[verts.Length];
            float slope = radius / height;
            for (int r = 0; r < Rings.Length; r++)
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float t = Rings[r];
                    verts[r * n + i] = dir * (0.06f + radius * t) + Vector3.down * (height * t);
                    normals[r * n + i] = (dir + Vector3.up * slope).normalized;
                }
            var tris = new int[(Rings.Length - 1) * Segments * 6];
            for (int r = 0, k = 0; r < Rings.Length - 1; r++)
                for (int i = 0; i < Segments; i++)
                {
                    int a = r * n + i, b = a + 1, c = a + n, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            view.enabled = false;
        }

        void LateUpdate()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            float on = lamp != null && lamp.enabled ? Mathf.Clamp01(lamp.intensity / 3f) : 0f;
            rain = Mathf.MoveTowards(rain, Storm.Active ? 1f : 0f, Time.deltaTime / 4f);
            view.enabled = on > 0.02f && cam != null;
            if (!view.enabled) return;
            float a = strength * on * Mathf.Lerp(1f, rainBoost, rain);
            Vector3 toCam = transform.InverseTransformPoint(cam.position);
            int n = Segments + 1;
            for (int r = 0; r < Rings.Length; r++)
            {
                Vector3 centre = Vector3.down * (height * Rings[r]);
                Vector3 look = (toCam - centre).normalized;
                for (int i = 0; i < n; i++)
                {
                    // Facing the camera: full glow; seen edge-on: none (a soft silhouette).
                    float facing = Mathf.Abs(Vector3.Dot(normals[r * n + i], look));
                    colors[r * n + i] = new Color(tint.r, tint.g, tint.b, a * Fade[r] * facing * facing);
                }
            }
            mesh.colors = colors;
        }
    }
}
