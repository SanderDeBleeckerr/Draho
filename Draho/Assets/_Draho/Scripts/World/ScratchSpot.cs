using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A place to scratch (research round 70: cats scratch to keep their claws in shape and to mark territory, with
    /// scent from glands in the paws and with visible marks; they like rough, upright things they can stretch up
    /// against, often near the paths they use). A small tan ring at the foot of a post, a leg or a door frame;
    /// rear up in it (jump without moving) and three claw marks appear. Saved like a feather ("scratch_...").
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ScratchSpot : MonoBehaviour
    {
        [SerializeField] string id;

        /// <summary>The save id (the sniff sense looks for ids not yet collected).</summary>
        public string Id => id;
        [SerializeField] string title = "a post";
        [SerializeField] float radius = 0.32f;
        [SerializeField] int coins = 4;
        [SerializeField] Material markMaterial;

        static int total = -1;
        CatMotor cat;
        LineRenderer ring;
        float rearing, phase;
        bool found, test;

        public void Configure(string spotId, string spotTitle, Material marks)
        {
            id = spotId;
            title = spotTitle;
            markMaterial = marks;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            ring = GetComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 20;
            ring.widthMultiplier = 0.018f;
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                transform.position = hit.point;
            found = Progress.Instance != null && Progress.Instance.IsCollected(id);
            if (total < 0) total = FindObjectsByType<ScratchSpot>(FindObjectsSortMode.None).Length;
            ring.enabled = !found;
            phase = Random.value * 6f;
        }

        void Update()
        {
            if (found || cat == null) return;
            phase += Time.deltaTime;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius));
            }
            ring.startColor = ring.endColor = new Color(0.85f, 0.65f, 0.4f, 0.45f + 0.15f * Mathf.Sin(phase * 1.4f));

            Vector3 d = cat.transform.position - transform.position;
            bool inside = new Vector2(d.x, d.z).magnitude < radius + 0.15f && Mathf.Abs(d.y) < 0.5f;
            rearing = inside && cat.IsRearing ? rearing + Time.deltaTime : 0f;
            if (rearing < 0.2f && !test) return; // short: rearing at something climbable turns into a climb after 0.25 s
            found = true;
            ring.enabled = false;
            Marks();
            var p = Progress.Instance;
            p?.Collect(id, coins);
            int n = p != null ? p.ScratchCount : 1;
            ProgressHud.Toast($"Scritch-scratch: {title}. Your mark is on it now. ({n}/{total})");
            Debug.Log($"STORY scratched: {id} ({n}/{total})");
        }

        void Marks()
        {
            if (markMaterial == null) return;
            // Three thin claw lines on the surface in front of the cat (none when there is only air).
            Vector3 f = cat.transform.forward;
            Vector3 from = cat.transform.position + Vector3.up * 0.38f;
            if (!Physics.Raycast(from, f, out RaycastHit hit, 0.7f, ~(1 << 2), QueryTriggerInteraction.Ignore)) { Debug.Log("STATE claw marks: nothing in front"); return; }
            Debug.Log($"STATE claw marks on {hit.collider.name} at {hit.distance:0.00} m");
            Vector3 basePos = hit.point + hit.normal * 0.004f;
            var rot = Quaternion.LookRotation(-hit.normal, Vector3.up);
            Vector3 along = Vector3.Cross(hit.normal, Vector3.up).normalized; // sideways on the surface
            for (int i = 0; i < 3; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(m.GetComponent<Collider>());
                m.name = "ClawMark";
                m.transform.SetParent(transform, true);
                m.transform.SetPositionAndRotation(basePos + along * ((i - 1) * 0.025f) + Vector3.up * (i == 1 ? 0.01f : 0f), rot * Quaternion.Euler(0f, 0f, 6f));
                m.transform.localScale = new Vector3(0.006f, 0.16f, 0.004f);
                m.GetComponent<Renderer>().sharedMaterial = markMaterial;
            }
        }

        /// <summary>Test hook: SendMessage('Scratch_sisal', 'TestFind').</summary>
        public void TestFind() => test = true;
    }
}
