using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A shaft of low sunlight through the sliding glass door, with dust motes floating in it: a cheap fake volume
    /// (research round 49: a transparent mesh fitted to the shaft, brighter at the glass and fading towards the far
    /// end). The shaft is rebuilt from the real sun direction, cut off at the floor or the far wall of the room, and
    /// only shows when the sun shines in through the door (the morning sun after the night).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SunBeam : MonoBehaviour
    {
        [Tooltip("The opening, in world space: bottom-left, top-left, top-right, bottom-right.")]
        [SerializeField] Vector3[] window = new Vector3[4];
        [Tooltip("The far wall of the room (world x).")]
        [SerializeField] float endX = 4.4f;
        [SerializeField] Color tint = new Color(1f, 0.78f, 0.5f);
        [SerializeField] float strength = 0.55f;
        [SerializeField] ParticleSystem motes;

        Light sun;
        Mesh mesh;
        MeshRenderer view;
        Vector3 lastDir;
        float lastAmount = -1f;

        public void Configure(Vector3[] opening, float farWallX, ParticleSystem dust)
        {
            window = opening;
            endX = farWallX;
            motes = dust;
        }

        void Awake()
        {
            var sunGo = GameObject.Find("Sun");
            if (sunGo != null) sun = sunGo.GetComponent<Light>();
            mesh = new Mesh { name = "SunBeam" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            view = GetComponent<MeshRenderer>();
            view.enabled = false;
        }

        void Update()
        {
            if (sun == null || window == null || window.Length != 4) return;
            Vector3 dir = sun.transform.forward;
            // In through the door (the light travels +x), by day or dawn, as bright as the sun.
            float amount = Mathf.Clamp01((dir.x - 0.15f) / 0.3f) * Mathf.Clamp01((0.6f - SkyMood.NightAmount) / 0.3f)
                           * Mathf.Clamp01(sun.intensity) * (dir.y < -0.02f ? 1f : 0f);
            if (motes != null)
            {
                var emission = motes.emission;
                emission.rateOverTime = amount > 0.05f ? 6f : 0f;
            }
            if (Mathf.Abs(amount - lastAmount) < 0.01f && Vector3.Angle(dir, lastDir) < 0.5f) return;
            lastAmount = amount;
            lastDir = dir;
            view.enabled = amount > 0.01f;
            if (!view.enabled) return;
            Rebuild(dir, amount);
        }

        void Rebuild(Vector3 dir, float amount)
        {
            var verts = new Vector3[8];
            var colors = new Color[8];
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = window[i];
                float toFloor = c.y / -dir.y;
                float toWall = (endX - c.x) / dir.x;
                Vector3 end = c + dir * Mathf.Max(0.01f, Mathf.Min(toFloor, toWall));
                verts[i] = transform.InverseTransformPoint(c);
                verts[i + 4] = transform.InverseTransformPoint(end);
                colors[i] = new Color(tint.r, tint.g, tint.b, strength * amount);         // bright at the glass
                colors[i + 4] = new Color(tint.r, tint.g, tint.b, strength * amount * 0.15f); // fading inside
            }
            // Four sides of the shaft (the material draws both faces).
            var tris = new int[24];
            for (int i = 0, t = 0; i < 4; i++)
            {
                int a = i, b = (i + 1) % 4, ea = a + 4, eb = b + 4;
                tris[t++] = a; tris[t++] = b; tris[t++] = eb;
                tris[t++] = a; tris[t++] = eb; tris[t++] = ea;
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
