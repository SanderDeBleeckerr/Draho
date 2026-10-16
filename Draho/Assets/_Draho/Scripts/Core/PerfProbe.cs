using Unity.Profiling;
using UnityEngine;

namespace Draho.Core
{
    /// <summary>
    /// Test hook for the playtest: SendMessage('PerfProbe', 'Log') writes render counters (draw calls, batches,
    /// set-pass calls, triangles), visible renderers and the frame time to the console ("PERF ...").
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        ProfilerRecorder drawCalls, batches, setPass, triangles, shadowCasters;
        float frameTime;

        void OnEnable()
        {
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
        }

        void OnDisable()
        {
            drawCalls.Dispose(); batches.Dispose(); setPass.Dispose(); triangles.Dispose(); shadowCasters.Dispose();
        }

        void Update() => frameTime = Mathf.Lerp(frameTime, Time.unscaledDeltaTime, 0.1f);

        public void Log()
        {
            int visible = 0, total = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled) continue;
                total++;
                if (r.isVisible) visible++;
            }
            string V(ProfilerRecorder r) => r.Valid ? r.LastValue.ToString() : "n/a";
            Debug.Log($"PERF draws {V(drawCalls)} batches {V(batches)} setpass {V(setPass)} tris {V(triangles)} " +
                      $"shadowcasters {V(shadowCasters)} renderers {visible}/{total} visible frame {frameTime * 1000f:0} ms " +
                      $"scale {(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset u ? u.renderScale : 1f):0.00}");
        }
    }
}
