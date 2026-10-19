using Draho.Cat;
using Draho.Core;
using UnityEngine;

namespace Draho.World
{
    /// <summary>A feather to find: it turns slowly and floats; the cat picks it up by touching it.</summary>
    public class Collectable : MonoBehaviour
    {
        [SerializeField] string id;

        /// <summary>The save id (the sniff sense looks for ids not yet collected).</summary>
        public string Id => id;
        [SerializeField] int coins = 2;
        [SerializeField] Transform visual;
        [SerializeField] float pickRadius = 0.45f;

        CatMotor cat;
        Vector3 basePosition;

        public void Configure(string collectId, Transform visualPart)
        {
            id = collectId;
            visual = visualPart;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            basePosition = visual != null ? visual.localPosition : Vector3.zero;
            if (Progress.Instance != null && Progress.Instance.IsCollected(id)) gameObject.SetActive(false);
        }

        void Update()
        {
            float t = Time.time + id.GetHashCode() % 7;
            if (visual != null)
            {
                visual.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * 2f) * 0.04f);
                visual.localRotation = Quaternion.Euler(20f, t * 60f, 35f);
            }
            if (cat == null) return;
            Vector3 d = cat.transform.position + Vector3.up * 0.15f - transform.position;
            if (d.magnitude > pickRadius) return;
            Progress.Instance?.Collect(id, coins);
            gameObject.SetActive(false);
        }
    }
}
