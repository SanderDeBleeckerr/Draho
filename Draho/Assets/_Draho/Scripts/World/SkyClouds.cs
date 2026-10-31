using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Clouds in the sky (round 106: fair-weather cumulus are white on top with grey, flat bases; at dusk they take the
    /// warm light; under storm cloud the sky is one grey cover). Soft billboards far away round the camera (the
    /// Draho/Clouds shader) that drift slowly with the wind; their colours follow the time of day.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class SkyClouds : MonoBehaviour
    {
        [SerializeField] float driftSpeed = 0.4f; // degrees of sky a second

        static readonly int LitId = Shader.PropertyToID("_Lit");
        static readonly int ShadeId = Shader.PropertyToID("_Shade");
        static readonly int DriftId = Shader.PropertyToID("_Drift");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        Material material;
        float drift;

        void Awake()
        {
            material = GetComponent<MeshRenderer>().material;
            gameObject.layer = 0;
        }

        void Update()
        {
            drift += driftSpeed * Time.deltaTime * (Storm.Active ? 4f : 1f);
            material.SetFloat(DriftId, drift);
            float night = SkyMood.NightAmount;
            Color lit, shade;
            float opacity;
            if (Storm.Active) { lit = new Color(0.3f, 0.31f, 0.34f); shade = new Color(0.16f, 0.17f, 0.2f); opacity = 1f; }
            else switch (SkyMood.Current)
            {
                case SkyMood.Mood.Evening: lit = new Color(1f, 0.72f, 0.52f); shade = new Color(0.55f, 0.42f, 0.5f); opacity = 0.9f; break;
                case SkyMood.Mood.Dawn: lit = new Color(1f, 0.8f, 0.72f); shade = new Color(0.62f, 0.55f, 0.65f); opacity = 0.85f; break;
                default: lit = Color.white; shade = new Color(0.72f, 0.75f, 0.82f); opacity = 0.95f; break;
            }
            // At night the clouds are faint grey shapes against the stars.
            lit = Color.Lerp(lit, new Color(0.14f, 0.15f, 0.2f), night);
            shade = Color.Lerp(shade, new Color(0.07f, 0.08f, 0.11f), night);
            material.SetColor(LitId, lit);
            material.SetColor(ShadeId, shade);
            material.SetFloat(OpacityId, Mathf.Lerp(opacity, 0.5f, night));
        }
    }
}
