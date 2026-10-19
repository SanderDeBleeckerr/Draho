using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// One of the warmest, cosiest places in town (a side activity): a soft golden ring that pulses slowly. Sit in it
    /// (stand still for a few seconds, the cat sits down) and the spot is found: a few coins, and it is saved.
    /// The rings make the cat's new sit part of the game and send the player to every corner of every area.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NapSpot : MonoBehaviour
    {
        [SerializeField] string id;

        /// <summary>The save id (the sniff sense looks for ids not yet collected).</summary>
        public string Id => id;
        [SerializeField] string title = "a warm spot";
        [SerializeField] float radius = 0.45f;
        [SerializeField] float sitTime = 4.5f;
        [SerializeField] int coins = 3;

        static int total = -1;
        CatMotor cat;
        LineRenderer ring;
        float still, phase;
        bool found;

        public void Configure(string spotId, string spotTitle)
        {
            id = spotId;
            title = spotTitle;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            ring = GetComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 28;
            ring.widthMultiplier = 0.025f;
            // Sit on whatever is under the spot (a rug, a table top, a roof).
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                transform.position = hit.point;
            found = Progress.Instance != null && Progress.Instance.IsCollected(id);
            if (total < 0) total = FindObjectsByType<NapSpot>(FindObjectsSortMode.None).Length;
            ring.enabled = !found;
            phase = Random.value * 6f;
        }

        void Update()
        {
            if (found || cat == null) return;
            phase += Time.deltaTime;
            float r = radius * (0.92f + 0.08f * Mathf.Sin(phase * 1.6f));
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r));
            }
            var c = new Color(1f, 0.8f, 0.4f, 0.55f + 0.25f * Mathf.Sin(phase * 1.6f));
            ring.startColor = ring.endColor = c;

            Vector3 d = cat.transform.position - transform.position;
            if (!found && new Vector2(d.x, d.z).sqrMagnitude < 2.25f && Mathf.Abs(d.y) < 1f) RingHints.Near("nap"); // within 1.5 m
            bool inside = new Vector2(d.x, d.z).magnitude < radius && Mathf.Abs(d.y) < 0.6f;
            bool sitting = inside && cat.Speed < 0.05f && cat.IsGrounded && !cat.IsCrouching;
            still = sitting ? still + Time.deltaTime : 0f;
            if (still < sitTime) return;
            found = true;
            ring.enabled = false;
            var p = Progress.Instance;
            p?.Collect(id, coins);
            int n = p != null ? p.NapCount : 1;
            ProgressHud.Toast($"A perfect nap spot: {title}. Purr... ({n}/{total})");
            Debug.Log($"STORY nap spot found: {id} ({n}/{total})");
        }

        /// <summary>Test hook: SendMessage('NapSpot_rug', 'TestFind').</summary>
        public void TestFind() => still = sitTime;
    }
}
