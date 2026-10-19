using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Lasting rewards for three more full sets (like WellRested, research round 56: a full set should change the
    /// game, not only a number). Every scratching place: sharp claws, climbing 20% faster. Every viewpoint:
    /// sure-footed, the storm wind pushes only half as hard. Every gift: the humans buy a red collar with a little
    /// bell, and the cat wears it from then on (it jingles).
    /// </summary>
    public class SetRewards : MonoBehaviour
    {
        [SerializeField] int scratchTotal = 8;
        [SerializeField] int giftTotal = 6;
        [SerializeField] int viewTotal = 5;
        [SerializeField] Material collarMat;
        [SerializeField] Material bellMat;

        CatMotor cat;
        bool claws, collar, sure;
        AudioSource bell;
        AudioClip jingle;
        bool wasAirborne;
        float stepTimer;

        public void Configure(int scratches, int gifts, Material collarMaterial, Material bellMaterial)
        {
            scratchTotal = scratches;
            giftTotal = gifts;
            collarMat = collarMaterial;
            bellMat = bellMaterial;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            if (Progress.Instance != null) Progress.Instance.Changed += Check;
            Check();
        }

        void OnDestroy()
        {
            if (Progress.Instance != null) Progress.Instance.Changed -= Check;
        }

        void Check()
        {
            var p = Progress.Instance;
            if (cat == null || p == null) return;
            bool loading = Time.timeSinceLevelLoad < 1f;
            if (!claws && p.ScratchCount >= scratchTotal)
            {
                claws = true;
                cat.ClimbBoost = 1.2f;
                if (!loading) ProgressHud.Toast("Every scratching place in town is yours. Sharp claws: you climb faster now.");
                Debug.Log("STORY sharp claws (all scratching places)");
            }
            if (!sure && p.ViewCount >= viewTotal)
            {
                sure = true;
                cat.WindFactor = 0.5f;
                if (!loading) ProgressHud.Toast("Every viewpoint seen: you know the high roofs now. Sure-footed: the wind pushes you only half as hard.");
                Debug.Log("STORY sure-footed (all viewpoints)");
            }
            if (!collar && p.GiftCount >= giftTotal)
            {
                collar = true;
                WearCollar();
                if (!loading) ProgressHud.Toast("Six gifts! Your humans laugh and buy you something back: a red collar with a little bell. Ting!");
                Debug.Log("STORY the collar (all gifts)");
            }
        }

        void WearCollar()
        {
            Transform neck = null;
            foreach (var t in cat.GetComponentsInChildren<Transform>())
                if (t.name == "Neck") { neck = t; break; }
            if (neck == null || collarMat == null) return;
            // A flat red ring round the neck and a small gold bell under the chin, kept a fixed size in world space.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "Collar";
            ring.transform.SetParent(neck, false);
            ring.GetComponent<Renderer>().sharedMaterial = collarMat;
            SetWorldSize(ring.transform, new Vector3(0.12f, 0.012f, 0.12f)); // wider than the neck, or it hides inside
            ring.transform.rotation = Quaternion.LookRotation(cat.transform.up, cat.transform.forward); // ring round the neck
            ring.transform.position = neck.position;
            var bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bell.GetComponent<Collider>());
            bell.name = "Bell";
            bell.transform.SetParent(neck, false);
            bell.GetComponent<Renderer>().sharedMaterial = bellMat != null ? bellMat : collarMat;
            SetWorldSize(bell.transform, Vector3.one * 0.022f);
            bell.transform.position = neck.position + cat.transform.forward * 0.06f - cat.transform.up * 0.045f;
            var r = ring.GetComponent<Renderer>().bounds;
            Debug.Log($"STATE collar at {ring.transform.position} (cat {cat.transform.position}), size {r.size}, neck scale {neck.lossyScale}");
        }


        void Update()
        {
            // The bell jingles on jumps and landings, and now and then while walking.
            if (!collar || cat == null) return;
            if (bell == null)
            {
                bell = gameObject.AddComponent<AudioSource>();
                bell.spatialBlend = 0f;
                bell.playOnAwake = false;
                jingle = MakeJingle();
            }
            bool airborne = cat.IsAirborne;
            if (airborne != wasAirborne) Ting(0.35f);
            wasAirborne = airborne;
            if (cat.IsGrounded && cat.Speed > 0.4f)
            {
                stepTimer -= Time.deltaTime * cat.Speed;
                if (stepTimer <= 0f) { stepTimer = Random.Range(0.9f, 1.8f); Ting(0.12f); }
            }
        }

        void Ting(float volume)
        {
            bell.pitch = Random.Range(0.95f, 1.08f);
            bell.PlayOneShot(jingle, volume);
        }

        static AudioClip MakeJingle()
        {
            // A small cat bell: a ball rattling in a thin shell, bright partials (3.1, 4.4, 6.8 kHz) that ring briefly.
            const int rate = 22050;
            int n = (int)(rate * 0.35f);
            var data = new float[n];
            var rng = new System.Random(91);
            for (int hit = 0; hit < 3; hit++)
            {
                int at = hit * (int)(rate * 0.045f) + rng.Next(200);
                float amp = 1f - hit * 0.3f;
                for (int i = at; i < n; i++)
                {
                    float t = (float)(i - at) / rate;
                    float env = Mathf.Exp(-t * 18f) * amp;
                    data[i] += (Mathf.Sin(2f * Mathf.PI * 3100f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 4400f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 6800f * t)) * env * 0.18f;
                }
            }
            var clip = AudioClip.Create("Jingle", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void SetWorldSize(Transform t, Vector3 size)
        {
            Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(size.x / Mathf.Max(1e-4f, parent.x), size.y / Mathf.Max(1e-4f, parent.y), size.z / Mathf.Max(1e-4f, parent.z));
        }

        /// <summary>Test hook: SendMessage('SetRewards', 'TestAll'): both rewards now.</summary>
        public void TestAll()
        {
            if (!claws) { claws = true; cat.ClimbBoost = 1.2f; Debug.Log("STORY sharp claws (test)"); }
            if (!collar) { collar = true; WearCollar(); Debug.Log("STORY the collar (test)"); }
            if (!sure) { sure = true; cat.WindFactor = 0.5f; Debug.Log("STORY sure-footed (test)"); }
            ProgressHud.Toast("A red collar with a little bell. Ting!");
        }
    }
}
