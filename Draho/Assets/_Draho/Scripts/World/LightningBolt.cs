using System.Collections.Generic;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A visible bolt with each lightning flash of the storm (research round 66: a flash is several return strokes
    /// about 40-60 ms apart down the same jagged channel, 0.2-0.5 s in all, so it flickers; a branch or two fork
    /// off the main channel). The bolt is made new each time by midpoint displacement in sky angles, drawn far away
    /// round the camera, low and to the side of the view (a low camera sees only the bottom of the sky).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LightningBolt : MonoBehaviour
    {
        [SerializeField] float width = 1.4f;
        [SerializeField] float distance = 140f;

        // Brightness over time (s): stroke, dark, stroke, dark, stroke, fade.
        static readonly Vector2[] Strokes =
        {
            new Vector2(0f, 1f), new Vector2(0.05f, 0.15f), new Vector2(0.09f, 0.9f), new Vector2(0.14f, 0.1f),
            new Vector2(0.19f, 0.7f), new Vector2(0.4f, 0f),
        };

        Mesh mesh;
        MeshRenderer view;
        Material material;
        float t = -1f, hold;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        void Awake()
        {
            mesh = new Mesh { name = "Bolt" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            view = GetComponent<MeshRenderer>();
            gameObject.layer = 0; // made at run time: not a near-only detail
            material = view.material;
            view.enabled = false;
            SkyMood.Flashed += Strike;
        }

        void OnDestroy() => SkyMood.Flashed -= Strike;

        void Update()
        {
            if (t < 0f) return;
            if (hold > 0f) { hold -= Time.deltaTime; material.SetFloat("_Bright", 3f); return; } // test: keep it lit
            t += Time.deltaTime;
            float b = 0f;
            for (int i = 0; i < Strokes.Length - 1; i++)
                if (t >= Strokes[i].x && t < Strokes[i + 1].x)
                    b = Strokes[i].y; // hold each step: a hard flicker
            if (t >= Strokes[Strokes.Length - 2].x) b = Mathf.Lerp(Strokes[Strokes.Length - 2].y, 0f, (t - Strokes[Strokes.Length - 2].x) / (Strokes[Strokes.Length - 1].x - Strokes[Strokes.Length - 2].x));
            material.SetFloat("_Bright", b * 3f);
            if (t >= Strokes[Strokes.Length - 1].x) { t = -1f; view.enabled = false; }
        }

        void Strike()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float az = cam.transform.eulerAngles.y + Random.Range(18f, 40f) * (Random.value < 0.5f ? -1f : 1f);
            verts.Clear(); uvs.Clear(); tris.Clear();
            // The main channel from the cloud base down to below the roofs, in (azimuth, elevation) degrees.
            var main = Jagged(new Vector2(az, 24f), new Vector2(az + Random.Range(-6f, 6f), -2f), 5, 0.22f);
            Ribbon(main, width);
            int fork = Random.Range(main.Count / 4, main.Count / 2);
            Vector2 from = main[fork];
            Ribbon(Jagged(from, from + new Vector2(Random.Range(5f, 9f) * (Random.value < 0.5f ? -1f : 1f), -Random.Range(6f, 10f)), 3, 0.25f), width * 0.55f);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f); // the shader moves it round the camera
            view.enabled = true;
            t = 0f;
            Debug.Log($"SKY bolt at {az:0}°");
        }

        static List<Vector2> Jagged(Vector2 a, Vector2 b, int levels, float rough)
        {
            var pts = new List<Vector2> { a, b };
            for (int level = 0; level < levels; level++)
            {
                var next = new List<Vector2> { pts[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 p = pts[i], q = pts[i + 1], d = q - p;
                    Vector2 perp = new Vector2(-d.y, d.x);
                    next.Add((p + q) * 0.5f + perp * Random.Range(-rough, rough));
                    next.Add(q);
                }
                pts = next;
            }
            return pts;
        }

        void Ribbon(List<Vector2> pts, float w)
        {
            float half = w * 0.5f / distance;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p = Dir(pts[i]), q = Dir(pts[i + 1]);
                Vector3 side = Vector3.Cross(q - p, p).normalized * half;
                int v = verts.Count;
                verts.Add(p - side); verts.Add(p + side); verts.Add(q + side); verts.Add(q - side);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                tris.Add(v); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            }
        }

        static Vector3 Dir(Vector2 azEl) => Quaternion.Euler(-azEl.y, azEl.x, 0f) * Vector3.forward;

        /// <summary>Test hook: SendMessage('LightningBolt', 'TestStrike').</summary>
        public void TestStrike()
        {
            SkyMood.Flash();
            hold = 3f; // slow enough for a screenshot
        }
    }
}
