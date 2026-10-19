using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Now and then a shooting star on a clear night (research round 64: a meteor lasts well under a second, a short
    /// bright streak that fades from the head back along its tail; a few per hour for a real watcher, more here so a
    /// player sees some). Drawn far away round the camera with the Draho/Meteor shader. A cat that sits still and
    /// sees one "makes a wish" the first time.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ShootingStars : MonoBehaviour
    {
        [SerializeField] Vector2 interval = new Vector2(15f, 40f);
        [SerializeField] float duration = 0.7f;

        static readonly int StartId = Shader.PropertyToID("_Start");
        static readonly int EndId = Shader.PropertyToID("_End");
        static readonly int TId = Shader.PropertyToID("_T");
        static readonly int BrightId = Shader.PropertyToID("_Bright");

        Material material;
        MeshRenderer view;
        CatMotor cat;
        float wait, t = -1f;
        bool wished;

        void Awake()
        {
            var mesh = new Mesh { name = "Meteor" };
            mesh.vertices = new[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f); // the shader moves it round the camera
            GetComponent<MeshFilter>().sharedMesh = mesh;
            view = GetComponent<MeshRenderer>();
            gameObject.layer = 0; // the builder saw no mesh (made here) and put it on the near-only Detail layer
            material = view.material;
            view.enabled = false;
            wait = Random.Range(interval.x * 0.3f, interval.y * 0.5f);
        }

        void Start() => cat = FindAnyObjectByType<CatMotor>();

        void Update()
        {
            if (t >= 0f)
            {
                t += Time.deltaTime / duration;
                float bright = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 4f;
                material.SetFloat(TId, Mathf.Min(t, 1f));
                material.SetFloat(BrightId, bright);
                if (t >= 1f) { t = -1f; view.enabled = false; }
                return;
            }
            bool clear = SkyMood.NightAmount > 0.8f && !Storm.Active;
            if (!clear) return;
            wait -= Time.deltaTime;
            if (wait > 0f) return;
            wait = Random.Range(interval.x, interval.y);
            // Usually somewhere ahead of the camera (a low camera rarely shows the sky behind or straight up).
            var cam = Camera.main;
            Fire(cam != null ? cam.transform.eulerAngles.y + Random.Range(20f, 45f) * (Random.value < 0.5f ? -1f : 1f) : Random.value * 360f);
        }

        void Fire(float az)
        {
            // Above the roofs and to the side of the story panel (a low camera sees 20-35 degrees up), falling at a slant over 15-25 degrees of sky.
            float el = Random.Range(20f, 32f);
            Vector3 start = Quaternion.Euler(-el, az, 0f) * Vector3.forward;
            Vector3 end = Quaternion.Euler(-(el - Random.Range(5f, 10f)), az + Random.Range(12f, 22f) * (Random.value < 0.5f ? -1f : 1f), 0f) * Vector3.forward;
            material.SetVector(StartId, start);
            material.SetVector(EndId, end);
            material.SetFloat(TId, 0f);
            material.SetFloat(BrightId, 0f);
            t = 0f;
            view.enabled = true;
            Debug.Log("STORY shooting star");
            if (!wished && cat != null && cat.Speed < 0.05f && cat.IsGrounded)
            {
                wished = true;
                ProgressHud.Toast("A shooting star! Drahomil makes a wish. (Tuna. It is always tuna.)");
            }
        }

        /// <summary>Test hook: SendMessage('ShootingStars', 'TestFire').</summary>
        public void TestFire()
        {
            var cam = Camera.main;
            duration = 2.5f; // slow, so a screenshot catches it
            Fire(cam != null ? cam.transform.eulerAngles.y + 35f : 0f);
        }
    }
}
