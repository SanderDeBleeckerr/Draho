using Draho.CameraRig;
using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A viewpoint on a high roof (research round 63: open-world "vista points" reward a climb with a slow camera move
    /// over the view, the way synchronisation points do; they also teach the map). A faint blue ring; stand still
    /// in it for a moment and the camera pulls back and turns slowly round the cat while a line names the view.
    /// Saved once found ("view_..."); the ring stays faint after, and the sweep can be watched again.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class Viewpoint : MonoBehaviour
    {
        [SerializeField] string id;
        [SerializeField] string view = "the town";
        [SerializeField] float radius = 0.55f;
        [SerializeField] float stillTime = 1.5f;
        [SerializeField] float sweepTime = 10f;
        [SerializeField] int coins = 6;

        static int total = -1;
        CatMotor cat;
        OrbitCamera orbit;
        LineRenderer ring;
        float still, phase, cooldown;
        bool found;

        public void Configure(string viewId, string description)
        {
            id = viewId;
            view = description;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            orbit = FindAnyObjectByType<OrbitCamera>();
            ring = GetComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 28;
            ring.widthMultiplier = 0.025f;
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                transform.position = hit.point;
            found = Progress.Instance != null && Progress.Instance.IsCollected(id);
            if (total < 0) total = FindObjectsByType<Viewpoint>(FindObjectsSortMode.None).Length;
            phase = Random.value * 6f;
        }

        void Update()
        {
            if (cat == null) return;
            phase += Time.deltaTime;
            cooldown -= Time.deltaTime;
            float r = radius * (0.95f + 0.05f * Mathf.Sin(phase * 1.2f));
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r));
            }
            float alpha = found ? 0.2f : 0.5f + 0.2f * Mathf.Sin(phase * 1.2f);
            ring.startColor = ring.endColor = new Color(0.55f, 0.8f, 1f, alpha);

            Vector3 d = cat.transform.position - transform.position;
            bool inside = new Vector2(d.x, d.z).magnitude < radius && Mathf.Abs(d.y) < 0.6f;
            still = inside && cat.Speed < 0.05f && cat.IsGrounded ? still + Time.deltaTime : 0f;
            if (still < stillTime || cooldown > 0f) return;
            still = 0f;
            cooldown = sweepTime + 20f; // once per visit
            orbit?.Sweep(sweepTime);
            if (!found)
            {
                found = true;
                var p = Progress.Instance;
                p?.Collect(id, coins);
                int n = p != null ? p.ViewCount : 1;
                ProgressHud.Toast($"Viewpoint: {view} ({n}/{total})");
                Debug.Log($"STORY viewpoint found: {id} ({n}/{total})");
            }
            else ProgressHud.Toast($"{view}");
        }

        /// <summary>Test hook: SendMessage('Viewpoint_ridge', 'TestFind').</summary>
        public void TestFind() => still = stillTime;
    }
}
