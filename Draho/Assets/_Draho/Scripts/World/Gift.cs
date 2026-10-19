using Draho.Cat;
using Draho.Core;
using Draho.Story;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A gift for the humans in the epilogue (research round 62: cats bring their people "gifts", most often small
    /// prey, also toys and odd finds; a cat that lives indoors brings what it can find). Touch it to pick it up in
    /// your mouth (one at a time), carry it home to the front door, and the humans react. Saved like a feather.
    /// </summary>
    public class Gift : MonoBehaviour
    {
        [SerializeField] string id;

        /// <summary>The save id (the sniff sense looks for ids not yet collected).</summary>
        public string Id => id;
        [SerializeField] string title = "a gift";
        [SerializeField] string reaction = "\"For us? Thank you, Drahomil!\"";
        [SerializeField] int coins = 8;
        [SerializeField] float pickRadius = 0.4f;
        [SerializeField] float deliverRadius = 1.1f;

        static Gift carried;
        public bool Carried => carried == this;
        CatMotor cat;
        Transform head, door;
        Renderer[] renderers;
        Vector3 home;
        Quaternion homeRotation;
        bool done, shown;
        float phase;

        public void Configure(string giftId, string giftTitle, string humanReaction)
        {
            id = giftId;
            title = giftTitle;
            reaction = humanReaction;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            if (cat != null)
                foreach (var t in cat.GetComponentsInChildren<Transform>())
                    if (t.name == "Head") { head = t; break; }
            var humans = FindAnyObjectByType<Humans>();
            if (humans != null) door = humans.transform.childCount > 0 ? humans.transform.GetChild(0) : humans.transform; // a person at the door
            renderers = GetComponentsInChildren<Renderer>();
            // Lie on whatever is under the spot.
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                transform.position = hit.point + Vector3.up * 0.02f;
            home = transform.position;
            homeRotation = transform.rotation;
            done = Progress.Instance != null && Progress.Instance.IsCollected(id);
            Show(false);
            if (carried == this) carried = null;
        }

        void Show(bool on)
        {
            if (shown == on) return;
            shown = on;
            foreach (var r in renderers) r.enabled = on;
        }

        void Update()
        {
            if (done || cat == null || !StoryDirector.InEpilogue) { Show(false); return; }
            Show(true);
            phase += Time.deltaTime;
            if (carried != this)
            {
                // A slow turn on the spot, so it catches the light.
                transform.rotation = homeRotation * Quaternion.Euler(0f, phase * 40f, 0f);
                if (carried == null && (cat.transform.position - transform.position).sqrMagnitude < pickRadius * pickRadius)
                    PickUp();
                return;
            }
            if (door != null)
            {
                Vector3 d = cat.transform.position - door.position;
                if (new Vector2(d.x, d.z).magnitude < deliverRadius && Mathf.Abs(d.y) < 1.2f) Deliver();
            }
        }

        void PickUp()
        {
            carried = this;
            var anchor = head != null ? head : cat.transform;
            transform.SetParent(anchor, true);
            transform.position = cat.transform.position + cat.transform.forward * 0.17f + Vector3.up * 0.15f;
            ProgressHud.Toast($"You pick up {title}. Carry it home to your humans at the front door.");
            Debug.Log($"STORY gift picked up: {id}");
        }

        void Deliver()
        {
            carried = null;
            done = true;
            transform.SetParent(null, true);
            Show(false);
            var p = Progress.Instance;
            p?.Collect(id, coins);
            int n = p != null ? p.GiftCount : 1;
            ProgressHud.Toast($"You drop {title} at their feet. {reaction} ({n}/6)");
            Debug.Log($"STORY gift delivered: {id} ({n}/6)");
        }

        /// <summary>Test hook: SendMessage('Gift_sock', 'TestPick'): pick it up wherever the cat is.</summary>
        public void TestPick()
        {
            if (carried == null && !done) PickUp();
        }
    }
}
