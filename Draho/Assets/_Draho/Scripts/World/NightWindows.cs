using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The lit windows of the surrounding blocks: the facade materials have an emission map with about a third of the
    /// windows lit; after dusk this turns their glow up (warm), and down again at dawn.
    /// </summary>
    public class NightWindows : MonoBehaviour
    {
        [SerializeField] Material[] facades;
        [SerializeField] float strength = 1.3f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        float shown = -1f;

        public void Configure(Material[] materials) => facades = materials;

        void Update()
        {
            float target = Mathf.Clamp01((SkyMood.NightAmount - 0.3f) / 0.5f);
            if (Mathf.Abs(target - shown) < 0.01f) return;
            shown = target;
            var c = new Color(1f, 0.85f, 0.65f) * (strength * shown);
            foreach (var m in facades)
                if (m != null) m.SetColor(EmissionId, c);
        }
    }
}
