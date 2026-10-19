using Draho.Core;
using Draho.Story;
using Draho.UI;
using Draho.World;
using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// The cat's nose as a hint (research round 71: a cat's sense of smell is many times better than ours, and it
    /// crouches low to sniff; games hint at hidden things with a sense mode the player starts on purpose, so the
    /// search stays the player's). Crouch and keep still for a moment: a line names the nearest thing not yet
    /// found (feather, nap spot, viewpoint, scratching post, gift) with its distance and direction, and a faint
    /// trail of scent drifts that way.
    /// </summary>
    public class SniffSense : MonoBehaviour
    {
        [SerializeField] float stillTime = 1.5f;
        [SerializeField] float cooldown = 6f;

        CatMotor motor;
        ParticleSystem scent;
        float still, wait;

        void Start()
        {
            motor = GetComponent<CatMotor>();
            var dust = transform.Find("Dust");
            if (dust != null)
            {
                var go = Instantiate(dust.gameObject, transform);
                go.name = "Scent";
                scent = go.GetComponent<ParticleSystem>();
                var main = scent.main;
                main.startColor = new Color(0.95f, 0.92f, 0.6f, 0.5f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
                main.gravityModifier = 0f;
                scent.Play(); // emission stays off: only Emit()
            }
        }

        void Update()
        {
            if (motor == null) return;
            wait -= Time.deltaTime;
            bool sniffing = motor.IsCrouching && motor.Speed < 0.05f && motor.IsGrounded;
            still = sniffing ? still + Time.deltaTime : 0f;
            if (still < stillTime || wait > 0f) return;
            wait = cooldown;
            Sniff();
        }

        void Sniff()
        {
            Vector3 me = transform.position;
            var p = Progress.Instance;
            string kind = null;
            Vector3 best = Vector3.zero;
            float bestDist = float.MaxValue;
            void Consider(string id, Vector3 pos, string what)
            {
                if (string.IsNullOrEmpty(id) || (p != null && p.IsCollected(id))) return;
                float d = Vector3.Distance(me, pos);
                if (d < bestDist) { bestDist = d; best = pos; kind = what; }
            }
            foreach (var c in FindObjectsByType<Collectable>(FindObjectsSortMode.None)) Consider(c.Id, c.transform.position, "a feather");
            foreach (var n in FindObjectsByType<NapSpot>(FindObjectsSortMode.None)) Consider(n.Id, n.transform.position, "a warm nap spot");
            foreach (var v in FindObjectsByType<Viewpoint>(FindObjectsSortMode.None)) Consider(v.Id, v.transform.position, "a high viewpoint");
            foreach (var s in FindObjectsByType<ScratchSpot>(FindObjectsSortMode.None)) Consider(s.Id, s.transform.position, "a good scratching place");
            if (StoryDirector.InEpilogue)
                foreach (var g in FindObjectsByType<Gift>(FindObjectsSortMode.None))
                    if (!g.Carried) Consider(g.Id, g.transform.position, "something for your humans");
            if (kind == null)
            {
                ProgressHud.Toast("Sniff... nothing new. You have found everything!");
                return;
            }
            Vector3 to = best - me;
            string where = Direction(to);
            ProgressHud.Toast($"Sniff, sniff... {kind}, {bestDist:0} m {where}.");
            Debug.Log($"STORY sniff: {kind} {bestDist:0.0} m {where}");
            if (scent != null)
            {
                Vector3 flat = new Vector3(to.x, 0f, to.z).normalized;
                for (int i = 0; i < 10; i++)
                {
                    var e = new ParticleSystem.EmitParams
                    {
                        position = me + Vector3.up * 0.15f + flat * (0.2f + i * 0.12f),
                        velocity = flat * Random.Range(0.25f, 0.45f) + Vector3.up * 0.05f,
                        applyShapeToPosition = false,
                    };
                    scent.Emit(e, 1);
                }
            }
        }

        static string Direction(Vector3 to)
        {
            var cam = Camera.main;
            Vector3 view = cam != null ? cam.transform.forward : Vector3.forward;
            view.y = 0f;
            float a = Vector3.SignedAngle(view, new Vector3(to.x, 0f, to.z), Vector3.up);
            string side = a < 0f ? "left" : "right";
            float abs = Mathf.Abs(a);
            string dir = abs < 30f ? "ahead" : abs < 75f ? $"ahead to the {side}" : abs < 120f ? $"to your {side}" : abs < 150f ? $"behind, to the {side}" : "behind you";
            if (to.y > 2.5f) dir += ", up high";
            else if (to.y < -2.5f) dir += ", down below";
            return dir;
        }

        /// <summary>Test hook: SendMessage('Drahomil', 'TestSniff').</summary>
        public void TestSniff() => Sniff();
    }
}
