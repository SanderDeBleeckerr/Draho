using Draho.Core;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Low mist lying in the gardens in the early morning (research round 69: radiation fog forms on clear, still
    /// nights over grass, lies in the lowest metre or two, and "burns off" in the first hours of sun). Large, very
    /// faint billboard puffs that drift slowly (no flat sheets: those draw hard lines where they cut walls). Thick at
    /// dawn and at the start of the game (the first morning), thinning to nothing over a few minutes.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class MorningMist : MonoBehaviour
    {
        [SerializeField] float burnOff = 240f;
        [SerializeField] float alpha = 0.16f;

        ParticleSystem system;
        Material material;
        float amount = -1f;

        void Awake()
        {
            system = GetComponent<ParticleSystem>();
            var r = GetComponent<ParticleSystemRenderer>();
            material = r.material;
            gameObject.layer = 0; // the builder saw empty bounds: not a near-only detail
        }

        void Update()
        {
            // The first morning: mist from the start, thinning over a few minutes; at dawn (chapter 11) it comes back.
            bool firstMorning = (Progress.Instance == null || Progress.Instance.SavedBeat <= 1) && Time.timeSinceLevelLoad < burnOff;
            float target = SkyMood.Current == SkyMood.Mood.Dawn ? 1f
                : firstMorning ? 1f - Time.timeSinceLevelLoad / burnOff
                : 0f;
            amount = amount < 0f ? target : Mathf.MoveTowards(amount, target, Time.deltaTime / 20f);
            var emission = system.emission;
            emission.enabled = amount > 0.02f;
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha * amount));
            if (amount <= 0.02f && system.particleCount > 0 && Time.frameCount % 60 == 0) system.Clear();
        }

        /// <summary>Test hook: SendMessage('MorningMist', 'TestMist'): thick mist now.</summary>
        public void TestMist()
        {
            amount = 1f;
            burnOff = Time.timeSinceLevelLoad + 600f;
            if (Progress.Instance == null || Progress.Instance.SavedBeat > 1) SkyMood.Set(SkyMood.Mood.Dawn, 0.1f);
        }
    }
}
