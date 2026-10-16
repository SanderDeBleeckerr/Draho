using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Zlatenka, the leopard tortoise: Drahomil's oldest friend. She walks very slowly from one sunny spot
    /// to the next. If Drahomil lies down next to her (DUCK, stay still), they nap together in the sun.
    /// </summary>
    public class Tortoise : MonoBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] Vector3[] sunnySpots;
        [SerializeField] float speed = 0.05f;
        [SerializeField] float napDistance = 1.0f;
        [SerializeField] float napTime = 5f;
        [SerializeField] int napCoins = 3;
        [SerializeField] float napCooldown = 120f;

        CatMotor cat;
        int spot;
        float rest = 30f, nap, cooldown, phase; // she starts with a long rest at her first sunny spot
        bool met;

        public void Configure(Transform headPart, Vector3[] spots)
        {
            head = headPart;
            sunnySpots = spots;
        }

        void Awake() => cat = FindAnyObjectByType<CatMotor>();

        void Update()
        {
            float dt = Time.deltaTime;
            phase += dt;
            cooldown -= dt;
            Vector3 p = transform.position;
            if (sunnySpots != null && sunnySpots.Length > 0)
            {
                Vector3 to = sunnySpots[spot] - p;
                to.y = 0f;
                if (to.magnitude < 0.1f)
                {
                    rest -= dt;
                    if (rest <= 0f) { spot = (spot + 1) % sunnySpots.Length; rest = Random.Range(20f, 40f); }
                }
                else if (nap <= 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), 20f * dt);
                    transform.position += transform.forward * speed * dt;
                }
            }
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(phase * 0.6f) * 8f, Mathf.Sin(phase * 0.35f) * 25f, 0f);
            if (cat == null) return;

            float d = Vector3.Distance(new Vector3(cat.transform.position.x, 0f, cat.transform.position.z), new Vector3(p.x, 0f, p.z));
            if (!met && d < 2.5f)
            {
                met = true;
                ProgressHud.Toast("Zlatenka! Drahomil's oldest friend. Lie down next to her (DUCK) for a nap in the sun.");
            }
            bool lyingNext = d < napDistance && cat.IsCrouching && cat.Speed < 0.05f;
            nap = lyingNext ? nap + dt : 0f;
            if (nap >= napTime && cooldown <= 0f)
            {
                cooldown = napCooldown;
                Progress.Instance?.AddCoins(napCoins, "nap with Zlatenka");
                Progress.Instance?.Save();
                ProgressHud.Toast($"A warm nap in the sun with Zlatenka. +{napCoins} coins");
                Debug.Log("STORY nap with Zlatenka");
            }
        }
    }
}
