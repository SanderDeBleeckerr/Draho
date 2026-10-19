using Draho.Cat;
using Draho.Core;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The lasting reward for finding every nap spot (research round 56: a full set should give a clear, lasting
    /// bonus that changes how the game plays, not only a number). A well rested cat jumps higher for the rest of
    /// the game, and carries a soft warm glow in its fur that lights the way a little at night.
    /// </summary>
    public class WellRested : MonoBehaviour
    {
        [SerializeField] int total = 12;
        [SerializeField] float jumpBoost = 1.15f;
        [SerializeField] Color glowColor = new Color(1f, 0.75f, 0.45f);
        [SerializeField] float glowIntensity = 0.12f;

        CatMotor cat;
        Light glow;
        bool rested;

        public void Configure(int spotCount) => total = spotCount;

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            if (Progress.Instance != null) Progress.Instance.Changed += Check;
            Check();
            if (rested) Debug.Log("STORY well rested (saved)");
        }

        void OnDestroy()
        {
            if (Progress.Instance != null) Progress.Instance.Changed -= Check;
        }

        void Check()
        {
            if (rested || cat == null || Progress.Instance == null || Progress.Instance.NapCount < total) return;
            bool wasLoading = Time.timeSinceLevelLoad < 1f;
            Apply();
            if (wasLoading) return;
            ProgressHud.Toast("Every nap spot in town! Well rested: you jump higher now, and your fur keeps a little warmth for the night.");
            Debug.Log("STORY well rested (all nap spots)");
        }

        void Apply()
        {
            rested = true;
            cat.JumpBoost = jumpBoost;
            var go = new GameObject("RestedGlow");
            go.transform.SetParent(cat.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.range = 0.9f;
            glow.shadows = LightShadows.None;
            glow.intensity = 0f;
        }

        void Update()
        {
            // Only noticeable in the dark; a slow breath, like a purr.
            if (glow == null) return;
            float night = SkyMood.NightAmount;
            glow.enabled = night > 0.05f;
            glow.intensity = glowIntensity * night * (0.85f + 0.15f * Mathf.Sin(Time.time * 1.3f));
        }

        /// <summary>Test hook: SendMessage('WellRested', 'TestComplete').</summary>
        public void TestComplete()
        {
            if (rested) return;
            Apply();
            ProgressHud.Toast("Every nap spot in town! Well rested: you jump higher now, and your fur keeps a little warmth for the night.");
            Debug.Log("STORY well rested (test)");
        }
    }
}
