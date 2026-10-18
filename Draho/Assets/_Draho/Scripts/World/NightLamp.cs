using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// An outdoor lamp that switches on in the evening and glows at night: a point light (no shadows, cheap on
    /// the phone) and an emissive bulb that the bloom picks up.
    /// </summary>
    public class NightLamp : MonoBehaviour
    {
        [SerializeField] Light lamp;
        [SerializeField] Renderer bulb;
        [SerializeField] float intensity = 2.2f;
        [SerializeField] Color glow = new Color(1f, 0.72f, 0.4f);

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        float shown = -1f;

        public bool IsOn => SkyMood.NightAmount > 0.3f;

        public void Configure(Light pointLight, Renderer bulbRenderer)
        {
            lamp = pointLight;
            bulb = bulbRenderer;
        }

        void Update()
        {
            // On from the evening; full at night.
            float on = Mathf.Clamp01((SkyMood.NightAmount - 0.3f) / 0.7f);
            if (Mathf.Abs(on - shown) < 0.01f) return;
            shown = on;
            if (lamp != null)
            {
                lamp.enabled = on > 0f;
                lamp.intensity = intensity * on;
            }
            if (bulb != null)
            {
                block ??= new MaterialPropertyBlock();
                bulb.GetPropertyBlock(block);
                block.SetColor(EmissionId, glow * (0.15f + 3f * on));
                bulb.SetPropertyBlock(block);
            }
        }
    }
}
