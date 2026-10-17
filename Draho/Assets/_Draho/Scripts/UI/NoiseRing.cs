using Draho.Cat;
using Draho.World;
using UnityEngine;

namespace Draho.UI
{
    /// <summary>
    /// A faint ring on the ground around Drahomil: how far his steps can be heard (sneak small and green,
    /// walk yellow, run large and orange). It shows only when an animal that listens is near, so the
    /// player can see why a mouse, a bird or Hrozna noticed him (research: readable stealth feedback).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NoiseRing : MonoBehaviour
    {
        [SerializeField] float showDistance = 6f;
        [SerializeField] int segments = 48;

        CatMotor cat;
        LineRenderer line;
        Component[] listeners;
        float radius, alpha;

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            line = GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.positionCount = segments;
            line.widthMultiplier = 0.02f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var found = new System.Collections.Generic.List<Component>();
            found.AddRange(FindObjectsByType<Mouse>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            found.AddRange(FindObjectsByType<Bird>(FindObjectsSortMode.None));
            found.AddRange(FindObjectsByType<Duck>(FindObjectsSortMode.None));
            found.AddRange(FindObjectsByType<Hrozna>(FindObjectsSortMode.None));
            found.AddRange(FindObjectsByType<Hedgehog>(FindObjectsSortMode.None));
            listeners = found.ToArray();
        }

        void LateUpdate()
        {
            if (cat == null) return;
            Vector3 p = cat.transform.position;
            bool near = false;
            foreach (var l in listeners)
                if (l != null && l.gameObject.activeInHierarchy && (l.transform.position - p).sqrMagnitude < showDistance * showDistance) { near = true; break; }

            // Same scale as the mouse's hearing: still 0.25, sneak 0.4, walk 1.2, run 2.4 m.
            float target = cat.IsCrouching ? 0.4f
                : cat.Speed < 0.05f ? 0.25f
                : cat.Speed > cat.Stats.walkSpeed * 1.5f || cat.IsAirborne ? 2.4f
                : 1.2f;
            radius = Mathf.Lerp(radius, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            alpha = Mathf.MoveTowards(alpha, near && !cat.IsClimbing ? 0.55f : 0f, 2f * Time.deltaTime);
            line.enabled = alpha > 0.01f;
            if (!line.enabled) return;

            Color c = radius < 0.6f ? new Color(0.5f, 1f, 0.6f) : radius < 1.6f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.55f, 0.3f);
            c.a = alpha;
            line.startColor = line.endColor = c;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(p.x + Mathf.Cos(a) * radius, p.y + 0.03f, p.z + Mathf.Sin(a) * radius));
            }
        }
    }
}
