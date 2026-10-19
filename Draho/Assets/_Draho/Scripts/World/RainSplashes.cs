using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// Splashes where the rain lands, near the camera (research round 74: games fake rain impacts with short
    /// splash particles placed by raycasts round the viewer instead of colliding every drop; a drop that hits
    /// water or a hard roof throws up a small crown and a few droplets for a tenth of a second or so).
    /// Only while the storm lasts and only on surfaces open to the sky.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class RainSplashes : MonoBehaviour
    {
        [SerializeField] float radius = 4f;
        [SerializeField] float perSecond = 70f;

        ParticleSystem system;
        Transform cam;
        float owed;

        void Awake()
        {
            system = GetComponent<ParticleSystem>();
            gameObject.layer = 0; // the builder saw empty bounds: not a near-only detail
        }

        void Update()
        {
            if (!Storm.Active) return;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam == null) return;
            owed += perSecond * Time.deltaTime;
            int n = Mathf.Min(12, Mathf.FloorToInt(owed));
            owed -= n;
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                Vector3 top = cam.position + cam.forward * 1.5f + new Vector3(r.x, 6f, r.y);
                if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 20f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.5f) continue; // walls and steep slate: the drops run off
                var e = new ParticleSystem.EmitParams { position = hit.point + Vector3.up * 0.01f, applyShapeToPosition = true };
                system.Emit(e, 3);
            }
        }
    
        /// <summary>Test hook: SendMessage('RainSplashes', 'TestReport').</summary>
        public void TestReport() => Debug.Log($"STATE rain splashes: {system.particleCount} alive, storm {Storm.Active}");
    }
}
