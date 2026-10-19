using Draho.Cat;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A bed of catnip in the begijnhof herb garden, and the faint green scent trail that leads to it (wisps that
    /// rise from the ground). A sniff gives Drahomil the zoomies: for a short time he runs much faster. Real cats
    /// react to catnip for about ten minutes, mostly by sniffing, rubbing and rolling (research round 52); in the
    /// game it is a short burst, and it works again after a rest.
    /// </summary>
    public class Catnip : MonoBehaviour
    {
        [SerializeField] ParticleSystem[] trail;
        [SerializeField] float sniffRadius = 0.7f;
        [SerializeField] float zoomTime = 12f;
        [SerializeField] float boost = 1.6f;
        [SerializeField] float restTime = 40f;

        CatMotor cat;
        float zoom, rest;

        public bool Sniffed { get; private set; }

        public void Configure(ParticleSystem[] wisps) => trail = wisps;

        void Awake()
        {
            cat = FindAnyObjectByType<CatMotor>();
            ShowTrail(false);
        }

        /// <summary>The story shows the scent trail.</summary>
        public void ShowTrail(bool on)
        {
            if (trail == null) return;
            foreach (var ps in trail)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.enabled = on;
                if (on) ps.Play();
            }
        }

        void Update()
        {
            if (cat == null) return;
            float dt = Time.deltaTime;
            rest -= dt;
            if (zoom > 0f)
            {
                zoom -= dt;
                if (zoom <= 0f)
                {
                    cat.SpeedBoost = 1f;
                    Debug.Log("CAT zoomies over");
                }
            }
            Vector3 d = cat.transform.position - transform.position;
            d.y = 0f;
            if (rest <= 0f && d.magnitude < sniffRadius)
            {
                Sniffed = true;
                zoom = zoomTime;
                rest = restTime;
                cat.SpeedBoost = boost;
                ShowTrail(false);
                ProgressHud.Toast("Catnip! Sniff, rub, roll... and ZOOMIES! Run!");
                Debug.Log("STORY catnip: zoomies");
            }
        }

        void OnDisable()
        {
            if (cat != null) cat.SpeedBoost = 1f;
        }
    }
}
