using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Draho.CameraRig
{
    /// <summary>
    /// Keeps the game smooth on slow phones: it lowers the render resolution (URP render scale) when the
    /// frame rate stays low, and raises it again when there is time left. It also stops drawing small
    /// details (layer "Detail") far from the camera. ?lockscale=1 in the URL keeps full resolution.
    /// </summary>
    public class AdaptiveQuality : MonoBehaviour
    {
        public const int DetailLayer = 8;

        [SerializeField] float detailDistance = 35f;
        [SerializeField] float lowFps = 40f;
        [SerializeField] float highFps = 57f;
        [SerializeField] float minScale = 0.6f;
        [SerializeField] float window = 1.5f;

        UniversalRenderPipelineAsset urp;
        float elapsed;
        int frames;
        bool locked;

        void Start()
        {
            var cam = GetComponent<Camera>();
            var distances = new float[32];
            distances[DetailLayer] = detailDistance;
            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true;
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            locked = (Application.absoluteURL ?? "").Contains("lockscale=1");
            if (urp != null) urp.renderScale = 1f;
        }

        void Update()
        {
            if (urp == null || locked) return;
            elapsed += Time.unscaledDeltaTime;
            frames++;
            if (elapsed < window) return;
            float fps = frames / elapsed;
            elapsed = 0f;
            frames = 0;
            float scale = urp.renderScale;
            if (fps < lowFps && scale > minScale) scale = Mathf.Max(minScale, scale - 0.1f);
            else if (fps > highFps && scale < 1f) scale = Mathf.Min(1f, scale + 0.05f);
            else return;
            urp.renderScale = scale;
            Debug.Log($"QUALITY {fps:0} fps: render scale {scale:0.00}");
        }
    }
}
