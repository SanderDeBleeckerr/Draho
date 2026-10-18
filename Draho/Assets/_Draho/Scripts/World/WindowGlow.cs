using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The lit windows of the block: one shared material whose glow follows <see cref="SkyMood.NightAmount"/>.
    /// The windows are one combined mesh (a few draw calls for the whole block), so this is cheap on the phone.
    /// </summary>
    public class WindowGlow : MonoBehaviour
    {
        [SerializeField] Material litWindows;
        [SerializeField] Color glow = new Color(1f, 0.7f, 0.38f);
        [SerializeField] float strength = 2.4f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        float shown = -1f;

        public void Configure(Material material) => litWindows = material;

        void Update()
        {
            // Lights go on in the evening, full at night.
            float on = Mathf.Clamp01((SkyMood.NightAmount - 0.25f) / 0.75f);
            if (litWindows == null || Mathf.Abs(on - shown) < 0.01f) return;
            shown = on;
            litWindows.SetColor(EmissionId, glow * (strength * on));
        }

        void OnDestroy()
        {
            // The material is an asset: do not leave it glowing in the editor.
            if (litWindows != null) litWindows.SetColor(EmissionId, Color.black);
        }
    }
}
