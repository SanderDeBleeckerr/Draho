using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Lasting rewards for two more full sets (like WellRested, research round 56: a full set should change the
    /// game, not only a number). Every scratching place: sharp claws, climbing 20% faster. Every gift: the humans
    /// buy a red collar with a little bell, and the cat wears it from then on.
    /// </summary>
    public class SetRewards : MonoBehaviour
    {
        [SerializeField] int scratchTotal = 8;
        [SerializeField] int giftTotal = 6;
        [SerializeField] Material collarMat;
        [SerializeField] Material bellMat;

        CatMotor cat;
        bool claws, collar;

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
            ProgressHud.Toast("A red collar with a little bell. Ting!");
        }
    }
}
