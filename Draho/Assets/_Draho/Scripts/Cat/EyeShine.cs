using Draho.World;
using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Eyeshine: at night a cat's eyes glow green-gold when they look back at a light near the viewer (the
    /// tapetum lucidum reflects it, like a camera flash). The camera carries a small fill light, so the eyes glow
    /// when Drahomil faces the camera in the dark, and more the straighter he looks (research round 43).
    /// </summary>
    public class EyeShine : MonoBehaviour
    {
        [SerializeField] Material eye;
        [SerializeField] Color shine = new Color(0.5f, 0.95f, 0.35f);
        [SerializeField] float strength = 2.2f;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        Transform cam;
        float shown = -1f;

        void Update()
        {
            if (eye == null) return;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            float glow = 0f;
            float night = Mathf.Clamp01(SkyMood.NightAmount * 2f - 1f);
            if (cam != null && night > 0f)
            {
                Vector3 toCam = cam.position - transform.position;
                toCam.y = 0f;
                Vector3 facing = transform.forward;
                facing.y = 0f;
                float look = Mathf.Clamp01(Vector3.Dot(facing.normalized, toCam.normalized));
                glow = night * Mathf.Pow(look, 6f);
            }
            if (Mathf.Abs(glow - shown) < 0.01f) return;
            shown = glow;
            eye.SetColor(EmissionId, shine * (strength * glow));
        }

        void OnDestroy()
        {
            // The material is an asset: do not leave it glowing in the editor.
            if (eye != null) eye.SetColor(EmissionId, Color.black);
        }
    }
}
