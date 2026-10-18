using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Drahomil's humans, seen from cat height: two pairs of legs at the front door. Hidden until they come home;
    /// then they shift their weight a little while the cat greets them.
    /// </summary>
    public class Humans : MonoBehaviour
    {
        Renderer[] renderers;
        float phase;

        public bool Home { get; private set; }
        /// <summary>True once the humans are home: the bounty board by the door stays quiet in the ending.</summary>
        public static bool AnyHome { get; private set; }

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;
            AnyHome = false;
        }

        public void ComeHome()
        {
            if (Home) return;
            Home = AnyHome = true;
            foreach (var r in renderers) r.enabled = true;
            Debug.Log("STORY the humans come home");
        }

        void Update()
        {
            if (!Home) return;
            phase += Time.deltaTime;
            // Each person sways a little from foot to foot.
            for (int i = 0; i < transform.childCount; i++)
            {
                var person = transform.GetChild(i);
                person.localRotation = Quaternion.Euler(0f, person.localEulerAngles.y, Mathf.Sin(phase * 1.3f + i * 2f) * 1.5f);
            }
        }
    }
}
