using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A rainbow in the morning after the storm (research round 89: the primary bow is a circle of about 42° round
    /// the antisolar point, red outside and violet inside, about 2° wide; a fainter secondary bow at about 51° has the
    /// colours reversed; with the sun low, the bow stands high). Drawn far away round the camera with Draho/Rainbow;
    /// rebuilt when the sun moves; only at dawn, fading in and out.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Rainbow : MonoBehaviour
    {
        const int Segments = 72;

        Light sun;
        Mesh mesh;
        Material material;
        MeshRenderer view;
        Vector3 lastAxis;
        float shown;

        void Awake()
        {
            view = GetComponent<MeshRenderer>();
            gameObject.layer = 0; // made at run time: not a near-only detail
            material = view.material;
            mesh = new Mesh { name = "Rainbow" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            view.enabled = false;
        }

        void Start()
        {
            var go = GameObject.Find("Sun");
            if (go != null) sun = go.GetComponent<Light>();
        }

        void Update()
        {
            if (sun == null) return;
            bool want = SkyMood.Current == SkyMood.Mood.Dawn && !Storm.Active;
            shown = Mathf.MoveTowards(shown, want ? 1f : 0f, Time.deltaTime / 6f);
            view.enabled = shown > 0.01f;
            if (!view.enabled) return;
            material.SetFloat("_Bright", 0.55f * shown);
            Vector3 axis = sun.transform.forward; // the light travels towards the antisolar point
            if (Vector3.Angle(axis, lastAxis) > 0.5f) Build(axis);
        }

        void Build(Vector3 axis)
        {
            lastAxis = axis;
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            void Band(float inner, float outer, float secondary)
            {
                Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                int start = verts.Count;
                for (int i = 0; i <= Segments; i++)
                {
                    var spin = Quaternion.AngleAxis(i * 360f / Segments, axis);
                    verts.Add(spin * (Quaternion.AngleAxis(inner, side) * axis));
                    verts.Add(spin * (Quaternion.AngleAxis(outer, side) * axis));
                    uvs.Add(new Vector2(secondary, 0f));
                    uvs.Add(new Vector2(secondary, 1f));
                }
                for (int i = 0; i < Segments; i++)
                {
                    int a = start + i * 2;
                    tris.AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
                }
            }
            Band(40.6f, 42.4f, 0f);   // primary
            Band(50.2f, 53.4f, 1f);   // secondary
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f); // the shader moves it round the camera
        }

        /// <summary>Test hook: SendMessage('Rainbow', 'TestShow'): dawn now.</summary>
        public void TestShow() => SkyMood.Set(SkyMood.Mood.Dawn, 0.5f);
    }
}
